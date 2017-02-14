// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class LocalVersionsRepoUpdater
    {
        /// <summary>
        /// Updates only the Latest_Packages file in the specified on-disk versions repository dir.
        /// </summary>
        public void UpdateBuildInfoLatestPackages(
            IEnumerable<string> packagePaths,
            string versionsRepoDir,
            string versionsRepoPath)
        {
            if (packagePaths == null)
            {
                throw new ArgumentNullException(nameof(packagePaths));
            }
            if (string.IsNullOrEmpty(versionsRepoDir))
            {
                throw new ArgumentException(nameof(versionsRepoDir));
            }
            if (string.IsNullOrEmpty(versionsRepoPath))
            {
                throw new ArgumentException(nameof(versionsRepoPath));
            }

            Dictionary<string, string> packages = packagePaths
                .Select(path => new NupkgNameInfo(path))
                // Ignore symbol packages.
                .Where(t => !t.SymbolPackage)
                .ToDictionary(t => t.Id, t => t.Version);

            string latestPackagesDir = Path.Combine(
                versionsRepoDir,
                versionsRepoPath);

            Directory.CreateDirectory(latestPackagesDir);

            File.WriteAllText(
                Path.Combine(latestPackagesDir, BuildInfo.LatestPackagesTxtFilename),
                VersionsRepoUpdater.CreatePackageListFile(packages));
        }
    }
}
