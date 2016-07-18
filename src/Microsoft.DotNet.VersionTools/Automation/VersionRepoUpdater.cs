// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class VersionRepoUpdater
    {
        private GitHubAuth _gitHubAuth;
        private string _versionsRepoOwner;
        private string _versionsRepo;

        public VersionRepoUpdater(
            GitHubAuth gitHubAuth,
            string versionRepoOwner = null,
            string versionsRepo = null)
        {
            if (gitHubAuth == null)
            {
                throw new ArgumentNullException(nameof(gitHubAuth));
            }
            _gitHubAuth = gitHubAuth;

            _versionsRepoOwner = versionRepoOwner ?? "dotnet";
            _versionsRepo = versionsRepo ?? "versions";
        }

        /// <param name="updateLatestVersion">If true, updates Latest.txt with a prerelease moniker. If there isn't one, makes the file empty.</param>
        /// <param name="updateLatestPackageList">If true, updates Latest_Packages.txt.</param>
        public async Task UpdateBuildInfoAsync(
            IEnumerable<string> packagePaths,
            string versionsRepoPath,
            bool updateLatestPackageList = true,
            bool updateLatestVersion = true)
        {
            if (packagePaths == null)
            {
                throw new ArgumentNullException(nameof(packagePaths));
            }
            if (versionsRepoPath == null)
            {
                throw new ArgumentNullException(nameof(versionsRepoPath));
            }

            NupkgNameInfo[] packages = packagePaths
                .Select(path => new NupkgNameInfo(path))
                // Ignore symbol packages.
                .Where(t => !t.SymbolPackage)
                .ToArray();

            string prereleaseVersion = packages
                .Select(t => t.Prerelease)
                .FirstOrDefault(prerelease => !string.IsNullOrEmpty(prerelease))
                ?? "";

            if (updateLatestPackageList)
            {
                string packageInfoFileContent = string.Join(
                    Environment.NewLine,
                    packages
                        .OrderBy(t => t.Id)
                        .Select(t => $"{t.Id} {t.Version}"));

                string packageInfoFilePath = $"{versionsRepoPath}/Latest_Packages.txt";
                string message = $"Updating Latest_Packages.txt at {versionsRepoPath} for {prereleaseVersion}";

                await UpdateGitHubFileAsync(packageInfoFilePath, packageInfoFileContent, message);
            }

            if (updateLatestVersion)
            {
                string latestFilePath = $"{versionsRepoPath}/Latest.txt";

                string message = $"Updating Latest.txt at {versionsRepoPath}";
                if (string.IsNullOrEmpty(prereleaseVersion))
                {
                    message += ". No prerelease versions published.";
                }
                else
                {
                    message += $" for {prereleaseVersion}";
                }

                await UpdateGitHubFileAsync(latestFilePath, prereleaseVersion, message);
            }
        }

        private async Task UpdateGitHubFileAsync(string path, string newFileContent, string commitMessage)
        {
            using (GitHubHttpClient client = new GitHubHttpClient(_gitHubAuth))
            {
                string fileUrl = $"https://api.github.com/repos/{_versionsRepoOwner}/{_versionsRepo}/contents/{path}";

                await client.PutGitHubFileAsync(fileUrl, commitMessage, newFileContent);
            }
        }
    }
}
