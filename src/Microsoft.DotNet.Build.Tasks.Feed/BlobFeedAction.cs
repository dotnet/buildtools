// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
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

        public BlobFeed feed;
        public int MaxClients { get; set; } = 8;

        public BlobFeedAction(string expectedFeedUrl, string accountKey, MSBuild.TaskLoggingHelper Log)
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
                // Sleet internally retries 5 times on each package when the push operation fails so we don't need to retry ourselves.
                for (int i = 0; i <= 1; i++)
                {
                    try
                    {
                        bool result = await PushAsync(items.ToList(), allowOverwrite);
                        return result;
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("init"))
                    {
                        Log.LogWarning($"Sub-feed {source.FeedSubPath} has not been initialized. Initializing now...");
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

              return false;
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }

        public async Task UploadAssets(IEnumerable<string> items, bool allowOverwrite)
        {
            using (var clientThrottle = new SemaphoreSlim(this.MaxClients, this.MaxClients))
            {
                try
                {
                    await Task.WhenAll(items.Select(item => UploadAsync(item, $"{feed.RelativePath}assets/", clientThrottle, allowOverwrite, CancellationToken)));
                }
                catch (Exception exc)
                {
                    Log.LogErrorFromException(exc);
                    throw;
                }
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

        private async Task UploadAsync(string item, string uploadPath, SemaphoreSlim clientThrottle, bool allowOverwrite, CancellationToken cancellationToken)
        {
            if (!File.Exists(item))
                throw new Exception(string.Format("The file '{0}' does not exist.", item));

            string fileName = Path.GetFileName(item);
            uploadPath += fileName;
            await clientThrottle.WaitAsync();
            string leaseId = string.Empty;

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(source.ConnectionString);
            CloudBlobClient client = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(feed.ContainerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(uploadPath);
            AzureBlobLease blobLease = new AzureBlobLease(blob);
            bool blobExists = await blob.ExistsAsync();
            bool isLeaseRequired = allowOverwrite && blobExists;

            if (isLeaseRequired)
            {
                try
                {
                    leaseId = blobLease.LeaseId;
                    Log.LogMessage($"Obtained lease ID {leaseId} for {uploadPath}.");
                }
                catch (Exception)
                {
                    Log.LogError($"Unable to obtain lease on {uploadPath}");
                }
            }

            try
            {
                if (!blobExists || allowOverwrite)
                {
                    Log.LogMessage($"Uploading {item} to {uploadPath}.");
                    AccessCondition accessCondition = new AccessCondition
                    {
                        LeaseId = leaseId
                    };

                    OperationContext opContext = new OperationContext
                    {
                        LogLevel = LogLevel.Informational
                    };


                    await blob.UploadFromFileAsync(item, accessCondition, new BlobRequestOptions(), opContext, cancellationToken);
                }
                else
                {
                    Log.LogMessage($"Skipping uploading of {item} to {uploadPath}. Already exists.");
                }
            }
            catch (Exception)
            {
                Log.LogError($"Unable to upload to {uploadPath}");
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
    }
}
