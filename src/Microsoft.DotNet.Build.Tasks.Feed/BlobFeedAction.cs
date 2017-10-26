// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using Sleet;
using System;
using System.Collections.Generic;
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

        public BlobFeed feed;
        public string feedUrl;

        const string feedRegex = @"(?<feedurl>https:\/\/(?<accountname>[^\.-]+)(?<domain>[^\/]*)\/((?<token>[a-zA-Z0-9+\/]*?\/\d{4}-\d{2}-\d{2})\/)?(?<containername>[^\/]+)\/(?<relativepath>.*\/))index\.json";

        public BlobFeedAction(string expectedFeedUrl, string accountKey, ITaskItem[] itemstoPush, MSBuild.TaskLoggingHelper Log)
        {
            this.Log = Log;
            Match m = Regex.Match(expectedFeedUrl, feedRegex);
            if (m.Success)
            {
                string accountName = m.Groups["accountname"].Value;
                string containerName = m.Groups["containername"].Value;
                string relativePath = m.Groups["relativepath"].Value;

                feed = new BlobFeed(accountName, accountKey, containerName, relativePath, Log);
                feedUrl = expectedFeedUrl;
            }
            else
            {
               throw new Exception("Unable to parse expected feed. Please check ExpectedFeedUrl.");
            }
        }

        public async Task<bool> PushToFeed(IEnumerable<string> items, bool allowOverwrite = false)
        {
            if (feed.IsSanityChecked(items))
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
                SleetSource source = new SleetSource
                {
                    Name = feed.ContainerName,
                    Type = "azure",
                    Path = feedUrl.Replace("index.json", string.Empty),
                    Container = feed.ContainerName,
                    FeedSubPath = feed.RelativePath,
                    ConnectionString = $"DefaultEndpointsProtocol=https;AccountName={feed.AccountName};AccountKey={feed.AccountKey};EndpointSuffix=core.windows.net"
                };

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

                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(source.ConnectionString);
                AzureFileSystem fileSystem = new AzureFileSystem(new LocalCache(), new Uri(source.Path), new Uri(source.Path), storageAccount, source.Name, source.FeedSubPath);

                // In case the first Push attempt fails with an InvalidOperationException we Init the feed and retry the Push command once.
                // Sleet internally retries 5 times on each package when the push operation fails so we don't need to retry ourselves.
                for (int i = 0; i <= 1; i++)
                {
                    try
                    {
                        
                        bool result = await PushCommand.RunAsync(settings, fileSystem, items.ToList(), true, true, new SleetLogger(Log));
                        return result;
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("init"))
                    {
                        Log.LogWarning($"Sub-feed {source.FeedSubPath} has not been initialized. Initializing now...");
                        bool result = await InitCommand.RunAsync(settings, fileSystem, new SleetLogger(Log));

                        if (result)
                        {
                            Log.LogMessage($"Initializing sub-feed {source.FeedSubPath} succeeded!");
                        }
                        else
                        {
                            Log.LogError($"Initializing sub-feed {source.FeedSubPath} failed!");
                            break;
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
    }
}
