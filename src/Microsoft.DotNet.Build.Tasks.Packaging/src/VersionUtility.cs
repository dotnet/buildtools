// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

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


        public static Version GetAssemblyVersion(string assemblyPath)
        {
            Version result = null;
            try
            {
                using (PEReader peReader = new PEReader(new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read)))
                {
                    if (peReader.HasMetadata)
                    {
                        MetadataReader reader = peReader.GetMetadataReader();
                        result = reader.GetAssemblyDefinition().Version;
                    }
                }
            }
            catch (BadImageFormatException)
            {
                // not a PE
            }

            return result;
        }
    }
}
