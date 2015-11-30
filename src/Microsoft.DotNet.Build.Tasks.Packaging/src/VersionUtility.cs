// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
