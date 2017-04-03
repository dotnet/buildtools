// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Util;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.VersionTools.Dependencies
{
    public class MSBuildPackageReferenceUpdater : IDependencyUpdater
    {
        public IEnumerable<string> MSBuildFilePaths { get; }

        public MSBuildPackageReferenceUpdater(IEnumerable<string> msbuildFilePaths)
        {
            MSBuildFilePaths = msbuildFilePaths;
        }

        public IEnumerable<DependencyUpdateTask> GetUpdateTasks(IEnumerable<DependencyBuildInfo> dependencyBuildInfos)
        {
            var tasks = new List<DependencyUpdateTask>();
            foreach (string filePath in MSBuildFilePaths)
            {
                try
                {
                    IEnumerable<DependencyChange> dependencyChanges = null;

                    Action update = FileUtils.GetUpdateFileContentsTask(
                        filePath,
                        contents => ReplaceAllDependencyVersions(
                            contents,
                            filePath,
                            dependencyBuildInfos,
                            out dependencyChanges));

                    // The output json may be different even if there weren't any changes made.
                    if (update != null && dependencyChanges.Any())
                    {
                        tasks.Add(new DependencyUpdateTask(
                            update,
                            dependencyChanges.Select(change => change.BuildInfo),
                            dependencyChanges.Select(change => $"In '{filePath}', {change.ToString()}")));
                    }
                }
                catch (Exception e)
                {
                    Trace.TraceWarning($"Non-fatal exception occurred processing '{filePath}'. Skipping file. Exception: {e}. ");
                }
            }
            return tasks;
        }

        private string ReplaceAllDependencyVersions(
            string input,
            string projectJsonFile,
            IEnumerable<DependencyBuildInfo> buildInfos,
            out IEnumerable<DependencyChange> dependencyChanges)
        {
            XDocument documentRoot = XDocument.Parse(input);

            dependencyChanges = FindAllDependencyProperties(documentRoot)
                    .Select(dependencyProperty => ReplaceDependencyVersion(projectJsonFile, dependencyProperty, buildInfos))
                    .Where(buildInfo => buildInfo != null)
                    .ToArray();

            StringWriter sw = new StringWriter();
            documentRoot.Save(sw, SaveOptions.None);
            return sw.ToString();
        }

        /// <summary>
        /// Replaces the single dependency with the updated version, if it matches any of the
        /// dependencies that need to be updated. Stops on the first updated value found.
        /// </summary>
        /// <returns>The BuildInfo used to change the value, or null if there was no change.</returns>
        private DependencyChange ReplaceDependencyVersion(
            string projectJsonFile,
            XElement packageRefElement,
            IEnumerable<DependencyBuildInfo> parsedBuildInfos)
        {
            string id = packageRefElement.Attribute("Include").Value;
            Debug.Assert(id != null);
            foreach (DependencyBuildInfo info in parsedBuildInfos)
            {
                foreach (PackageIdentity packageInfo in info.Packages)
                {
                    if (id != packageInfo.Id)
                    {
                        continue;
                    }

                    string oldVersion;
                    // Support the new  "inline" Version attribute, or the old-style nested Version element.
                    XAttribute versionAttribute = null;
                    XElement versionElement = null;

                    versionAttribute = packageRefElement.Attribute(XName.Get("Version", packageRefElement.GetDefaultNamespace().NamespaceName));
                    if (versionAttribute == null)
                    {
                        versionElement = packageRefElement.Element(XName.Get("Version", packageRefElement.GetDefaultNamespace().NamespaceName));
                    }

                    if (versionAttribute != null)
                    {
                        oldVersion = versionAttribute.Value;
                    }
                    else
                    {
                        if (versionElement == null)
                        {
                            Trace.TraceWarning($"Couldn't parse the version information for package '{id}' in '{projectJsonFile}'. Skipping.");
                            continue;
                        }

                        oldVersion = versionElement.Value;
                    }

                    VersionRange parsedOldVersionRange;
                    if (!VersionRange.TryParse(oldVersion, out parsedOldVersionRange))
                    {
                        Trace.TraceWarning($"Couldn't parse '{oldVersion}' for package '{id}' in '{projectJsonFile}'. Skipping.");
                        continue;
                    }
                    NuGetVersion oldNuGetVersion = parsedOldVersionRange.MinVersion;

                    if (oldNuGetVersion == packageInfo.Version)
                    {
                        // Versions match, no update to make.
                        continue;
                    }

                    if (oldNuGetVersion.IsPrerelease || info.UpgradeStableVersions)
                    {
                        string newVersion = packageInfo.Version.ToNormalizedString();
                        if (versionAttribute != null)
                        {
                            versionAttribute.Value = newVersion;
                        }
                        else
                        {
                            Debug.Assert(versionElement != null);
                            versionElement.Value = newVersion;
                        }

                        return new DependencyChange
                        {
                            BuildInfo = info.BuildInfo,
                            PackageId = id,
                            Before = oldNuGetVersion,
                            After = packageInfo.Version
                        };
                    }
                }
            }
            return null;
        }

        private static IEnumerable<XElement> FindAllDependencyProperties(XDocument projectFileRoot)
        {
            return projectFileRoot
                .Descendants()
                .OfType<XElement>()
                .Where(property => property.Name.LocalName == "PackageReference");
        }

        private class DependencyChange
        {
            public BuildInfo BuildInfo { get; set; }
            public string PackageId { get; set; }
            public NuGetVersion Before { get; set; }
            public NuGetVersion After { get; set; }

            public override string ToString()
            {
                return $"'{PackageId} {Before.ToNormalizedString()}' must be " +
                    $"'{After.ToNormalizedString()}' ({BuildInfo.Name})";
            }
        }
    }
}
