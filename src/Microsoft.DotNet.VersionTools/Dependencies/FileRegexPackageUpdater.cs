// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Dependencies
{
    public class FileRegexPackageUpdater : FileRegexUpdater
    {
        public string PackageId { get; set; }

        protected override string TryGetDesiredValue(
            IEnumerable<DependencyBuildInfo> dependencyBuildInfos,
            out IEnumerable<BuildInfo> usedBuildInfos)
        {
            var matchingBuildInfo = dependencyBuildInfos
                .Select(d => d.BuildInfo)
                .FirstOrDefault(d => d.LatestPackages.ContainsKey(PackageId));

            if (matchingBuildInfo == null)
            {
                usedBuildInfos = Enumerable.Empty<BuildInfo>();

                Trace.TraceError($"Could not find package version information for '{PackageId}'");
                return $"DEPENDENCY '{PackageId}' NOT FOUND";
            }

            usedBuildInfos = new[] { matchingBuildInfo };

            return matchingBuildInfo.LatestPackages[PackageId];
        }
    }
}
