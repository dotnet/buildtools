// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class DependencyUpdateResults
    {
        public IEnumerable<BuildInfo> UsedBuildInfos { get; }

        public DependencyUpdateResults(IEnumerable<BuildInfo> usedBuildInfos)
        {
            UsedBuildInfos = usedBuildInfos;
        }

        public string GetSuggestedCommitMessage()
        {
            var orderedInfos = UsedBuildInfos.OrderBy(info => info.Name).ToArray();

            string updatedDependencyNames = string.Join(", ", orderedInfos.Select(d => d.Name));
            string updatedDependencyVersions = string.Join(", ", orderedInfos.Select(d => d.LatestReleaseVersion));

            string commitMessage = $"Update {updatedDependencyNames} to {updatedDependencyVersions}";
            if (UsedBuildInfos.Count() > 1)
            {
                commitMessage += ", respectively";
            }
            return commitMessage;
        }

        public bool ChangesDetected()
        {
            // Ensure changes were performed as expected.
            bool hasModifiedFiles = GitHasChanges();
            bool hasUsedBuildInfo = UsedBuildInfos.Any();
            if (hasModifiedFiles != hasUsedBuildInfo)
            {
                throw new Exception(
                    "'git status' does not match DependencyInfo information. " +
                    $"Git has modified files: {hasModifiedFiles}. " +
                    $"DependencyInfo is updated: {hasUsedBuildInfo}.");
            }
            if (!hasModifiedFiles)
            {
                Trace.TraceWarning("Dependencies are currently up to date");
                return false;
            }
            return true;
        }

        private static bool GitHasChanges()
        {
            CommandResult statusResult = Command.Git("status", "--porcelain")
                .CaptureStdOut()
                .Execute();
            statusResult.EnsureSuccessful();

            return !string.IsNullOrWhiteSpace(statusResult.StdOut);
        }
    }
}
