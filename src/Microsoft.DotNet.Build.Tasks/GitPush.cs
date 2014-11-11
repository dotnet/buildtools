// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GitPush : Task
    {
        [Required]
        public string RepositoryRoot { get; set; }

        [Required]
        public string RemoteUri { get; set; }

        [Required]
        public string RemoteUsername { get; set; }

        [Required]
        public string RemotePassword { get; set; }        

        public override bool Execute()
        {
            using (var repository = new Repository(RepositoryRoot))
            {
                string branchName = repository.Head.Name;
                // get the git commit associated with this build
                string sourceGetVersion = Environment.GetEnvironmentVariable("TF_BUILD_SOURCEGETVERSION");

                if (!string.IsNullOrEmpty(sourceGetVersion))
                {
                    // TF_BUILD_SOURCEGETVERSION has the branch and commit encoded (LG:branch:commit)
                    // Example: TF_BUILD_SOURCEGETVERSION = LG:refs/heads/master:e0bfee0d37677f4cb9bc28a0f5506f33b60c3712
                    int startIndex = sourceGetVersion.LastIndexOf('/') + 1;
                    int endIndex = sourceGetVersion.LastIndexOf(':');
                    branchName = sourceGetVersion.Substring(startIndex, endIndex - startIndex);
                }

                Remote remote = repository.Network.Remotes.Add("gitpushremote", RemoteUri);


                Branch branch = repository.Branches[branchName];
                if (branch == null)
                {
                    // Create branch
                    branch = repository.Branches.Add(branchName, repository.Head.Tip);
                }

                repository.Branches.Update(
                    branch,
                    b => b.Remote = remote.Name,
                    b => b.UpstreamBranch = branchName);

                Action<string> pushAction = (string pushRefSpec) =>
                    {
                        repository.Network.Push(
                            repository.Network.Remotes["gitpushremote"],
                            pushRefSpec,
                            new PushOptions
                            {
                                CredentialsProvider = new CredentialsHandler((url, usernameFromUrl, types) => new UsernamePasswordCredentials
                                {
                                    Username = RemoteUsername,
                                    Password = RemotePassword
                                })
                            });

                        Log.LogMessage("Pushed {0} to {1}", pushRefSpec, RemoteUri);
                    };

                // Force push current branch
                pushAction(string.Format("refs/heads/{0}", branchName));

                // Push tags
                foreach (Tag tag in repository.Tags)
                {
                    //pushAction(tag.CanonicalName);
                }
            }

            return true;
        }
    }
}
