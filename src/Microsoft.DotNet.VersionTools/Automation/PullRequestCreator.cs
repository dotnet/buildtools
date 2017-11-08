﻿// Licensed to the .NET Foundation under one or more agreements.
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
                                await UpdateDiscardedCommitListAsync(
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

        private async Task UpdateDiscardedCommitListAsync(
            GitHubProject baseProject,
            GitHubPullRequest pullRequestToUpdate,
            GitCommit oldCommit,
            GitHubClient client)
        {
            // GitHub returns the HTML "commit" url, but we want "commits" so that CI results show.
            string oldCommitsUrl = oldCommit.HtmlUrl.Replace("commit", "commits");

            GitHubCombinedStatus combinedStatus = await client.GetStatusAsync(
                baseProject,
                oldCommit.Sha);

            string statusLines = combinedStatus
                .Statuses
                .OrderBy(s => s.State)
                .ThenBy(s => s.Context)
                .Select(GetStatusLine)
                .Aggregate(string.Empty, (acc, line) => acc + line + "\r\n");

            string oldCommitEntry =
                $" * [`{oldCommit.Sha.Substring(0, 7)}`]({oldCommitsUrl}) {oldCommit.Message}\r\n" +
                $"{statusLines}";

            // Find comment to update, if one exists.
            GitHubComment[] prIssueComments = await client.GetAllIssueCommentsAsync(
                baseProject,
                pullRequestToUpdate.Number);

            GitHubComment commentToUpdate = prIssueComments
                .FirstOrDefault(c => c.User.Login == _auth.User);

            string commentBodyToUpdate = commentToUpdate?.Body ?? string.Empty;

            // Find insertion point. GitHub always returns \r\n.
            string insertionMarker = $"<{DiscardedCommitElementName}>\r\n\r\n";
            string endInsertionMarker = $"\r\n</{DiscardedCommitElementName}>";

            int elementBegin = commentBodyToUpdate.IndexOf(
                insertionMarker,
                StringComparison.Ordinal);

            string newCommentBody;

            if (elementBegin != -1)
            {
                newCommentBody = commentBodyToUpdate.Insert(
                    elementBegin + insertionMarker.Length,
                    oldCommitEntry);
            }
            else
            {
                newCommentBody = commentBodyToUpdate +
                    "<details><summary>Discarded auto-update commits (click to expand)</summary>" +
                    $"{insertionMarker}{oldCommitEntry}{endInsertionMarker}" +
                    "</details>";
            }

            if (commentToUpdate == null)
            {
                await client.PostCommentAsync(
                    baseProject,
                    pullRequestToUpdate.Number,
                    newCommentBody);
            }
            else
            {
                await client.PatchCommentAsync(
                    baseProject,
                    commentToUpdate.Id,
                    newCommentBody);
            }
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

        private static string GetStatusLine(GitHubStatus status)
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

            string line = $"   * {emoticon} **{status.Context}** {status.Description}";

            if (!string.IsNullOrEmpty(status.TargetUrl))
            {
                line += $" [Details]({status.TargetUrl})";
            }

            return line;
        }
    }
}
