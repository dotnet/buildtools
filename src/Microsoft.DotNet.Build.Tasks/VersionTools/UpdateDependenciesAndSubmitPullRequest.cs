// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public class UpdateDependenciesAndSubmitPullRequest : UpdateToRemoteDependencies
    {
        public string ProjectRepoOwner { get; set; }

        [Required]
        public string ProjectRepoName { get; set; }
        [Required]
        public string ProjectRepoBranch { get; set; }

        /// <summary>
        /// The git author of the update commit. Defaults to the same as GitHubUser.
        /// </summary>
        public string GitHubAuthor { get; set; }

        public ITaskItem[] NotifyGitHubUsers { get; set; }

        public bool AlwaysCreateNewPullRequest { get; set; }

        public bool MaintainersCanModifyPullRequest { get; set; }

        /// <summary>
        /// A commit message to use instead of the default generated message.
        /// </summary>
        public string CommitMessage { get; set; }

        protected override void TraceListenedExecute()
        {
            using (GitHubClient client = CreateClient(allowAnonymous: false))
            {
                DependencyUpdateResults updateResults = UpdateToRemote(client);

                if (updateResults.ChangesDetected())
                {
                    var origin = new GitHubProject(ProjectRepoName, GitHubUser);

                    var upstreamBranch = new GitHubBranch(
                        ProjectRepoBranch,
                        new GitHubProject(ProjectRepoName, ProjectRepoOwner));

                    if (string.IsNullOrEmpty(CommitMessage))
                    {
                        CommitMessage = updateResults.GetSuggestedCommitMessage();
                    }

                    string body = string.Empty;
                    if (NotifyGitHubUsers != null)
                    {
                        body += PullRequestCreator.NotificationString(NotifyGitHubUsers.Select(item => item.ItemSpec));
                    }

                    var prCreator = new PullRequestCreator(client.Auth, origin, upstreamBranch, GitHubAuthor);
                    prCreator.CreateOrUpdateAsync(
                        CommitMessage,
                        CommitMessage + $" ({ProjectRepoBranch})",
                        body,
                        forceCreate: AlwaysCreateNewPullRequest,
                        maintainersCanModify: MaintainersCanModifyPullRequest).Wait();
                }
                else
                {
                    Log.LogMessage("No update required: no changes detected.");
                }
            }
        }
    }
}
