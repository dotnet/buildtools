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
            IEnumerable<BuildInfo> buildInfos,
            out IEnumerable<BuildInfo> usedBuildInfos)
        {
            var newVersion = buildInfos
                .SelectMany(d => d.LatestPackages.Select(p => new
                {
                    Package = p,
                    BuildInfo = d
                }))
                .FirstOrDefault(p => p.Package.Id == PackageId);

            if (newVersion == null)
            {
                usedBuildInfos = Enumerable.Empty<BuildInfo>();

                Trace.TraceError($"Could not find package version information for '{PackageId}'");
                return $"DEPENDENCY '{PackageId}' NOT FOUND";
            }

            usedBuildInfos = new[] { newVersion.BuildInfo };

            return newVersion.Package.Version.ToNormalizedString();
        }
    }
}
