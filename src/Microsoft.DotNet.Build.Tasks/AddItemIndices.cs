// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Takes Input, adds Index metadata with each item's location in the array, and outputs them.
    /// </summary>
    public class AddItemIndices : BuildTask
    {
        [Required]
        public ITaskItem[] Input { get; set; }

        [Output]
        public ITaskItem[] Output { get; set; }

        public override bool Execute()
        {
            Output = Input
                .Select((item, i) =>
                {
                    ITaskItem itemWithIndex = new TaskItem(item);
                    itemWithIndex.SetMetadata("Index", i.ToString());
                    return itemWithIndex;
                })
                .ToArray();

            return !Log.HasLoggedErrors;
        }
    }
}
