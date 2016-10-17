// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks.Compose
{
    public class WriteFileToStandardOutput : Task
    {
        [Required]
        public string Path { get; set; }

        public override bool Execute()
        {
            Console.Write(File.ReadAllText(Path));
            return true;
        }
    }
}
