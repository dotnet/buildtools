// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class PullRequestOptions
    {
        /// <summary>
        /// Always create new pull requests.
        /// </summary>
        public bool ForceCreate { get; set; }

        /// <summary>
        /// Allow repo maintainers to modify the pull request without explicitly giving them
        /// permission to the fork being used.
        /// </summary>
        public bool MaintainersCanModify { get; set; } = true;

        /// <summary>
        /// When force pushing to a branch, update the description with a pointer to the commit that
        /// was discarded so it is still accessible, along with CI.
        /// </summary>
        public bool TrackDiscardedCommits { get; set; } = true;

        /// <summary>
        /// A custom branching strategy to use instead of the PullRequestCreator default.
        /// </summary>
        public IUpdateBranchNamingStrategy BranchNamingStrategy { get; set; }
    }
}
