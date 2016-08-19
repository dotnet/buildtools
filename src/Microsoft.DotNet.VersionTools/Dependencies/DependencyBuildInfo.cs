// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Packaging.Core;
using NuGet.Versioning;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Dependencies
{
    public class DependencyBuildInfo
    {
        public BuildInfo BuildInfo { get; }

        /// <summary>
        /// If true, upgraders will upgrade any version of this package to the one specified in the
        /// buildinfo, even if it's stable. Otherwise, prerelease versions are the only ones that
        /// are upgraded.
        /// </summary>
        public bool UpgradeStableVersions { get; }

        public IEnumerable<PackageIdentity> Packages { get; }

        public DependencyBuildInfo(BuildInfo buildInfo, bool upgradeStableVersions)
        {
            BuildInfo = buildInfo;
            UpgradeStableVersions = upgradeStableVersions;
            Packages = buildInfo.LatestPackages
                .Select(pair => new PackageIdentity(pair.Key, new NuGetVersion(pair.Value)))
                .ToArray();
        }
    }
}
