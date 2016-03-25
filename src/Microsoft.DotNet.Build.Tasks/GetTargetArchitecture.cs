// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GetTargetArchitecture : Task
    {
        [Output]
        public string TargetArch { get; set; }

        public override bool Execute()
        {
            if (RuntimeInformation.OSArchitecture == Architecture.X64)
                TargetArch = "x64";
            else if (RuntimeInformation.OSArchitecture == Architecture.X86)
                TargetArch = "x86";
            else if (RuntimeInformation.OSArchitecture == Architecture.Arm)
                TargetArch = "arm";
            else if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                TargetArch = "arm64";
            
            return true;
        }
    }
}
