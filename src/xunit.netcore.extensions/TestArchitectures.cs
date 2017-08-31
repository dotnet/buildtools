// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Xunit
{
    [Flags]
    public enum TestArchitectures
    {
        X86 = 1,
        X64 = 2,
        Arm = 4,
        Arm64 = 8,
        Any = ~0
    }
}
