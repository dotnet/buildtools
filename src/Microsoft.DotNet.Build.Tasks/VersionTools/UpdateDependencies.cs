// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.VersionTools;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Dependencies;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public class UpdateDependencies : Task
    {
        [Required]
        public ITaskItem[] DependencyBuildInfo { get; set; }

        public ITaskItem[] ProjectJsonFiles { get; set; }

        public ITaskItem[] XmlUpdateStep { get; set; }

        public string ProjectRepoOwner { get; set; }

        [Required]
        public string ProjectRepoName { get; set; }
        [Required]
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

        public bool AlwaysCreateNewPullRequest { get; set; }

        public override bool Execute()
        {
            MsBuildTraceListener[] listeners = Trace.Listeners.AddMsBuildTraceListeners(Log);
            try
            {
                IDependencyUpdater[] updaters = GetDependencyUpdaters().ToArray();
                BuildInfo[] buildInfos = GetBuildInfos().ToArray();
                var updater = new DependencyUpdater();

                DependencyUpdateResults updateResults = updater.Update(updaters, buildInfos);

                if (updateResults.ChangesDetected())
                {
                    var gitHubAuth = new GitHubAuth(GitHubAuthToken, GitHubUser, GitHubEmail);

                    var origin = new GitHubProject(ProjectRepoName, GitHubUser);

                    var upstreamBranch = new GitHubBranch(
                        ProjectRepoBranch,
                        new GitHubProject(ProjectRepoName, ProjectRepoOwner));

                    string suggestedMessage = updateResults.GetSuggestedCommitMessage();
                    string body = string.Empty;
                    if (NotifyGitHubUsers != null)
                    {
                        body += PullRequestCreator.NotificationString(NotifyGitHubUsers.Select(item => item.ItemSpec));
                    }

                    var prCreator = new PullRequestCreator(gitHubAuth, origin, upstreamBranch, GitHubAuthor);
                    prCreator.CreateOrUpdateAsync(
                        suggestedMessage,
                        suggestedMessage + $" ({ProjectRepoBranch})",
                        body,
                        forceCreate: AlwaysCreateNewPullRequest).Wait();
                }
            }
            finally
            {
                Trace.Listeners.RemoveMsBuildTraceListeners(listeners);
            }

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
