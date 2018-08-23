// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Microsoft.DotNet.VersionTools.Automation.VstsApi;
using System;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public class SubmitPullRequest : BuildTask
    {
        public string GitHubAuthToken { get; set; }
        public string GitHubUser { get; set; }
        public string GitHubEmail { get; set; }

        /// <summary>
        /// A VSTS PAT. Setting this makes VstsInstanceName and GitHubAuthor required. Setting this
        /// property indicates that the PR will be sent to VSTS (regardless whether GitHubAuthToken
        /// is set).
        ///
        /// VstsPat and GitHubAuthToken may both be set: this is useful to get a larger rate limit
        /// for GitHub requests during the earlier tasks in the update process and submit the final
        /// PR to VSTS.
        /// </summary>
        public string VstsPat { get; set; }
        public string VstsInstanceName { get; set; }
        public string VstsCommitterEmail { get; set; }
        public string VstsApiVersionOverride { get; set; }

        /// <summary>
        /// GitHub repository owner (such as 'dotnet') or VSTS project containing the repo.
        /// </summary>
        public string ProjectRepoOwner { get; set; }

        [Required]
        public string ProjectRepoName { get; set; }
        [Required]
        public string ProjectRepoBranch { get; set; }

        public string CommitMessage { get; set; }

        /// <summary>
        /// Title of the pull request. Defaults to the commit message.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Body of the pull request. Optional.
        /// 
        /// Only used when submitting a new pull request or if TrackDiscardedCommits is false.
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// The git author of the update commit. Defaults to the same as GitHubUser.
        /// </summary>
        public string GitHubAuthor { get; set; }

        public ITaskItem[] NotifyGitHubUsers { get; set; }

        public bool AlwaysCreateNewPullRequest { get; set; }

        public bool MaintainersCanModifyPullRequest { get; set; }

        public bool TrackDiscardedCommits { get; set; }

        public override bool Execute()
        {
            Trace.Listeners.MsBuildListenedInvoke(Log, TraceListenedExecute);
            return !Log.HasLoggedErrors;
        }

        private void TraceListenedExecute()
        {
            // GitHub and VSTS have different dev flow conventions.
            GitHubProject origin;

            using (IGitHubClient client = CreateClient(out origin))
            {
                var upstreamBranch = new GitHubBranch(
                    ProjectRepoBranch,
                    new GitHubProject(ProjectRepoName, ProjectRepoOwner));

                string body = Body ?? string.Empty;

                if (NotifyGitHubUsers != null)
                {
                    body += PullRequestCreator.NotificationString(NotifyGitHubUsers.Select(item => item.ItemSpec));
                }

                var options = new PullRequestOptions
                {
                    ForceCreate = AlwaysCreateNewPullRequest,
                    MaintainersCanModify = MaintainersCanModifyPullRequest,
                    TrackDiscardedCommits = TrackDiscardedCommits
                };

                var prCreator = new PullRequestCreator(client.Auth, GitHubAuthor);
                prCreator.CreateOrUpdateAsync(
                    CommitMessage,
                    CommitMessage + $" ({ProjectRepoBranch})",
                    body,
                    upstreamBranch,
                    origin,
                    options,
                    client).Wait();
            }
        }

        private IGitHubClient CreateClient(out GitHubProject origin)
        {
            if (!string.IsNullOrEmpty(VstsPat))
            {
                if (string.IsNullOrEmpty(VstsInstanceName))
                {
                    throw new ArgumentException($"{nameof(VstsInstanceName)} is required but not set.");
                }
                if (string.IsNullOrEmpty(GitHubAuthor))
                {
                    throw new ArgumentException($"{nameof(GitHubAuthor)} is required but not set.");
                }

                var idAuth = new GitHubAuth(VstsPat);

                // Get profile information from VSTS to use to create the real client.
                using (var idClient = new VstsAdapterClient(idAuth, VstsInstanceName, VstsApiVersionOverride))
                {
                    VstsProfile profile = idClient.GetMyProfileAsync().Result;
                    var fullAuth = new GitHubAuth(VstsPat, profile.Id, VstsCommitterEmail);

                    origin = new GitHubProject(ProjectRepoName, ProjectRepoOwner);
                    return new VstsAdapterClient(fullAuth, VstsInstanceName, VstsApiVersionOverride);
                }
            }

            if (!string.IsNullOrEmpty(GitHubAuthToken))
            {
                if (string.IsNullOrEmpty(GitHubUser))
                {
                    throw new ArgumentException($"{nameof(GitHubUser)} is required but not set.");
                }
                if (string.IsNullOrEmpty(GitHubEmail))
                {
                    throw new ArgumentException($"{nameof(GitHubEmail)} is required but not set.");
                }

                origin = new GitHubProject(ProjectRepoName, GitHubUser);
                return new GitHubClient(new GitHubAuth(GitHubAuthToken, GitHubUser, GitHubEmail));
            }

            throw new ArgumentException($"{nameof(GitHubAuthToken)} and/or {nameof(VstsPat)} is required.");
        }
    }
}
