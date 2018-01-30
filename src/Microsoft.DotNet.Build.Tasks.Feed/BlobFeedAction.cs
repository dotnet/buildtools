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
using NuGet.Packaging.Core;
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
        private bool hasToken = false;

        public BlobFeed feed;

        public BlobFeedAction(string expectedFeedUrl, string accountKey, MSBuild.TaskLoggingHelper Log)
        {
            // This blob feed action regex is custom because of the way that nuget handles query strings (it doesn't)
            // Instead of encoding the query string containing the SAS at the end of the URL we encode it at the beginning.
            // As a result, we can't parse this feed url like a traditional feed url.  When this changes, this code could be simplified and
            // BlobUriParser could be used instead.
            this.Log = Log;
            Match m = Regex.Match(expectedFeedUrl, feedRegex);
            if (m.Success)
            {
                string accountName = m.Groups["accountname"].Value;
                string containerName = m.Groups["containername"].Value;
                string relativePath = m.Groups["relativepath"].Value;
                feed = new BlobFeed(accountName, accountKey, containerName, relativePath, Log);
                feedUrl = m.Groups["feedurl"].Value;
                hasToken = !string.IsNullOrEmpty(m.Groups["token"].Value);

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

            if (!items.Any())
            {
                Log.LogMessage("No items to push found in the items list.");
                return true;
            }

            try
            {
                bool result = await PushAsync(items, allowOverwrite);
                return result;
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }

        public async Task UploadAssets(ITaskItem item, SemaphoreSlim clientThrottle, int uploadTimeout, bool allowOverwrite = false)
        {
            string relativeBlobPath = item.GetMetadata("RelativeBlobPath");

            if (string.IsNullOrEmpty(relativeBlobPath))
            {
                string fileName = Path.GetFileName(item.ItemSpec);
                string recursiveDir = item.GetMetadata("RecursiveDir");
                relativeBlobPath = $"{recursiveDir}{fileName}";
            }

            string contentType = item.GetMetadata("ContentType");

            relativeBlobPath = $"{feed.RelativePath}{relativeBlobPath}".Replace("\\", "/");

            if (relativeBlobPath.Contains("//"))
            {
                Log.LogError(
                    $"Item '{item.ItemSpec}' RelativeBlobPath contains virtual directory " +
                    $"without name (double forward slash): '{relativeBlobPath}'");
                return;
            }

            Log.LogMessage($"Uploading {relativeBlobPath}");

            await clientThrottle.WaitAsync();

            try
            {
                bool blobExists = false;

                if (!allowOverwrite)
                {
                    blobExists = await feed.CheckIfBlobExists(relativeBlobPath);
                }

                if (allowOverwrite || !blobExists)
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
                        contentType,
                        uploadTimeout);
                }
                else
                {
                    Log.LogError($"Item '{item}' already exists in {relativeBlobPath}.");
                }
            }
            catch (Exception exc)
            {
                Log.LogError($"Unable to upload to {relativeBlobPath} due to {exc}.");
                throw;
            }
            finally
            {
                clientThrottle.Release();
            }
        }

        public async Task CreateContainerAsync(IBuildEngine buildEngine, bool publishFlatContainer)
        {
            Log.LogMessage($"Creating container {feed.ContainerName}...");

            CreateAzureContainer createContainer = new CreateAzureContainer
            {
                AccountKey = feed.AccountKey,
                AccountName = feed.AccountName,
                ContainerName = feed.ContainerName,
                FailIfExists = false,
                IsPublic = !hasToken,
                BuildEngine = buildEngine
            };

            await createContainer.ExecuteAsync();

            Log.LogMessage($"Creating container {feed.ContainerName} succeeded!");

            if (!publishFlatContainer)
            {
                try
                {
                    bool result = await InitAsync();

                    if (result)
                    {
                        Log.LogMessage($"Initializing sub-feed {source.FeedSubPath} succeeded!");
                    }
                    else
                    {
                        throw new Exception($"Initializing sub-feed {source.FeedSubPath} failed!");
                    }
                }
                catch (Exception e)
                {
                    Log.LogErrorFromException(e);
                }
            }
        }

        public async Task<ISet<PackageIdentity>> GetPackageIdentitiesAsync()
        {
            var context = new SleetContext
            {
                LocalSettings = GetSettings(),
                Log = new SleetLogger(Log),
                Source = GetAzureFileSystem(),
                Token = CancellationToken
            };
            context.SourceSettings = await FeedSettingsUtility.GetSettingsOrDefault(
                context.Source,
                context.Log,
                context.Token);

            var packageIndex = new PackageIndex(context);

            return await packageIndex.GetPackagesAsync();
        }

        public ISleetFileSystemLock CreateLock()
        {
            return GetAzureFileSystem().CreateLock(new SleetLogger(Log));
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
            bool result = await PushCommand.RunAsync(settings, fileSystem, items.ToList(), allowOverwrite, skipExisting: false, log: new SleetLogger(Log));
            return result;
        }

        private async Task<bool> InitAsync()
        {

            LocalSettings settings = GetSettings();
            AzureFileSystem fileSystem = GetAzureFileSystem();
            bool result = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, enableSymbols: false, log: new SleetLogger(Log), token: CancellationToken);
            return result;
        }
    }
}
