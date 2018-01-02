// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class PullRequestCreator
    {
        private const string DiscardedCommitElementName = "auto-pr-discard-list";

        private GitHubAuth _auth;

        public string GitAuthorName { get; }

        public PullRequestCreator(
            GitHubAuth auth,
            string gitAuthorName = null)
        {
            if (auth == null)
            {
                throw new ArgumentNullException(
                    nameof(auth),
                    "Authentication is required: pull requests cannot be created anonymously.");
            }
            _auth = auth;
            GitAuthorName = gitAuthorName ?? auth.User;
        }

        public async Task CreateOrUpdateAsync(
            string commitMessage,
            string title,
            string description,
            GitHubBranch baseBranch,
            GitHubProject origin,
            PullRequestOptions options)
        {
            options = options ?? new PullRequestOptions();

            var upstream = baseBranch.Project;

            using (var client = new GitHubClient(_auth))
            {
                GitHubBranch originBranch = null;
                GitHubPullRequest pullRequestToUpdate = null;

                IUpdateBranchNamingStrategy namingStrategy = options.BranchNamingStrategy
                    ?? new SingleBranchNamingStrategy("UpdateDependencies");

                string upgradeBranchPrefix = namingStrategy.Prefix(baseBranch.Name);

                if (!options.ForceCreate)
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

                        GitCommit headCommit = await client.GetCommitAsync(
                            origin,
                            pullRequestToUpdate.Head.Sha);

                        string blockedReason = GetUpdateBlockedReason(
                            pullRequestToUpdate.Head,
                            headCommit,
                            upgradeBranchPrefix,
                            origin);

                        if (blockedReason == null)
                        {
                            if (options.TrackDiscardedCommits)
                            {
                                await PostDiscardedCommitCommentAsync(
                                    baseBranch.Project,
                                    pullRequestToUpdate,
                                    headCommit,
                                    client);
                            }

                            originBranch = new GitHubBranch(
                                pullRequestToUpdate.Head.Ref,
                                origin);
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
                        namingStrategy.Prefix(baseBranch.Name) +
                        namingStrategy.CreateFreshBranchNameSuffix(baseBranch.Name);

                    originBranch = new GitHubBranch(newBranchName, origin);
                }

                PushNewCommit(originBranch, commitMessage);

                if (pullRequestToUpdate != null)
                {
                    await client.UpdateGitHubPullRequestAsync(
                        upstream,
                        pullRequestToUpdate.Number,
                        title,
                        description,
                        maintainersCanModify: options.MaintainersCanModify);
                }
                else
                {
                    await client.PostGitHubPullRequestAsync(
                        title,
                        description,
                        originBranch,
                        baseBranch,
                        options.MaintainersCanModify);
                }
            }
        }

        private async Task PostDiscardedCommitCommentAsync(
            GitHubProject baseProject,
            GitHubPullRequest pullRequestToUpdate,
            GitCommit oldCommit,
            GitHubClient client)
        {
            GitHubCombinedStatus combinedStatus = await client.GetStatusAsync(
                baseProject,
                oldCommit.Sha);

            CiStatusLine[] statuses = combinedStatus
                .Statuses
                .OrderBy(s => s.State)
                .ThenBy(s => s.Context)
                .Select(CiStatusLine.Create)
                .ToArray();

            string statusLines = statuses
                .Aggregate(string.Empty, (acc, line) => acc + line.MarkdownLine + "\r\n");

            string ciSummary = string.Join(
                " ",
                statuses
                    .GroupBy(s => s.Emoticon)
                    .Select(g => $"{g.Count()}{g.Key}")
                    .ToArray());

            string commentBody =
                $"Discarded [`{oldCommit.Sha.Substring(0, 7)}`]({oldCommit.HtmlUrl}): " +
                $"`{oldCommit.Message}`";

            if (statuses.Any())
            {
                commentBody += "\r\n\r\n" +
                    "<details>" +
                    "<summary>" +
                    $"CI Status: {ciSummary} (click to expand)\r\n" +
                    "</summary>" +
                    $"\r\n\r\n{statusLines}\r\n" +
                    "</details>";
            }

            await client.PostCommentAsync(
                baseProject,
                pullRequestToUpdate.Number,
                commentBody);
        }

        public static string NotificationString(IEnumerable<string> usernames)
        {
            return $"/cc @{string.Join(" @", usernames)}";
        }

        private string GetUpdateBlockedReason(
            GitHubHead head,
            GitCommit headCommit,
            string upgradeBranchPrefix,
            GitHubProject origin)
        {
            if (head.User.Login != origin.Owner)
            {
                return $"Owner of head repo '{head.User.Login}' is not '{origin.Owner}'";
            }
            if (!head.Ref.StartsWith(upgradeBranchPrefix))
            {
                return $"Ref name '{head.Ref}' does not start with '{upgradeBranchPrefix}'";
            }
            if (headCommit.Author.Name != GitAuthorName)
            {
                return $"Head commit author '{headCommit.Author.Name}' is not '{GitAuthorName}'";
            }
            if (headCommit.Committer.Name != GitAuthorName)
            {
                return $"Head commit committer '{headCommit.Committer.Name}' is not '{GitAuthorName}'";
            }
            return null;
        }

        private void PushNewCommit(GitHubBranch branch, string commitMessage)
        {
            GitCommand.Commit(commitMessage, GitAuthorName, _auth.Email, all: true);

            string remoteUrl = $"github.com/{branch.Project.Segments}.git";
            string refSpec = $"HEAD:refs/heads/{branch.Name}";

            GitCommand.Push(
                $"https://{_auth.User}:{_auth.AuthToken}@{remoteUrl}",
                $"https://{remoteUrl}",
                refSpec,
                force: true);
        }

        private class CiStatusLine
        {
            public static CiStatusLine Create(GitHubStatus status)
            {
                string emoticon = ":grey_question:";
                if (string.Equals(status.State, "success", StringComparison.OrdinalIgnoreCase))
                {
                    emoticon = ":heavy_check_mark:";
                }
                else if (string.Equals(status.State, "pending", StringComparison.OrdinalIgnoreCase))
                {
                    emoticon = ":hourglass:";
                }
                else if (string.Equals(status.State, "error", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(status.State, "failure", StringComparison.OrdinalIgnoreCase))
                {
                    emoticon = ":x:";
                }

                string line = $" * {emoticon} **{status.Context}** {status.Description}";
                if (!string.IsNullOrEmpty(status.TargetUrl))
                {
                    line += $" [Details]({status.TargetUrl})";
                }

                return new CiStatusLine
                {
                    Emoticon = emoticon,
                    MarkdownLine = line
                };
            }

            public string Emoticon { get; private set; }
            public string MarkdownLine { get; private set; }
        }
    }
}
