// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.VersionTools;
using Microsoft.DotNet.VersionTools.Compose.Model;
using Microsoft.DotNet.VersionTools.Compose.Model.Command;
using Microsoft.DotNet.VersionTools.Dependencies;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Compose
{
    public class UpdateConsumes : Task
    {
        [Required]
        public string ConsumesJsonPath { get; set; }

        [Required]
        public string ConsumesRuntimeName { get; set; }

        [Required]
        public string NewDependencyType { get; set; }

        /// <summary>
        /// Puts prerelease package dependencies in a different group. If not specified, all new
        /// dependencies are put into NewDependencyType.
        /// </summary>
        public string NewPrereleaseDependencyType { get; set; }

        [Required]
        public ITaskItem[] ProjectJsonFiles { get; set; }

        [Required]
        public ITaskItem[] DependencyBuildInfo { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(NewPrereleaseDependencyType))
            {
                NewPrereleaseDependencyType = NewDependencyType;
            }

            if (ProjectJsonFiles != null && ProjectJsonFiles.Any())
            {
                ConsumesOutput consumes = ConsumesHelpers.ReadConsumesFile(ConsumesJsonPath);

                ProjectJsonUpdater jsonUpdater = new ProjectJsonUpdater(
                    ProjectJsonFiles.Select(item => item.ItemSpec))
                {
                    AllowOnlySpecifiedPackages = true
                };

                DependencyBuildInfo[] dependencyBuildInfos = DependencyBuildInfo
                    .Select(item => BaseDependenciesTask.CreateBuildInfoDependency(item, null))
                    .ToArray();

                IEnumerable<PackageDependencyUpdateTask> updateTasks = jsonUpdater
                    .GetUpdateTasks(dependencyBuildInfos)
                    .OfType<PackageDependencyUpdateTask>();

                IDictionary<string, ArtifactSet> dependencies = consumes[ConsumesRuntimeName].Dependencies;

                NuGetArtifactSet stableNuget = dependencies[NewDependencyType].NuGet;
                NuGetArtifactSet prereleaseNuget = dependencies[NewPrereleaseDependencyType].NuGet;

                foreach (PackageDependencyUpdateTask updateTask in updateTasks)
                {
                    foreach (PackageDependencyChange change in updateTask.Changes)
                    {
                        NuGetArtifactSet nuget = change.Before.IsPrerelease
                            ? prereleaseNuget
                            : stableNuget;

                        PackageVersionList versions;
                        if (!nuget.Packages.TryGetValue(change.PackageId, out versions))
                        {
                            versions = new PackageVersionList();
                            nuget.Packages[change.PackageId] = versions;
                        }

                        string existingVersion = change.Before.ToNormalizedString();
                        if (!versions.Contains(existingVersion))
                        {
                            Log.LogMessage($"Adding dependency '{change.PackageId} {existingVersion}'");
                            versions.Add(existingVersion);
                        }
                    }
                }

                ConsumesHelpers.WriteConsumesFile(ConsumesJsonPath, consumes);
            }
            else
            {
                Log.LogWarning("No project.json files specified.");
            }
            return !Log.HasLoggedErrors;
        }
    }
}
