// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using LibGit2Sharp;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GitTag : Task
    {
        [Required]
        public string RepositoryRoot { get; set; }

        [Required]
        public string Tag { get; set; }

        public string TaggerName { get; set; }

        public string TaggerEmail { get; set; }

        public string TagMessage { get; set; }

        public override bool Execute()
        {
            using (var repository = new Repository(RepositoryRoot))
            {
                if (repository.Tags[Tag] == null)
                {
                    repository.Tags.Add(Tag, repository.Head.Tip, new Signature(TaggerName, TaggerEmail, DateTimeOffset.Now), TagMessage);

                    Log.LogMessage("Tagged repository {0} commit {1}... with tag {2}", RepositoryRoot, repository.Head.Tip.Sha.Substring(0, 8), Tag);
                }
            }

            return true;
        }
    }
}
