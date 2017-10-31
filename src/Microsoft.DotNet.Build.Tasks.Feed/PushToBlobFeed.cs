// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using MSBuild = Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Collections.Generic;

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
                    await blobFeedAction.PushToFeed(ConvertToStringLists(ItemsToPush), Overwrite);
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
