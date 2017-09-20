// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class PullRequestCreator
    {
        private GitHubAuth _auth;

        public GitHubBranch UpstreamBranch { get; }
        public GitHubProject Origin { get; }

        public string GitAuthorName { get; }

        private IUpdateBranchNamingStrategy _namingStrategy;

        public PullRequestCreator(
            GitHubAuth auth,
            string projectName,
            string upstreamOwner,
            string upstreamBranch,
            string gitAuthorName)
            : this(
                auth,
                new GitHubProject(projectName, auth.User),
                new GitHubBranch(upstreamBranch, new GitHubProject(projectName, upstreamOwner)),
                gitAuthorName)
        {
        }

        public PullRequestCreator(
            GitHubAuth auth,
            GitHubProject origin,
            GitHubBranch upstreamBranch,
            string gitAuthorName = null,
            IUpdateBranchNamingStrategy namingStrategy = null)
        {
            _auth = auth;

            Origin = origin;
            UpstreamBranch = upstreamBranch;

            GitAuthorName = gitAuthorName ?? auth.User;
            _namingStrategy = namingStrategy ?? new SingleBranchNamingStrategy("UpdateDependencies");
        }

        public async Task CreateOrUpdateAsync(
            string commitMessage,
            string title,
            string description,
            bool forceCreate = false,
            bool maintainersCanModify = true)
        {
            var upstream = UpstreamBranch.Project;

            using (var client = new GitHubClient(_auth))
            {
                GitHubBranch originBranch = null;
                GitHubPullRequest pullRequestToUpdate = null;

                string upgradeBranchPrefix = _namingStrategy.Prefix(UpstreamBranch.Name);

                if (!forceCreate)
                {
                    pullRequestToUpdate = await client.SearchPullRequestsAsync(
                        upstream,
                        upgradeBranchPrefix,
                        _auth.User);

                    if (pullRequestToUpdate == null)
                    {
                        Trace.TraceInformation($"No existing pull request found.");
                    }
                    else
                    {
                        Trace.TraceInformation(
                            $"Pull request already exists for {upgradeBranchPrefix} in {upstream.Segments}. " +
                            $"#{pullRequestToUpdate.Number}, '{pullRequestToUpdate.Title}'");

                        string blockedReason = GetUpdateBlockedReason(client, pullRequestToUpdate, upgradeBranchPrefix);

                        if (blockedReason == null)
                        {
                            originBranch = new GitHubBranch(
                                pullRequestToUpdate.Head.Ref,
                                Origin);
                        }
                        else
                        {
                            string comment =
                                $"Couldn't update this pull request: {blockedReason}\n" +
                                $"Would have applied '{commitMessage}'";

                            await client.PostCommentAsync(upstream, pullRequestToUpdate.Number, comment);
                            return;
                        }
                    }
                }

                // No existing branch to update: push to a new one.
                if (originBranch == null)
                {
                    string newBranchName =
                        _namingStrategy.Prefix(UpstreamBranch.Name) +
                        _namingStrategy.CreateFreshBranchNameSuffix(UpstreamBranch.Name);

                    originBranch = new GitHubBranch(newBranchName, Origin);
                }

                PushNewCommit(originBranch, commitMessage);

                if (pullRequestToUpdate != null)
                {
                    await client.UpdateGitHubPullRequestAsync(
                        upstream,
                        pullRequestToUpdate.Number,
                        title,
                        description,
                        maintainersCanModify: maintainersCanModify);
                }
                else
                {
                    await client.PostGitHubPullRequestAsync(
                        title,
                        description,
                        originBranch,
                        UpstreamBranch,
                        maintainersCanModify);
                }
            }
        }

        public static string NotificationString(IEnumerable<string> usernames)
        {
            return $"/cc @{string.Join(" @", usernames)}";
        }

        private string GetUpdateBlockedReason(
            GitHubClient client,
            GitHubPullRequest pullRequest,
            string upgradeBranchPrefix)
        {
            if (pullRequest.Head.User.Login != Origin.Owner)
            {
                return $"Owner of head repo '{pullRequest.Head.User.Login}' is not '{Origin.Owner}'";
            }
            if (!pullRequest.Head.Ref.StartsWith(upgradeBranchPrefix))
            {
                return $"Ref name '{pullRequest.Head.Ref}' does not start with '{upgradeBranchPrefix}'";
            }

            GitCommit commit = client.GetCommitAsync(Origin, pullRequest.Head.Sha).Result;
            if (commit.Author.Name != GitAuthorName)
            {
                return $"Head commit author '{commit.Author.Name}' is not '{GitAuthorName}'";
            }
            if (commit.Committer.Name != GitAuthorName)
            {
                return $"Head commit committer '{commit.Committer.Name}' is not '{GitAuthorName}'";
            }
            return null;
        }

        private void PushNewCommit(GitHubBranch branch, string commitMessage)
        {
            Command.Git("commit", "-a", "-m", commitMessage, "--author", $"{GitAuthorName} <{_auth.Email}>")
                .EnvironmentVariable("GIT_COMMITTER_NAME", GitAuthorName)
                .EnvironmentVariable("GIT_COMMITTER_EMAIL", _auth.Email)
                .Execute()
                .EnsureSuccessful();

            string remoteUrl = $"github.com/{branch.Project.Segments}.git";
            string refSpec = $"HEAD:refs/heads/{branch.Name}";

            string logMessage = $"git push https://{remoteUrl} {refSpec}";
            Trace.TraceInformation($"EXEC {logMessage}");

            CommandResult pushResult =
                Command.Git("push", "--force", $"https://{_auth.User}:{_auth.AuthToken}@{remoteUrl}", refSpec)
                    .QuietBuildReporter()  // we don't want secrets showing up in our logs
                    .CaptureStdErr() // git push will write to StdErr upon success, disable that
                    .CaptureStdOut()
                    .Execute();

            var message = logMessage + $" exited with {pushResult.ExitCode}";
            if (pushResult.ExitCode == 0)
            {
                Trace.TraceInformation($"EXEC success: {message}");
            }
            else
            {
                Trace.TraceError($"EXEC failure: {message}");
            }

            pushResult.EnsureSuccessful(suppressOutput: true);
        }
    }
}
