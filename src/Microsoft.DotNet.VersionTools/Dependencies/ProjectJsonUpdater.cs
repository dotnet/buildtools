// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Dependencies
{
    public class ProjectJsonUpdater : IDependencyUpdater
    {
        public IEnumerable<string> ProjectJsonPaths { get; }

        /// <summary>
        /// When true, it is invalid to have a dependency with no entries in any buildinfo.
        /// </summary>
        public bool AllowOnlySpecifiedPackages { get; set; }

        public ProjectJsonUpdater(IEnumerable<string> projectJsonPaths)
        {
            ProjectJsonPaths = projectJsonPaths;
        }

        public IEnumerable<DependencyUpdateTask> GetUpdateTasks(IEnumerable<DependencyBuildInfo> dependencyBuildInfos)
        {
            var tasks = new List<DependencyUpdateTask>();
            foreach (string projectJsonFile in ProjectJsonPaths)
            {
                try
                {
                    PackageDependencyChange[] dependencyChanges = null;

                    Action update = FileUtils.GetUpdateFileContentsTask(
                        projectJsonFile,
                        contents => ReplaceAllDependencyVersions(
                            contents,
                            projectJsonFile,
                            dependencyBuildInfos,
                            out dependencyChanges));

                    if (dependencyChanges.Any())
                    {
                        tasks.Add(new PackageDependencyUpdateTask(
                            dependencyChanges,
                            projectJsonFile,
                            update));
                    }
                }
                catch (Exception e)
                {
                    Trace.TraceWarning($"Non-fatal exception occurred processing '{projectJsonFile}'. Skipping file. Exception: {e}. ");
                }
            }
            return tasks;
        }

        private string ReplaceAllDependencyVersions(
            string input,
            string projectJsonFile,
            IEnumerable<DependencyBuildInfo> buildInfos,
            out PackageDependencyChange[] dependencyChanges)
        {
            JObject projectRoot = JObject.Parse(input);

            dependencyChanges = FindAllDependencyProperties(projectRoot)
                    .Select(dependencyProperty => ReplaceDependencyVersion(projectJsonFile, dependencyProperty, buildInfos))
                    .Where(change => change != null)
                    .ToArray();

            return JsonConvert.SerializeObject(projectRoot, Formatting.Indented) + Environment.NewLine;
        }

        /// <summary>
        /// Replaces the single dependency with the updated version, if it matches any of the
        /// dependencies that need to be updated.
        /// </summary>
        /// <returns>Info about how the value was changed, or null if there was no change.</returns>
        private PackageDependencyChange ReplaceDependencyVersion(
            string projectJsonFile,
            JProperty dependencyProperty,
            IEnumerable<DependencyBuildInfo> parsedBuildInfos)
        {
            string id = dependencyProperty.Name;

            NuGetVersion version = ParsePackageVersion(dependencyProperty);
            if (version == null)
            {
                // It's ok to skip if the dependency target is to a project.
                if ((string)dependencyProperty.Value["target"] != "project")
                {
                    Trace.TraceWarning(
                        "Couldn't parse dependency version of package " +
                        $"'{id}' in '{projectJsonFile}' (skipping)");
                }
                return null;
            }

            string dependencyString = $"'{id} {version}' in '{projectJsonFile}'";

            MatchingPackage[] matchingPackageInfos = FindMatchingPackages(parsedBuildInfos, id).ToArray();

            if (matchingPackageInfos.Any(p => version == p.Package.Version))
            {
                // Version of package matches a specified one exactly: no update to make.
                return null;
            }

            // An update is needed. Allow updating to a stable version if enabled for the source buildinfo.
            var targetPackageInfo = matchingPackageInfos
                .Where(info => version.IsPrerelease || info.SourceBuildInfo.UpgradeStableVersions)
                .OrderByDescending(info => info.Package.Version.IsPrerelease)
                .ThenBy(info => info.Package.Version)
                .FirstOrDefault();

            if (targetPackageInfo == null)
            {
                if (AllowOnlySpecifiedPackages)
                {
                    // Package not specified and no upgrade found.
                    return new PackageDependencyChange(null, id, version, null);
                }
                return null;
            }

            SetPackageVersion(dependencyProperty, targetPackageInfo.Package.Version);
            return new PackageDependencyChange(
                targetPackageInfo.SourceBuildInfo.BuildInfo,
                id,
                version,
                targetPackageInfo.Package.Version);
        }

        private static NuGetVersion ParsePackageVersion(JProperty property)
        {
            string version;
            if (property.Value is JObject)
            {
                version = (string)property.Value["version"];
            }
            else
            {
                version = (string)property.Value;
            }

            VersionRange parsedVersionRange;
            if (version != null && VersionRange.TryParse(version, out parsedVersionRange))
            {
                return parsedVersionRange.MinVersion;
            }
            return null;
        }

        private static void SetPackageVersion(JProperty property, NuGetVersion version)
        {
            string newVersion = version.ToNormalizedString();
            if (property.Value is JObject)
            {
                property.Value["version"] = newVersion;
            }
            else
            {
                property.Value = newVersion;
            }
        }

        private static IEnumerable<MatchingPackage> FindMatchingPackages(
            IEnumerable<DependencyBuildInfo> buildInfos,
            string packageId)
        {
            return buildInfos.SelectMany(info => info.Packages
                .Where(p => p.Id == packageId)
                .Select(p => new MatchingPackage
                {
                    Package = p,
                    SourceBuildInfo = info
                }));
        }

        private static IEnumerable<JProperty> FindAllDependencyProperties(JObject projectJsonRoot)
        {
            return projectJsonRoot
                .Descendants()
                .OfType<JProperty>()
                .Where(property => property.Name == "dependencies")
                .Select(property => property.Value)
                .SelectMany(o => o.Children<JProperty>());
        }

        private class MatchingPackage
        {
            public PackageIdentity Package { get; set; }
            public DependencyBuildInfo SourceBuildInfo { get; set; }
        }
    }
}
