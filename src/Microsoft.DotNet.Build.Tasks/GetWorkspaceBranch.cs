// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using LibGit2Sharp;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GetWorkspaceBranch : Task
    {
        [Required]
        public string RepositoryRoot { get; set; }

        [Output]
        public string WorkspaceBranch { get; set; }

        public override bool Execute()
        {
            using (var repository = new Repository(RepositoryRoot))
            {
                WorkspaceBranch = repository.Head.Name;
            }

            return true;
        }
    }
}
