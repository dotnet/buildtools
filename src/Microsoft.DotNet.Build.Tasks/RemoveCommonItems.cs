// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Given a list of items, remove those which would overwrite items in the specified list
    /// of reference items.
    /// </summary>
    public class RemoveCommonItems : Task
    {
        /// <summary>
        /// The list of items from which to remove those that would overwrite ReferenceItems if copied
        /// to DestinationFolder.
        /// </summary>
        [Required]
        public ITaskItem[] SourceItems
        {
            get;
            set;
        }

        /// <summary>
        /// The folder to which the SourceItems would be copied.
        /// </summary>
        [Required]
        public string DestinationFolder
        {
            get;
            set;
        }

        /// <summary>
        /// The list of items to be removed from SourceItems if the source item would overwrite it.
        /// </summary>
        [Required]
        public ITaskItem[] ReferenceItems
        {
            get;
            set;
        }

        /// <summary>
        /// The list of source items with common items removed.
        /// </summary>
        [Output]
        public ITaskItem[] UniqueItems
        {
            get;
            set;
        }

        private string NormalizedDestinationFolder
        {
            get { return Path.GetFullPath(DestinationFolder).TrimEnd(Path.DirectorySeparatorChar); }
        }

        public override bool Execute()
        {
            List<ITaskItem> filteredList = new List<ITaskItem>(SourceItems.Length);

            foreach (ITaskItem item in SourceItems)
            {
                if (!IsItemInReferenceItems(item))
                {
                    filteredList.Add(item);
                }
            }

            UniqueItems = filteredList.ToArray();
            return true;
        }

        private bool IsItemInReferenceItems(ITaskItem sourceItem)
        {
            string destinationForItem = Path.Combine(DestinationFolder, Path.GetFileName(sourceItem.ItemSpec));

            foreach (ITaskItem item in ReferenceItems)
            {
                if (string.Equals(Path.GetFullPath(destinationForItem), Path.GetFullPath(item.ItemSpec), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
