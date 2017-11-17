// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
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
                    BlobFeedAction blobFeedAction = new BlobFeedAction(ExpectedFeedUrl, AccountKey, Log);

                    if (!SkipCreateContainer)
                    {
                        await blobFeedAction.CreateContainerAsync(BuildEngine, PublishFlatContainer);
                    }

                    if (PublishFlatContainer)
                    {
                        await PublishToFlatContainerAsync(ItemsToPush, blobFeedAction);
                    }
                    else
                    {
                        ITaskItem[] symbolItems = ItemsToPush.Where(i => i.ItemSpec.Contains("symbols.nupkg")).Select(i => 
                        {
                            string fileName = Path.GetFileName(i.ItemSpec);
                            i.SetMetadata("RelativeBlobPath", $"symbols/{fileName}");
                            return i;
                        }).ToArray();
                        ITaskItem[] packages = ItemsToPush.Where(i => !symbolItems.Contains(i)).ToArray();
                        List<string> packageItems = GetPackageStringLists(packages);

                        await blobFeedAction.PushToFeed(packageItems, Overwrite);
                        await PublishToFlatContainerAsync(symbolItems, blobFeedAction, true);
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }

        private List<string> GetPackageStringLists(ITaskItem[] taskItems)
        {
            List<string> stringList = new List<string>();
            foreach (var item in taskItems)
            {
                stringList.Add(item.ItemSpec);
            }

            return stringList;
        }

        private async Task PublishToFlatContainerAsync(ITaskItem[] taskItems, BlobFeedAction blobFeedAction, bool symbolItems = false)
        {
            if (taskItems.Length > 0)
            {
                using (var clientThrottle = new SemaphoreSlim(this.MaxClients, this.MaxClients))
                {
                    Log.LogMessage($"Uploading {taskItems.Length} items...");
                    await Task.WhenAll(taskItems.Select(item => blobFeedAction.UploadAssets(item, clientThrottle, Overwrite, symbolItems)));
                }
            }
        }
    }
}
