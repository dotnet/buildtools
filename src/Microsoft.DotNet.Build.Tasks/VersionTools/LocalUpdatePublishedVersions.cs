// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.VersionTools.Automation;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public class LocalUpdatePublishedVersions : BuildTask
    {
        [Required]
        public ITaskItem[] ShippedNuGetPackage { get; set; }

        [Required]
        public string VersionsRepoLocalBaseDir { get; set; }

        [Required]
        public string VersionsRepoPath { get; set; }

        public override bool Execute()
        {
            Trace.Listeners.MsBuildListenedInvoke(Log, () =>
            {
                var updater = new LocalVersionsRepoUpdater();

                updater.UpdateBuildInfoLatestPackages(
                    ShippedNuGetPackage.Select(item => item.ItemSpec),
                    VersionsRepoLocalBaseDir,
                    VersionsRepoPath);
            });
            return true;
        }
    }
}
