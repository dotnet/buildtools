// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Dependencies.Submodule
{
    /// <summary>
    /// Updates the submodule at the given path to the latest commit available from its target
    /// repository.
    /// </summary>
    public class LatestCommitSubmoduleUpdater : SubmoduleUpdater
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

        protected override string GetDesiredCommitHash(
            IEnumerable<DependencyBuildInfo> dependencyBuildInfos,
            out IEnumerable<DependencyBuildInfo> usedBuildInfos)
        {
            usedBuildInfos = Enumerable.Empty<DependencyBuildInfo>();

            string remoteRefOutput = FetchGitOutput("ls-remote", Repository, Ref);

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
                    "The configured Ref must match exactly one ref on the remote. " +
                    $"Matched {remoteRefLines.Length}{allRefs}.");
            }

            return remoteRefLines.Single().Split('\t').First();
        }
    }
}
