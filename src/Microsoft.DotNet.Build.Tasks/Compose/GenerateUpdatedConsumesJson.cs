// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.VersionTools;
using Microsoft.DotNet.VersionTools;
using Microsoft.DotNet.VersionTools.Compose;
using Microsoft.DotNet.VersionTools.Compose.Model;
using Microsoft.DotNet.VersionTools.Compose.Model.Command;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Compose
{
    /// <summary>
    /// Take a consumes output JSON string, change it to match build info items downloaded from the
    /// versions repo, and output the changed JSON.
    /// </summary>
    public class GenerateUpdatedConsumesJson : Task
    {
        [Required]
        public string ConsumesJsonPath { get; set; }

        [Required]
        public ITaskItem[] RemoteDependencyBuildInfo { get; set; }

        /// <summary>
        /// If set, uses the "auto-update" ref defined by the item rather than the CurrentRef. If
        /// no auto-update ref is set, falls back to CurrentRef.
        /// </summary>
        public bool UseVersionsRepoAutoUpdateRef { get; set; }

        [Output]
        public string BuildInfoConsumesJsonContent { get; set; }

        public override bool Execute()
        {
            ProducesOutput[] producesOutputs = RemoteDependencyBuildInfo
                .Select(CreateBuildInfo)
                .Select(CreateProducesOutput)
                .ToArray();

            ConsumesOutput consumes = ConsumesHelpers.ReadConsumesFile(ConsumesJsonPath);

            var updater = new ConsumesUpdater
            {
                TargetDependencyGroupName = "build"
            };
            foreach (var produces in producesOutputs)
            {
                updater.Upgrade(consumes, produces);
            }

            BuildInfoConsumesJsonContent = ConsumesHelpers.Serialize(consumes);

            return !Log.HasLoggedErrors;
        }

        private BuildInfo CreateBuildInfo(ITaskItem item)
        {
            string currentRef = null;

            if (UseVersionsRepoAutoUpdateRef)
            {
                currentRef = item.GetMetadata(BaseDependenciesTask.VersionsRepoAutoUpdateRefMetadataName);
            }
            if (string.IsNullOrEmpty(currentRef))
            {
                currentRef = item.GetMetadata(BaseDependenciesTask.CurrentRefMetadataName);
            }

            if (string.IsNullOrEmpty(currentRef))
            {
                Log.LogError($"Item has no {BaseDependenciesTask.CurrentRefMetadataName} metadata: {item}");
            }

            return BuildInfo.Get(
                item.ItemSpec,
                BuildInfo.RawBuildInfoBaseUrl(
                    item.GetMetadata(BaseDependenciesTask.RawVersionsBaseUrlMetadataName),
                    currentRef,
                    item.GetMetadata(BaseDependenciesTask.BuildInfoPathMetadataName)));
        }

        /// <summary>
        /// Create a fake Produces output from the published build-info.
        /// </summary>
        private ProducesOutput CreateProducesOutput(BuildInfo info)
        {
            return new ProducesOutput
            {
                OsAll = new ArtifactSet
                {
                    NuGet = new NuGetArtifactSet
                    {
                        Packages = new SortedDictionary<string, PackageVersionList>(
                            info.LatestPackages.ToDictionary(
                                p => p.Key,
                                p => new PackageVersionList { p.Value })),

                        ReleaseLabels = new SortedDictionary<string, string>
                        {
                            [info.Name] = info.LatestReleaseVersion
                        }
                    }
                }
            };
        }
    }
}
