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

        public bool SkipStableVersions { get; set; } = true;

        public ProjectJsonUpdater(IEnumerable<string> projectJsonPaths)
        {
            ProjectJsonPaths = projectJsonPaths;
        }

        public IEnumerable<BuildInfo> Update(IEnumerable<BuildInfo> buildInfos)
        {
            foreach (string projectJsonFile in ProjectJsonPaths)
            {
                IEnumerable<BuildInfo> buildInfosUsed = null;

                try
                {
                    FileUtils.ReplaceFileContents(projectJsonFile, contents => ReplaceAllDependencyVersions(contents, projectJsonFile, buildInfos, out buildInfosUsed));
                }
                catch (Exception e)
                {
                    Trace.TraceWarning($"Non-fatal exception occurred reading or writing '{projectJsonFile}'. Skipping file. Exception: {e}. ");
                    continue;
                }

                if (buildInfosUsed != null)
                {
                    foreach (var buildInfo in buildInfosUsed)
                    {
                        yield return buildInfo;
                    }
                }
            }
        }

        private string ReplaceAllDependencyVersions(string input, string projectJsonFile, IEnumerable<BuildInfo> buildInfos, out IEnumerable<BuildInfo> buildInfosUsed)
        {
            JObject projectRoot = JObject.Parse(input);

            buildInfosUsed = FindAllDependencyProperties(projectRoot)
                    .Select(dependencyProperty => ReplaceDependencyVersion(projectJsonFile, dependencyProperty, buildInfos))
                    .Where(buildInfo => buildInfo != null)
                    .ToArray();

            return JsonConvert.SerializeObject(projectRoot, Formatting.Indented) + Environment.NewLine;
        }

        /// <summary>
        /// Replaces the single dependency with the updated version, if it matches any of the
        /// dependencies that need to be updated. Stops on the first updated value found.
        /// </summary>
        /// <returns>The BuildInfo used to change the value, or null if there was no change.</returns>
        private BuildInfo ReplaceDependencyVersion(
            string projectJsonFile,
            JProperty dependencyProperty,
            IEnumerable<BuildInfo> buildInfos)
        {
            string id = dependencyProperty.Name;
            foreach (BuildInfo buildInfo in buildInfos)
            {
                foreach (PackageIdentity packageInfo in buildInfo.LatestPackages)
                {
                    if (id != packageInfo.Id)
                    {
                        continue;
                    }

                    string oldVersion;
                    if (dependencyProperty.Value is JObject)
                    {
                        oldVersion = (string)dependencyProperty.Value["version"];
                    }
                    else
                    {
                        oldVersion = (string)dependencyProperty.Value;
                    }
                    VersionRange parsedOldVersionRange;
                    if (!VersionRange.TryParse(oldVersion, out parsedOldVersionRange))
                    {
                        Trace.TraceWarning($"Couldn't parse '{oldVersion}' for package '{id}' in '{projectJsonFile}'. Skipping.");
                        continue;
                    }
                    NuGetVersion oldNuGetVersion = parsedOldVersionRange.MinVersion;

                    if (SkipStableVersions && !oldNuGetVersion.IsPrerelease)
                    {
                        continue;
                    }

                    if (oldNuGetVersion != packageInfo.Version)
                    {
                        string newVersion = packageInfo.Version.ToNormalizedString();
                        if (dependencyProperty.Value is JObject)
                        {
                            dependencyProperty.Value["version"] = newVersion;
                        }
                        else
                        {
                            dependencyProperty.Value = newVersion;
                        }

                        return buildInfo;
                    }
                }
            }
            return null;
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
    }
}
