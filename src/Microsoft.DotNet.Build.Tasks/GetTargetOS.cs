// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Runtime.InteropServices;
using System;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GetTargetOS : Task
    {
        [Output]
        public string TargetOS { get; set; }

        public override bool Execute()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                TargetOS = "Windows_NT";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                TargetOS = "Linux";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                TargetOS = "OSX";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")))
                TargetOS = "FreeBSD";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("NETBSD")))
                TargetOS = "NetBSD";

            return true;
        }
    }
}
