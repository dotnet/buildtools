// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Dependencies
{
    public class FileRegexReleaseUpdater : FileRegexUpdater
    {
        public string BuildInfoName { get; set; }

        protected override string TryGetDesiredValue(
            IEnumerable<DependencyBuildInfo> dependencyBuildInfos,
            out IEnumerable<BuildInfo> usedBuildInfos)
        {
            BuildInfo project = dependencyBuildInfos
                .Select(d => d.BuildInfo)
                .SingleOrDefault(d => d.Name == BuildInfoName);

            if (project == null)
            {
                usedBuildInfos = Enumerable.Empty<BuildInfo>();

                Trace.TraceError($"Could not find build info for project named {BuildInfoName}");
                return $"PROJECT '{BuildInfoName}' NOT FOUND";
            }

            usedBuildInfos = new[] { project };

            return project.LatestReleaseVersion;
        }
    }
}