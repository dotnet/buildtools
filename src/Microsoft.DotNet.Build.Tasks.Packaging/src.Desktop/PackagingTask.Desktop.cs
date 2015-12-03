// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Build.Common.Desktop;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public partial class PackagingTask
    {
        static PackagingTask()
        {
            AssemblyResolver.Enable();
        }
    }
}
