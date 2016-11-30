// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Compose.Model;
using Microsoft.DotNet.VersionTools.Compose.Model.Command;
using Microsoft.DotNet.VersionTools.Util;

namespace Microsoft.DotNet.VersionTools.Compose
{
    public class ConsumesUpdater
    {
        public string TargetDependencyGroupName { get; set; } = "floating";

        /// <summary>
        /// Updates a Consumes command output to consume the build described by a Produces output.
        /// </summary>
        public void Upgrade(ConsumesOutput consumes, ProducesOutput produces)
        {
            NuGetArtifactSet targetSet = consumes.OsAll.Dependencies[TargetDependencyGroupName].NuGet;
            NuGetArtifactSet sourceSet = produces.OsAll.NuGet;

            foreach (var package in sourceSet.Packages.EmptyIfNull())
            {
                targetSet.Packages[package.Key] = package.Value;
            }
            foreach (var releaseLabel in sourceSet.ReleaseLabels.EmptyIfNull())
            {
                targetSet.ReleaseLabels[releaseLabel.Key] = releaseLabel.Value;
            }
        }
    }
}
