// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
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
                    List<string> packageItems = ConvertToStringLists(ItemsToPush, true);
                    List<string> assetItems = ConvertToStringLists(ItemsToPush, false);

                    if (packageItems.Count > 0)
                    {
                        await blobFeedAction.PushToFeed(packageItems, Overwrite);
                    }

                    if (assetItems.Count > 0)
                    {
                        await blobFeedAction.UploadAssets(assetItems, Overwrite);
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }
            return !Log.HasLoggedErrors;
        }

        private List<string> ConvertToStringLists(ITaskItem[] taskItems, bool isNugetPackage)
        {
            List<string> stringList = new List<string>();
            foreach (var item in taskItems)
            {
                string fileSpec = item.ItemSpec;
                string extension = Path.GetExtension(fileSpec);

                // If packages is set to true it will only add files with ".nupkg" extension to the list (true == true)
                // else this will add non-nupkg files to the list (false == false)
                if ((extension == ".nupkg") == isNugetPackage)
                {
                    stringList.Add(item.ItemSpec);
                }
            }

            return stringList;
        }
    }
}
