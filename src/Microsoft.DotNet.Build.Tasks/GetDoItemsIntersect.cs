// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GetDoItemsIntersect : BuildTask
    {
        [Required]
        public ITaskItem[] ItemGroup1 { get; set; }

        [Required]
        public ITaskItem[] ItemGroup2 { get; set; }

        [Output]
        public bool Result { get; set; }

        public override bool Execute()
        {
            Result = ItemGroup1.Select(ig => ig.ItemSpec).Intersect(ItemGroup2.Select(ig => ig.ItemSpec)).Any();

            Log.LogMessage("GetDoItemsIntersect completed successfully - returned {0}", Result.ToString());

            return true;
        }
    }
}
