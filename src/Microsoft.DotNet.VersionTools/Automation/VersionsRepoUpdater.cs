﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class VersionsRepoUpdater
    {
        private const int MaxTries = 10;
        private const int RetryMillisecondsDelay = 5000;

        private GitHubAuth _gitHubAuth;
        private GitHubProject _project;

        public VersionsRepoUpdater(
            GitHubAuth gitHubAuth,
            string versionsRepoOwner = null,
            string versionsRepo = null)
            : this(
                gitHubAuth,
                new GitHubProject(versionsRepo ?? "versions", versionsRepoOwner))
        {
        }

        public VersionsRepoUpdater(GitHubAuth gitHubAuth, GitHubProject project)
        {
            if (gitHubAuth == null)
            {
                throw new ArgumentNullException(nameof(gitHubAuth));
            }
            _gitHubAuth = gitHubAuth;

            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }
            _project = project;
        }

        /// <param name="updateLatestVersion">If true, updates Latest.txt with a prerelease moniker. If there isn't one, makes the file empty.</param>
        /// <param name="updateLatestPackageList">If true, updates Latest_Packages.txt.</param>
        /// <param name="updateLastBuildPackageList">If true, updates Last_Build_Packages.txt, and enables keeping old packages in Latest_Packages.txt.</param>
        public async Task UpdateBuildInfoAsync(
            IEnumerable<string> packagePaths,
            string versionsRepoPath,
            bool updateLatestPackageList = true,
            bool updateLatestVersion = true,
            bool updateLastBuildPackageList = true)
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

            Dictionary<string, string> packageDictionary = packages.ToDictionary(
                    t => t.Id,
                    t => t.Version);

            using (GitHubClient client = new GitHubClient(_gitHubAuth))
            {
                for (int i = 0; i < MaxTries; i++)
                {
                    try
                    {
                        // Master commit to use as new commit's parent.
                        string masterRef = "heads/master";
                        GitReference currentMaster = await client.GetReferenceAsync(_project, masterRef);
                        string masterSha = currentMaster.Object.Sha;

                        List<GitObject> objects = new List<GitObject>();

                        if (updateLastBuildPackageList)
                        {
                            objects.Add(new GitObject
                            {
                                Path = $"{versionsRepoPath}/Last_Build_Packages.txt",
                                Type = GitObject.TypeBlob,
                                Mode = GitObject.ModeFile,
                                Content = CreatePackageListFile(packageDictionary)
                            });
                        }

                        if (updateLatestPackageList)
                        {
                            string latestPackagesPath = $"{versionsRepoPath}/Latest_Packages.txt";

                            var allPackages = new Dictionary<string, string>(packageDictionary);

                            if (updateLastBuildPackageList)
                            {
                                Dictionary<string, string> existingPackages = await GetPackagesAsync(client, latestPackagesPath);

                                // Add each existing package if there isn't a new package with the same id.
                                foreach (var package in existingPackages)
                                {
                                    if (!allPackages.ContainsKey(package.Key))
                                    {
                                        allPackages[package.Key] = package.Value;
                                    }
                                }
                            }

                            objects.Add(new GitObject
                            {
                                Path = latestPackagesPath,
                                Type = GitObject.TypeBlob,
                                Mode = GitObject.ModeFile,
                                Content = CreatePackageListFile(allPackages)
                            });
                        }

                        if (updateLatestVersion)
                        {
                            objects.Add(new GitObject
                            {
                                Path = $"{versionsRepoPath}/Latest.txt",
                                Type = GitObject.TypeBlob,
                                Mode = GitObject.ModeFile,
                                Content = prereleaseVersion
                            });
                        }

                        string message = $"Updating {versionsRepoPath}";
                        if (string.IsNullOrEmpty(prereleaseVersion))
                        {
                            message += ". No prerelease versions published.";
                        }
                        else
                        {
                            message += $" for {prereleaseVersion}";
                        }

                        GitTree tree = await client.PostTreeAsync(_project, masterSha, objects.ToArray());
                        GitCommit commit = await client.PostCommitAsync(_project, message, tree.Sha, new[] { masterSha });

                        // Only fast-forward. Don't overwrite other changes: throw exception instead.
                        await client.PatchReferenceAsync(_project, masterRef, commit.Sha, force: false);

                        Trace.TraceInformation($"Committed build-info update on attempt {i + 1}.");
                        break;
                    }
                    catch (HttpRequestException ex)
                    {
                        int nextTry = i + 1;
                        if (nextTry < MaxTries)
                        {
                            Trace.TraceInformation($"Encountered exception committing build-info update: {ex.Message}");
                            Trace.TraceInformation($"Trying again in {RetryMillisecondsDelay}ms. {MaxTries - nextTry} tries left.");
                            await Task.Delay(RetryMillisecondsDelay);
                        }
                        else
                        {
                            Trace.TraceInformation("Encountered exception committing build-info update.");
                            throw;
                        }
                    }
                }
            }
        }

        private async Task<Dictionary<string, string>> GetPackagesAsync(GitHubClient client, string path)
        {
            string latestPackages = await client.GetGitHubFileContentsAsync(
                path,
                new GitHubBranch("master", _project));

            using (var reader = new StringReader(latestPackages))
            {
                return await BuildInfo.ReadPackageListAsync(reader);
            }
        }

        private static string CreatePackageListFile(Dictionary<string, string> packages)
        {
            return string.Join(
                   Environment.NewLine,
                   packages
                       .OrderBy(t => t.Key)
                       .Select(t => $"{t.Key} {t.Value}"));
        }
    }
}
