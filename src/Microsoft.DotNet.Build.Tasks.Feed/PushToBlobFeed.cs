// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using MSBuild = Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.DotNet.Build.Tasks.Feed
{

    public class PushToBlobFeed : MSBuild.Task
    {
        [Required]
        public string AccountName { get; set; }

        [Required]
        public string AccountKey { get; set; }

        [Required]
        public string ContainerName { get; set; }

        [Required]
        public ITaskItem[] ItemsToPush { get; set; }

        [Required]
        public string RelativePath { get; set; }

        public string IndexDirectory { get; set; }

        public bool PublishFlatContainer { get; set; }

        public bool Overwrite { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            Debugger.Launch();
            try
            {
                Log.LogMessage(MessageImportance.High, "Performing feed push...");
                if (ItemsToPush == null)
                {
                    Log.LogError($"No items to push. Please check ItemGroup ItemsToPush.");
                }
                if (string.IsNullOrEmpty(RelativePath))
                {
                    Log.LogWarning($"No relative path. Items are pushed to root of container.");
                }
                BlobFeedAction blobFeedAction = new BlobFeedAction(AccountName, AccountKey, ContainerName, IndexDirectory, Log);
                bool containerExists = await blobFeedAction.feed.CheckIfFeedExists();
                if (!containerExists)
                {
                    await blobFeedAction.feed.CreateFeedContainer(RelativePath);
                }
                if (!PublishFlatContainer)
                {
                    if (!containerExists)
                    {
                        await blobFeedAction.PushToFeed(ConvertToStringLists(ItemsToPush), RelativePath);
                    }
                    else
                    {
                        await blobFeedAction.PushToFeed(ConvertToStringLists(ItemsToPush), RelativePath, Overwrite);
                    }
                }
                else
                {
                    await blobFeedAction.PushToFeedFlat(ConvertToStringLists(ItemsToPush), RelativePath, Overwrite);
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
