// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public class NormalizePaths : BuildTask
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
