// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.CloudTestTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class PushToBlobFeed : MSBuild.Task
    {
        [Required]
        public string ExpectedFeedUrl { get; set; }

        [Required]
        public string AccountKey { get; set; }

        [Required]
        public ITaskItem[] ItemsToPush { get; set; }

        public bool Overwrite { get; set; }

        public bool PublishFlatContainer { get; set; }

        public int RetryAttempts { get; set; } = 5;

        public int RetryDelayInSeconds { get; set; } = 30;

        public int MaxClients { get; set; } = 8;

        public bool SkipCreateContainer { get; set; } = false;

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Performing feed push...");

                if (ItemsToPush == null)
                {
                    Log.LogError($"No items to push. Please check ItemGroup ItemsToPush.");
                }
                else
                {
                    BlobFeedAction blobFeedAction = new BlobFeedAction(ExpectedFeedUrl, AccountKey, Log, RetryAttempts, RetryDelayInSeconds);

                    if (!SkipCreateContainer)
                    {
                        await blobFeedAction.CreateContainerAsync(this.BuildEngine);
                    }

                    List<string> items = ConvertToStringLists(ItemsToPush);

                    if (!PublishFlatContainer)
                    {
                        await blobFeedAction.PushToFeed(items, Overwrite);
                    }
                    else
                    {
                        using (var clientThrottle = new SemaphoreSlim(this.MaxClients, this.MaxClients))
                        {
                            Log.LogMessage($"Uploading {ItemsToPush.Length} items...");
                            await Task.WhenAll(ItemsToPush.Select(item => blobFeedAction.UploadAssets(item, clientThrottle, Overwrite)));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }

        private List<string> ConvertToStringLists(ITaskItem[] taskItems)
        {
            List<string> stringList = new List<string>();
            foreach (var item in taskItems)
            {
                stringList.Add(item.ItemSpec);
            }

            return stringList;
        }
    }
}
