// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Execute
{
    public struct OS
    {
        public const string OSX = "OSX";
        public const string FreeBSD = "FreeBSD";
        public const string NetBSD = "NetBSD";
        public const string Linux = "Linux";
        public const string Unix = "Unix";
        public const string Windows = "Windows_NT";
    }

    public struct Arch
    {
        public const string X86 = "x86";
        public const string X64 = "x64";
        public const string Arm = "arm";
        public const string Arm64 = "arm64";
    }
}
