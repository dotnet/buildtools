// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    internal static class VersionUtility
    {
        public static bool IsCompatibleApiVersion(Version referenceVersion, Version definitionVersion)
        {
            return (referenceVersion.Major == definitionVersion.Major &&
                referenceVersion.Minor == definitionVersion.Minor &&
                referenceVersion.Build <= definitionVersion.Build &&
                referenceVersion.Revision <= definitionVersion.Revision);
        }
    }
}
