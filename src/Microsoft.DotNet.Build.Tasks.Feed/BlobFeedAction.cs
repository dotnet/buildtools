// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using Sleet;
using System;
using System.Collections.Generic;
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
        
        const string feedRegex = @"(?<feedurl>https:\/\/(?<accountname>[^\.-]+)(?<domain>[^\/]*)\/((?<token>[a-zA-Z0-9+\/]*?\/\d{4}-\d{2}-\d{2})\/)?(?<containername>[^\/]+)\/(?<relativepath>.*)\/)index\.json";

        public BlobFeedAction(string accountName, string accountKey, string containerName, string packagesPath, MSBuild.TaskLoggingHelper Log)
        {
            this.Log = Log;
            this.feed = new BlobFeed(accountName, accountKey, containerName, packagesPath, Log);
        }

        public async Task<bool> PushToFeed(bool allowOverwrite = false)
        {
            if (feed.IsSanityChecked(feed.PackagesPath))
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    Log.LogError("Task PushToFeed cancelled");
                    CancellationToken.ThrowIfCancellationRequested();
                }

                await PushItemsToFeedAsync(feed.PackagesPath, allowOverwrite);
            }

            return !Log.HasLoggedErrors;
        }

        public async Task<bool> PushItemsToFeedAsync(string packagesPath, bool allowOverwrite)
        {
            Log.LogMessage(MessageImportance.Low, $"START pushing items to feed");

            try
            {
                Source source = new Source
                {
                    Name = feed.ContainerName,
                    Type = "azure",
                    Path = $"https://{feed.AccountName}.blob.core.windows.net/{feed.ContainerName}/",
                    Container = feed.ContainerName,
                    ConnectionString = $"DefaultEndpointsProtocol=https;AccountName={feed.AccountName};AccountKey={feed.AccountKey};EndpointSuffix=core.windows.net"
                };

                SleetSettings sleetSettings = new SleetSettings()
                {
                    Sources = new List<Source>
                    {
                       source 
                    }
                };

                LocalSettings settings = new LocalSettings
                {
                    Json = JObject.FromObject(sleetSettings)
                };

                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(source.ConnectionString);
                AzureFileSystem fileSystem = new AzureFileSystem(new LocalCache(), new Uri(source.Path), storageAccount, source.Name);
                bool result = await PushCommand.RunAsync(settings, fileSystem, new List<string> { packagesPath }, true, true, new SleetLogger(Log));

                return result;
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
