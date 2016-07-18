// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.VersionTools;
using Microsoft.DotNet.VersionTools.Automation;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.DotNet.VersionTools.Dependencies;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public class UpdateDependencies : Task
    {
        [Required]
        public ITaskItem[] DependencyBuildInfo { get; set; }

        public ITaskItem[] ProjectJsonFiles { get; set; }

        public ITaskItem[] XmlUpdateStep { get; set; }

        public string ProjectRepo { get; set; }
        public string ProjectRepoOwner { get; set; }
        public string ProjectRepoBranch { get; set; }

        [Required]
        public string GitHubAuthToken { get; set; }
        public string GitHubUser { get; set; }
        public string GitHubEmail { get; set; }

        /// <summary>
        /// The git author of the update commit. Defaults to the same as GitHubUser.
        /// </summary>
        public string GitHubAuthor { get; set; }

        public ITaskItem[] NotifyGitHubUsers { get; set; }

        public override bool Execute()
        {
            MsBuildTraceListener[] listeners = Trace.Listeners.AddMsBuildTraceListeners(Log);

            IDependencyUpdater[] updaters = GetDependencyUpdaters().ToArray();
            BuildInfo[] buildInfos = GetBuildInfos().ToArray();

            var gitHubAuth = new GitHubAuth(GitHubAuthToken, GitHubUser, GitHubEmail);

            var updater = new DependencyUpdater(
                gitHubAuth,
                ProjectRepo,
                ProjectRepoOwner,
                ProjectRepoBranch,
                GitHubAuthor ?? GitHubUser,
                NotifyGitHubUsers?.Select(item => item.ItemSpec));

            updater.UpdateAndSubmitPullRequestAsync(updaters, buildInfos).Wait();

            Trace.Listeners.RemoveMsBuildTraceListeners(listeners);

            return true;
        }

        private IEnumerable<IDependencyUpdater> GetDependencyUpdaters()
        {
            if (ProjectJsonFiles?.Any() ?? false)
            {
                yield return new ProjectJsonUpdater(ProjectJsonFiles.Select(item => item.ItemSpec));
            }

            if (XmlUpdateStep != null)
            {
                foreach (ITaskItem step in XmlUpdateStep)
                {
                    yield return new FileRegexReleaseUpdater
                    {
                        Path = step.GetMetadata("Path"),
                        Regex = new Regex($@"<{step.GetMetadata("ElementName")}>(?<version>.*)<"),
                        VersionGroupName = "version",
                        BuildInfoName = step.GetMetadata("BuildInfoName")
                    };
                }
            }
        }

        private IEnumerable<BuildInfo> GetBuildInfos()
        {
            return DependencyBuildInfo.Select(buildInfoItem => BuildInfo.Get(
                buildInfoItem.ItemSpec,
                buildInfoItem.GetMetadata("RawUrl")));
        }
    }
}
