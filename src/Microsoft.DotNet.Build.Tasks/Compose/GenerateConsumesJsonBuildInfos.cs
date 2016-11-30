// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.VersionTools;
using Microsoft.DotNet.VersionTools.Compose.Model;
using Microsoft.DotNet.VersionTools.Compose.Model.Command;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Compose
{
    /// <summary>
    /// Generates DependencyBuildInfo items from a consumes.json file. These can be fed into
    /// existing dependency verification/update tooling.
    /// 
    /// One DependencyBuildInfo is created per package because there is no reasonable way to put
    /// multiple packages into one item. Downloading from the versions repo (the old method) was
    /// able to store multiple by putting a single url in the item, not a list of packages.
    /// </summary>
    public class GenerateConsumesJsonBuildInfos : Task
    {
        [Required]
        public string ConsumesJsonPath { get; set; }

        [Required]
        public string ConsumesRuntimeName { get; set; }

        [Output]
        public ITaskItem[] DependencyBuildInfo { get; set; }

        public override bool Execute()
        {
            ConsumesOutput consumes = ConsumesHelpers.ReadConsumesFile(ConsumesJsonPath);

            IEnumerable<NuGetArtifactSet> nugetSets = consumes[ConsumesRuntimeName]
                .Dependencies.Values
                .Select(set => set.NuGet)
                .ToArray();

            var dependencyBuildInfos = new List<ITaskItem>();

            foreach (var set in nugetSets)
            {
                if (set.Packages != null)
                {
                    // For every package consumed, make a build info for each version consumed.
                    dependencyBuildInfos.AddRange(set.Packages.SelectMany(CreateBuildInfoItems));
                }
                if (set.ReleaseLabels != null)
                {
                    // For each prerelease specifier, make a build info for upgraders to find.
                    dependencyBuildInfos.AddRange(set.ReleaseLabels.Select(CreateReleaseSpecBuildInfoItem));
                }
            }

            DependencyBuildInfo = dependencyBuildInfos.ToArray();

            return true;
        }

        private static IEnumerable<ITaskItem> CreateBuildInfoItems(
            KeyValuePair<string, PackageVersionList> dependency)
        {
            return dependency.Value.Select(version =>
               new TaskItem(dependency.Key, new Dictionary<string, string>
               {
                   [BaseDependenciesTask.PackageIdMetadataName] = dependency.Key,
                   [BaseDependenciesTask.VersionMetadataName] = version,
                   [BaseDependenciesTask.UpdateStableVersionsMetadataName] = "true"
               }));
        }

        private static ITaskItem CreateReleaseSpecBuildInfoItem(
            KeyValuePair<string, string> releaseLabel)
        {
            return new TaskItem(releaseLabel.Key, new Dictionary<string, string>
            {
                [BaseDependenciesTask.LatestReleaseVersionMetadataName] = releaseLabel.Value
            });
        }
    }
}
