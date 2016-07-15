// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Microsoft.DotNet.VersionTools.Automation
{
    class NupkgNameInfo
    {
        public NupkgNameInfo(string path)
        {
            using (PackageArchiveReader archiveReader = new PackageArchiveReader(path))
            {
                PackageIdentity identity = archiveReader.GetIdentity();
                Id = identity.Id;
                Version = identity.Version.ToString();
                Prerelease = identity.Version.Release;
            }
            SymbolPackage = path.EndsWith(".symbols.nupkg");
        }

        public string Id { get; set; }
        public string Version { get; set; }
        public string Prerelease { get; set; }
        public bool SymbolPackage { get; set; }
    }
}
