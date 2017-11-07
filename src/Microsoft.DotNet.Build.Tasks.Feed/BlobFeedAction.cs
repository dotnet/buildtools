// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.CloudTestTasks;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using Sleet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MSBuild = Microsoft.Build.Utilities;
using CloudTestTasks = Microsoft.DotNet.Build.CloudTestTasks;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    sealed class BlobFeedAction
    {
        private MSBuild.TaskLoggingHelper Log;
        private static readonly CancellationTokenSource TokenSource = new CancellationTokenSource();
        private static readonly CancellationToken CancellationToken = TokenSource.Token;
        private const string feedRegex = @"(?<feedurl>https:\/\/(?<accountname>[^\.-]+)(?<domain>[^\/]*)\/((?<token>[a-zA-Z0-9+\/]*?\/\d{4}-\d{2}-\d{2})\/)?(?<containername>[^\/]+)\/(?<relativepath>.*\/)?)index\.json";
        private string feedUrl;
        private SleetSource source;
        private int retries;
        private TimeSpan delay;

        public BlobFeed feed;

        public BlobFeedAction(string expectedFeedUrl, string accountKey, MSBuild.TaskLoggingHelper Log, int retryAttempts, int retryDelay)
        {
            this.Log = Log;
            Match m = Regex.Match(expectedFeedUrl, feedRegex);
            if (m.Success)
            {
                string accountName = m.Groups["accountname"].Value;
                string containerName = m.Groups["containername"].Value;
                string relativePath = m.Groups["relativepath"].Value;
                feed = new BlobFeed(accountName, accountKey, containerName, relativePath, Log);
                feedUrl = m.Groups["feedurl"].Value;
                retries = retryAttempts;
                delay = TimeSpan.FromSeconds(retryDelay);

                source = new SleetSource
                {
                    Name = feed.ContainerName,
                    Type = "azure",
                    Path = feedUrl,
                    Container = feed.ContainerName,
                    FeedSubPath = feed.RelativePath,
                    ConnectionString = $"DefaultEndpointsProtocol=https;AccountName={feed.AccountName};AccountKey={feed.AccountKey};EndpointSuffix=core.windows.net"
                };
            }
            else
            {
                throw new Exception("Unable to parse expected feed. Please check ExpectedFeedUrl.");
            }
        }

        public async Task<bool> PushToFeed(IEnumerable<string> items, bool allowOverwrite = false)
        {
            if (IsSanityChecked(items))
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    Log.LogError("Task PushToFeed cancelled");
                    CancellationToken.ThrowIfCancellationRequested();
                }

                await PushItemsToFeedAsync(items, allowOverwrite);
            }

            return !Log.HasLoggedErrors;
        }

        public async Task<bool> PushItemsToFeedAsync(IEnumerable<string> items, bool allowOverwrite)
        {
            Log.LogMessage(MessageImportance.Low, $"START pushing items to feed");

            try
            {
                // In case the first Push attempt fails with an InvalidOperationException we Init the feed and retry the Push command once.
                // We also retry in case Sleet is not able to get a lock on the feed since it does not retry in this case.
                for (int i = 0; i < retries; i++)
                {
                    bool requiresInit = false;

                    try
                    {
                        bool result = await PushAsync(items.ToList(), allowOverwrite);
                        return result;
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("init"))
                    {
                        Log.LogWarning($"Sub-feed {source.FeedSubPath} has not been initialized. Initializing now...");
                        requiresInit = true;
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("Unable to obtain a lock on the feed."))
                    {
                        Log.LogWarning($"Sleet was not able to get a lock on the feed. Sleeping {delay} seconds and retrying.");
                        await Task.Delay(delay);

                        // Pushing packages might take more than just 60 seconds, so on each iteration we add the defined value to itself so wait for
                        // a bit more the next iterations
                        delay += delay;
                    }

                    // If the feed has not been Init'ed this will be caught in the first iteration
                    if (requiresInit && i == 0)
                    {
                        i--;
                        bool result = await InitAsync();

                        if (result)
                        {
                            Log.LogMessage($"Initializing sub-feed {source.FeedSubPath} succeeded!");
                        }
                        else
                        {
                            Log.LogError($"Initializing sub-feed {source.FeedSubPath} failed!");
                        }
                    }
                }

                Log.LogError($"Pushing packages to sub-feed {source.FeedSubPath} failed!");

                return false;
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }

        public async Task UploadAssets(ITaskItem item, SemaphoreSlim clientThrottle, bool allowOverwrite = false)
        {
            string relativeBlobPath = item.GetMetadata("RelativeBlobPath");
            if (string.IsNullOrEmpty(relativeBlobPath))
                throw new Exception($"Metadata 'RelativeBlobPath' is missing for item '{item.ItemSpec}'.") ;
            relativeBlobPath = $"{feed.RelativePath}{relativeBlobPath.Replace("\\", "/")}"; 
            
            Log.LogMessage($"Uploading {relativeBlobPath}");

            await clientThrottle.WaitAsync();

            // this defines the lease for 15 seconds (max is 60) and 3000 milliseconds between requests
            CloudTestTasks.AzureBlobLease blobLease = new CloudTestTasks.AzureBlobLease(
                feed.AccountName, 
                feed.AccountKey, 
                string.Empty, 
                feed.ContainerName, 
                relativeBlobPath, 
                Log, 
                "15", 
                "5000");

            bool blobExists = await feed.CheckIfBlobExists(relativeBlobPath);
            bool isLeaseRequired = allowOverwrite && blobExists;
            string leaseId = null;

            if (isLeaseRequired)
            {
                try
                {
                    leaseId = blobLease.Acquire();
                    Log.LogMessage($"Obtained lease ID {leaseId} for {relativeBlobPath}.");
                }
                catch (Exception exc)
                {
                    Log.LogError($"Unable to obtain lease on {relativeBlobPath} due to exception: {exc}");
                    throw;
                }
            }

            try
            {
                if (!blobExists || allowOverwrite)
                {
                    Log.LogMessage($"Uploading {item} to {relativeBlobPath}.");
                    UploadClient uploadClient = new UploadClient(Log);
                    await uploadClient.UploadBlockBlobAsync(
                        CancellationToken,
                        feed.AccountName,
                        feed.AccountKey,
                        feed.ContainerName,
                        item.ItemSpec,
                        relativeBlobPath,
                        leaseId);

                }
                else
                {
                    Log.LogMessage($"Skipping uploading of {item} to {relativeBlobPath}. Already exists.");
                }
            }
            catch (Exception)
            {
                Log.LogError($"Unable to upload to {relativeBlobPath}");
                throw;
            }
            finally
            {
                if (isLeaseRequired)
                {
                    blobLease.Release();
                }

                clientThrottle.Release();
            }
        }

        private bool IsSanityChecked(IEnumerable<string> items)
        {
            Log.LogMessage(MessageImportance.Low, $"START checking sanitized items for feed");
            foreach (var item in items)
            {
                if (items.Any(s => Path.GetExtension(item) != ".nupkg"))
                {
                    Log.LogError($"{item} is not a nupkg");
                    return false;
                }
            }
            List<string> duplicates = items.GroupBy(x => x)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key).ToList();
            if (duplicates.Count > 0)
            {
                Log.LogError($"Duplicates found: {duplicates}");
                return false;
            }
            Log.LogMessage(MessageImportance.Low, $"DONE checking for sanitized items for feed");
            return true;
        }

        private LocalSettings GetSettings()
        {
            SleetSettings sleetSettings = new SleetSettings()
            {
                Sources = new List<SleetSource>
                    {
                       source
                    }
            };

            LocalSettings settings = new LocalSettings
            {
                Json = JObject.FromObject(sleetSettings)
            };

            return settings;
        }

        private AzureFileSystem GetAzureFileSystem()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(source.ConnectionString);
            AzureFileSystem fileSystem = new AzureFileSystem(new LocalCache(), new Uri(source.Path), new Uri(source.Path), storageAccount, source.Name, source.FeedSubPath);
            return fileSystem;
        }

        private async Task<bool> PushAsync(IEnumerable<string> items, bool allowOverwrite)
        {
            LocalSettings settings = GetSettings();
            AzureFileSystem fileSystem = GetAzureFileSystem();
            bool result = await PushCommand.RunAsync(settings, fileSystem, items.ToList(), allowOverwrite, !allowOverwrite, new SleetLogger(Log));
            return result;
        }

        private async Task<bool> InitAsync()
        {
            LocalSettings settings = GetSettings();
            AzureFileSystem fileSystem = GetAzureFileSystem();
            bool result = await InitCommand.RunAsync(settings, fileSystem, true, true, new SleetLogger(Log), CancellationToken);
            return result;
        }
    }
}
