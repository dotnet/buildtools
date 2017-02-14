// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.VersionTools.Automation;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public class LocalUpdatePublishedVersions : Task
    {
        [Required]
        public ITaskItem[] ShippedNuGetPackage { get; set; }

        [Required]
        public string VersionsRepoDir { get; set; }

        [Required]
        public string VersionsRepoPath { get; set; }

        public override bool Execute()
        {
            MsBuildTraceListener[] listeners = Trace.Listeners.AddMsBuildTraceListeners(Log);

            var updater = new LocalVersionsRepoUpdater();

            updater.UpdateBuildInfoLatestPackages(
                ShippedNuGetPackage.Select(item => item.ItemSpec),
                VersionsRepoDir,
                VersionsRepoPath);

            Trace.Listeners.RemoveMsBuildTraceListeners(listeners);

            return true;
        }
    }
}
