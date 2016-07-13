// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    // Filter the test archives for upload for CloudTest on Helix, based on the OSGroup and TestTFM
    // This will be used to generate the test list and decide the blobs uploaded to Azure
    public sealed class FilterForUpload : Task
    {
        /// <summary>
        ///     This is the list of test archives produced with OSPlatformConfig metadata set
        /// </summary>
        [Required]
        public ITaskItem[] UnfilteredUploadItems { get; set; }

        /// <summary>
        ///     This is the TestTFM folder that we want to select test zips from. This needs to be on the path to the test zip.
        /// </summary>
        [Required]
        public string FilterToTestTFM { get; set; }

        /// <summary>
        ///     This is the OSGroup folder that we want to select test zips from. This does not require an exact match as some
        ///     tests
        ///     may or may not have an exact OSGroup folder match.
        /// </summary>
        [Required]
        public string FilterToOSGroup { get; set; }

        /// <summary>
        ///     This is the bin folder that should be removed from all paths.
        /// </summary>
        [Required]
        public string BinDir { get; set; }

        [Output]
        public ITaskItem[] FilteredUploadItems { get; set; }

        public override bool Execute()
        {
            List<ITaskItem> filteredBasedOnTestTfm =
                UnfilteredUploadItems.Where(
                    item => this.MakeRelativePathTo(item.ItemSpec, BinDir).Contains(FilterToTestTFM)).ToList();

            List<ITaskItem> localFilteredUploadItemsList = new List<ITaskItem>();

            Dictionary<string, List<ITaskItem>> map = new Dictionary<string, List<ITaskItem>>();
            foreach (var item in filteredBasedOnTestTfm)
            {
                string testName = Path.GetFileName(item.ItemSpec);
                List<ITaskItem> values = null;
                if (!map.TryGetValue(testName, out values))
                {
                    values = new List<ITaskItem>();
                    map[testName] = values;
                }
                values.Add(item);
            }

            foreach (var item in map)
            {
                List<ITaskItem> values = item.Value;
                ITaskItem selectedItem = null;
                foreach (var value in values)
                {
                    if (value.GetMetadata("OSPlatformConfig").Contains(FilterToOSGroup))
                    {
                        selectedItem = value;
                        break;
                    }
                }
                // Fallback to first value
                if (selectedItem == null)
                {
                    selectedItem = values[0];
                }
                localFilteredUploadItemsList.Add(selectedItem);
            }

            FilteredUploadItems = localFilteredUploadItemsList.ToArray();
            return true;
        }


        private string MakeRelativePathTo(string targetPath, string basePath)
        {
            basePath = Path.GetFullPath(basePath);
            string fullPath1 = Path.GetFullPath(targetPath);
            if (fullPath1.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath1.Substring(basePath.Length + 1);
            }
            else
            {
                return targetPath;
            }
        }
    }
}
