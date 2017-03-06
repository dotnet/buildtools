// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public sealed class RemoveItemMetadata : Task
    {
        /// <summary>
        /// Semicolon or comma-delineated list of metadata fields to strip from items.
        /// </summary>
        [Required]
        public string FieldsToRemove { get; set; }

        /// <summary>
        /// An item group to remove the specified metadata fields from.
        /// </summary>
        [Required]
        public ITaskItem[] Items { get; set; }

        /// <summary>
        /// Input items with metadata stripped out.
        /// </summary>
        [Output]
        public ITaskItem[] ProcessedItems { get; set; }

        public override bool Execute()
        {
            string [] fields = FieldsToRemove.Split(new char [] { ',', ';'});

            if (fields.Length == 0)
            {
                Log.LogWarning("No metadata field names specified, returning");
                return true;
            }
            ProcessedItems = new ITaskItem[Items.Length];
            for (int i = 0; i < Items.Length; i++)
            {
                ITaskItem current = Items[i];
                foreach (string fieldName in fields)
                {
                    if (current.GetMetadata(fieldName) != null)
                    {
                        current.RemoveMetadata(fieldName);
                    }
                }
                ProcessedItems[i] = current;
            }
            return true;
        }
    }
}
