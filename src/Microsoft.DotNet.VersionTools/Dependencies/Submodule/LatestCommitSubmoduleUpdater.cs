// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Dependencies.Submodule
{
    /// <summary>
    /// Updates the submodule at the given path to the latest commit available from its target
    /// repository.
    /// </summary>
    public class LatestCommitSubmoduleUpdater : SubmoduleUpdater, IDependencyInfoProvider
    {
        public string Repository { get; }

        public string Ref { get; }

        public LatestCommitSubmoduleUpdater(string repository, string @ref)
        {
            if (string.IsNullOrEmpty(repository))
            {
                throw new ArgumentException(
                    "A repository must be specified. For example, 'origin'. Got null or empty string.",
                    nameof(repository));
            }
            Repository = repository;

            if (string.IsNullOrEmpty(@ref))
            {
                throw new ArgumentException(
                    "A ref must be specified. For example, 'master'. Got null or empty string.",
                    nameof(@ref));
            }
            Ref = @ref;
        }

        /// <summary>
        /// Return a BuildInfo for this submodule updater. Generating one allows the caller to
        /// determine whether this updater should use the checked-in submodule or get the latest
        /// commit hash from the remote.
        /// </summary>
        public IEnumerable<IDependencyInfo> CreateDependencyInfos()
        {
            return new[]
            {
                new SubmoduleDependencyInfo
                {
                    Repository = Repository,
                    Ref = Ref,
                    Commit = GetCurrentIndexedHash()
                }
            };
        }

        protected override string GetDesiredCommitHash(
            IEnumerable<IDependencyInfo> dependencyInfos,
            out IEnumerable<IDependencyInfo> usedDependencyInfos)
        {
            // Find the build info that was created earlier, which the user may have modified.
            SubmoduleDependencyInfo[] matchingInfos = dependencyInfos
                .OfType<SubmoduleDependencyInfo>()
                .Where(info => info.Repository == Repository)
                .ToArray();

            if (matchingInfos.Length != 1)
            {
                string matchingInfoString = string.Join(", ", matchingInfos.AsEnumerable());

                throw new ArgumentException(
                    $"Expected exactly 1 {nameof(SubmoduleDependencyInfo)} for '{Repository}', " +
                    $"found {matchingInfos.Length}: '{matchingInfoString}'");
            }

            usedDependencyInfos = matchingInfos;

            SubmoduleDependencyInfo matchingInfo = matchingInfos[0];
            Trace.TraceInformation($"For {Path}, Found: {matchingInfo}");

            // If the info is unpinned, find the new commit.
            if (string.IsNullOrEmpty(matchingInfo.Commit))
            {
                // Unpinned: fetch commit to use from remote.
                string remoteRefOutput = FetchGitInPathOutput("ls-remote", "--heads", Repository, Ref);

                string[] remoteRefLines = remoteRefOutput
                    .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToArray();

                if (remoteRefLines.Length != 1)
                {
                    string allRefs = "";
                    if (remoteRefLines.Length > 1)
                    {
                        allRefs = $" ({string.Join(", ", remoteRefLines)})";
                    }

                    throw new NotSupportedException(
                        $"The configured Ref '{Ref}' for '{Path}' " +
                        $"must match exactly one ref on the remote, '{Repository}'. " +
                        $"Matched {remoteRefLines.Length}{allRefs}. ");
                }

                matchingInfo.Commit = remoteRefLines.Single().Split('\t').First();
            }

            return matchingInfo.Commit;
        }

        protected override void FetchRemoteBranch()
        {
            Trace.TraceInformation($"Fetching remote '{Repository}' for '{Path}'.");
            GitInPath("fetch", Repository)
                .Execute()
                .EnsureSuccessful();
        }
    }
}
