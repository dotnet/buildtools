// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.VersionTools;
using Microsoft.DotNet.VersionTools.Dependencies.BuildOutput;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public class TaskItemBuildDependencyInfo : BuildDependencyInfo
    {
        /// <summary>
        /// MSBuild item used to create this DependencyInfo.
        /// </summary>
        public ITaskItem SourceItem { get; }

        public TaskItemBuildDependencyInfo(
            BuildInfo buildInfo,
            bool upgradeStableVersions,
            IEnumerable<string> disabledPackages,
            ITaskItem sourceItem)
            : base(buildInfo, upgradeStableVersions, disabledPackages)
        {
            SourceItem = sourceItem;
        }
    }
}
