// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Dependencies;
using Microsoft.DotNet.VersionTools.Dependencies.BuildOutput;
using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class DependencyUpdateResults
    {
        public IEnumerable<IDependencyInfo> UsedInfos { get; }

        public IEnumerable<BuildInfo> UsedBuildInfos { get; }

        public DependencyUpdateResults(IEnumerable<IDependencyInfo> usedInfos)
        {
            UsedInfos = usedInfos;

            UsedBuildInfos = UsedInfos
                .OfType<BuildDependencyInfo>()
                .Select(i => i.BuildInfo)
                .Distinct()
                .ToArray();
        }

        public string GetSuggestedCommitMessage()
        {
            var orderedInfos = UsedInfos.OrderBy(info => info.SimpleName).ToArray();

            string updatedDependencyNames = string.Join(", ", orderedInfos.Select(d => d.SimpleName));
            string updatedDependencyVersions = string.Join(", ", orderedInfos.Select(d => d.SimpleVersion));

            string commitMessage = $"Update {updatedDependencyNames} to {updatedDependencyVersions}";
            if (UsedInfos.Count() > 1)
            {
                commitMessage += ", respectively";
            }
            return commitMessage;
        }

        public bool ChangesDetected()
        {
            // Ensure changes were performed as expected.
            bool hasModifiedFiles = GitHasChanges();
            bool hasUsedBuildInfo = UsedInfos.Any();
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
