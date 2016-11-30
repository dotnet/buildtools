// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.VersionTools;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public class UpdateDependenciesAndSubmitPullRequest : BaseDependenciesTask
    {
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

        public string CurrentRefXmlPath { get; set; }

        public bool AlwaysCreateNewPullRequest { get; set; }

        protected override void TraceListenedExecute()
        {
            // Use the commit sha of versions repo master (not just "master") for stable upgrade.
            var gitHubAuth = new GitHubAuth(GitHubAuthToken, GitHubUser, GitHubEmail);
            var client = new GitHubClient(gitHubAuth);
            string masterSha = client
                .GetReferenceAsync(new GitHubProject("versions", "dotnet"), "heads/master")
                .Result.Object.Sha;

            foreach (ITaskItem item in DependencyBuildInfo)
            {
                if (!string.IsNullOrEmpty(item.GetMetadata(CurrentRefMetadataName)))
                {
                    item.SetMetadata(CurrentRefMetadataName, masterSha);
                }
                string autoUpgradeBranch = item.GetMetadata(AutoUpgradeBranchMetadataName);
                if (!string.IsNullOrEmpty(autoUpgradeBranch))
                {
                    item.SetMetadata(CurrentBranchMetadataName, autoUpgradeBranch);
                }
            }

            DependencyUpdateResults updateResults = DependencyUpdateUtils.Update(
                CreateUpdaters().ToArray(),
                CreateBuildInfoDependencies().ToArray());

            // Update CurrentRef and CurrentBranch for each applicable build info used.
            if (!string.IsNullOrEmpty(CurrentRefXmlPath))
            {
                foreach (BuildInfo info in updateResults.UsedBuildInfos)
                {
                    foreach (var infoItem in DependencyBuildInfo.Where(item => item.ItemSpec == info.Name))
                    {
                        if (!string.IsNullOrEmpty(infoItem.GetMetadata(CurrentRefMetadataName)))
                        {
                            UpdateProperty(
                                CurrentRefXmlPath,
                                $"{info.Name}{CurrentRefMetadataName}",
                                masterSha);
                        }

                        string autoUpgradeBranch = infoItem.GetMetadata(AutoUpgradeBranchMetadataName);
                        if (!string.IsNullOrEmpty(autoUpgradeBranch))
                        {
                            UpdateProperty(
                                CurrentRefXmlPath,
                                $"{info.Name}{CurrentBranchMetadataName}",
                                autoUpgradeBranch);
                        }
                    }
                }
            }

            if (updateResults.ChangesDetected())
            {
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
            else
            {
                Log.LogMessage("No update required: no changes detected.");
            }
        }

        private void UpdateProperty(string path, string elementName, string newValue)
        {
            const string valueGroup = "valueGroup";
            Action updateAction = FileUtils.GetUpdateFileContentsTask(
                path,
                contents =>
                {
                    Group g = CreateXmlUpdateRegex(elementName, valueGroup)
                        .Match(contents)
                        .Groups[valueGroup];

                    if (g.Success)
                    {
                        return contents
                            .Remove(g.Index, g.Length)
                            .Insert(g.Index, newValue);
                    }
                    return contents;
                });
            // There may not be an task to perform for the value to be up to date: allow null.
            updateAction?.Invoke();
        }
    }
}
