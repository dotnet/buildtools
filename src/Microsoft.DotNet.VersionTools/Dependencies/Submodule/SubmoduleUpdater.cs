// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Dependencies.BuildOutput;
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

        public IEnumerable<DependencyUpdateTask> GetUpdateTasks(IEnumerable<IDependencyInfo> dependencyInfos)
        {
            IEnumerable<IDependencyInfo> usedInfos;
            string desiredHash = GetDesiredCommitHash(dependencyInfos, out usedInfos);
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
                Trace.TraceInformation($"In '{Path}', moving from '{currentHash}' to '{desiredHash}'.");

                FetchRemoteBranch();

                Trace.TraceInformation($"In '{Path}', checking out '{desiredHash}'.");
                GitInPath("checkout", desiredHash).Execute();
            };

            string[] updateStrings =
            {
                $"In '{Path}', current HEAD '{currentHash}' should be '{desiredHash}'"
            };

            yield return new DependencyUpdateTask(update, usedInfos, updateStrings);
        }

        protected abstract string GetDesiredCommitHash(
            IEnumerable<IDependencyInfo> dependencyInfos,
            out IEnumerable<IDependencyInfo> usedDependencyInfos);

        /// <summary>
        /// Fetch the remote branch that contains the commit being upgraded to. By default, fetch
        /// all remotes, but subclasses may be able to reduce the fetch scope.
        /// </summary>
        protected virtual void FetchRemoteBranch()
        {
            Trace.TraceInformation($"Fetching all configured remotes for '{Path}'.");
            GitInPath("fetch", "--all").Execute();
        }

        protected string GetCurrentCommitHash()
        {
            return FetchGitInPathOutput("rev-parse", "HEAD").Trim();
        }

        protected string GetCurrentIndexedHash()
        {
            // Get the current commit of the submodule as tracked by the containing repo. This
            // ensures local changes don't interfere.
            // https://git-scm.com/docs/git-submodule/1.8.2#git-submodule---cached
            return FetchGitOutput("submodule", "status", "--cached", Path)
                .Substring(1, 40);
        }

        protected string FetchGitInPathOutput(params string[] args) => ExecuteOutput(GitInPath(args));

        protected string FetchGitOutput(params string[] args) => ExecuteOutput(Command.Git(args));

        internal Command GitInPath(params string[] args)
        {
            var dirArgs = new[]
            {
                "-C", Path
            };
            return Command.Git(dirArgs.Concat(args).ToArray());
        }

        internal static string ExecuteOutput(Command c)
        {
            var writer = new StringWriter();

            c.ForwardStdOut(writer).Execute();

            return writer.ToString();
        }
    }
}
