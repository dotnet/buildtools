// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Dependencies.Submodule
{
    public abstract class SubmoduleUpdater : IDependencyUpdater
    {
        public string Path { get; set; }

        public IEnumerable<DependencyUpdateTask> GetUpdateTasks(IEnumerable<DependencyBuildInfo> dependencyBuildInfos)
        {
            IEnumerable<DependencyBuildInfo> usedBuildInfos;
            string desiredHash = GetDesiredCommitHash(dependencyBuildInfos, out usedBuildInfos);
            string currentHash = GetCurrentCommitHash();

            if (desiredHash == null)
            {
                Trace.TraceWarning($"Unable to find a desired hash for '{Path}', leaving as '{currentHash}'.");
                yield break;
            }

            if (desiredHash == currentHash)
            {
                Trace.TraceInformation($"Nothing to upgrade for '{Path}' at '{desiredHash}'");
                yield break;
            }

            Action update = () =>
            {
                Trace.TraceInformation($"Fetching all configured remotes for '{Path}' to move from '{currentHash}' to '{desiredHash}'.");
                GitInPath("fetch", "--all").Execute();

                Trace.TraceInformation($"In '{Path}', checking out '{desiredHash}'.");
                GitInPath("checkout", desiredHash).Execute();
            };

            string[] updateStrings =
            {
                $"In '{Path}', HEAD '{currentHash}' must be '{desiredHash}'"
            };

            yield return new DependencyUpdateTask(update, usedBuildInfos.Select(d => d.BuildInfo), updateStrings);
        }

        protected abstract string GetDesiredCommitHash(
            IEnumerable<DependencyBuildInfo> dependencyBuildInfos,
            out IEnumerable<DependencyBuildInfo> usedBuildInfos);

        protected string GetCurrentCommitHash()
        {
            return FetchGitOutput("rev-parse", "HEAD").Trim();
        }

        protected string FetchGitOutput(params string[] args)
        {
            var writer = new StringWriter();

            GitInPath(args)
                .ForwardStdOut(writer)
                .Execute();

            return writer.ToString();
        }

        internal Command GitInPath(params string[] args)
        {
            var dirArgs = new[]
            {
                "-C", Path
            };
            return Command.Git(dirArgs.Concat(args).ToArray());
        }
    }
}
