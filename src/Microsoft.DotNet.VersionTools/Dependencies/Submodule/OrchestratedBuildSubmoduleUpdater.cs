// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using Microsoft.DotNet.VersionTools.Dependencies.BuildManifest;
using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Dependencies.Submodule
{
    public class OrchestratedBuildSubmoduleUpdater : SubmoduleUpdater
    {
        public string BuildName { get; set; }

        public string GitUrl { get; set; }

        protected override string GetDesiredCommitHash(
            IEnumerable<IDependencyInfo> dependencyInfos,
            out IEnumerable<IDependencyInfo> usedDependencyInfos)
        {
            DependencyInfoMatch[] matches = dependencyInfos
                .OfType<OrchestratedBuildDependencyInfo>()
                .SelectMany(info => info.OrchestratedBuildModel.Builds
                    .Where(b => b.Name.Equals(BuildName, StringComparison.OrdinalIgnoreCase))
                    .Select(b => new DependencyInfoMatch { Info = info, Match = b }))
                .ToArray();

            if (matches.Length != 1)
            {
                throw new ArgumentException(
                    $"For '{Path}', expected 1 build matching '{BuildName}', " +
                    $"but found {matches.Length}: '{string.Join(", ", matches.AsEnumerable())}'");
            }

            DependencyInfoMatch match = matches[0];

            if (string.IsNullOrEmpty(match.Match.Commit))
            {
                throw new ArgumentException(
                    $"For '{Path}', found match '{match}', but no commit on '{match.Match}'.");
            }

            usedDependencyInfos = new[] { match.Info };
            return match.Match.Commit;
        }

        protected override void FetchRemoteBranch()
        {
            Trace.TraceInformation($"In '{Path}', fetching from '{GitUrl}'");
            GitCommand.Fetch(Path, GitUrl);
        }

        private class DependencyInfoMatch
        {
            public OrchestratedBuildDependencyInfo Info { get; set; }
            public BuildIdentity Match { get; set; }

            public override string ToString() => $"'{Match}' from '{Info.SimpleName}'";
        }
    }
}
