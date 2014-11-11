// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public class NormalizePaths : Task
    {
        [Required]
        public ITaskItem[] InputPaths { get; set; }

        [Output]
        public ITaskItem[] OutputPaths { get; set; }

        public override bool Execute()
        {
            OutputPaths = InputPaths.Select(
                iti => new TaskItem(Path.GetFullPath(iti.ItemSpec))).ToArray();

            return true;
        }
    }
}
