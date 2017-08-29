// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Given a list of items, remove duplicate items. Metadata is not considered. Case insensitive.
    /// Unlike <see cref="RemoveDuplicates"/>, the last item in the list wins rather than the first.
    /// That distinction is important when the items have different metadata.
    /// </summary>
    public class RemoveDuplicatesWithLastOneWinsPolicy : BuildTask
    {
        /// <summary>
        /// The list of items from which to remove duplicates.
        /// </summary>
        public ITaskItem[] Inputs
        {
            get;
            set;
        }

        /// <summary>
        /// The list of items with duplicates removed.
        /// </summary>
        [Output]
        public ITaskItem[] Filtered
        {
            get;
            set;
        }

        public override bool Execute()
        {
            var existingIndexMap = new Dictionary<string, int>(Inputs.Length, StringComparer.OrdinalIgnoreCase);
            var filteredList = new List<ITaskItem>(Inputs.Length);

            foreach (ITaskItem item in Inputs)
            {
                int existingIndex;
                if (existingIndexMap.TryGetValue(item.ItemSpec, out existingIndex))
                {
                    filteredList[existingIndex] = item;
                }
                else
                {
                    filteredList.Add(item);
                    existingIndexMap.Add(item.ItemSpec, filteredList.Count - 1);
                }
            }

            Filtered = filteredList.ToArray();
            return true;
        }
    }
}


