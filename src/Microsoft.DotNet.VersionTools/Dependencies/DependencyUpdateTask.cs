// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Automation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.DotNet.VersionTools.Dependencies
{
    /// <summary>
    /// A task that can be performed to update dependency versions to values in BuildInfos. Has
    /// properties that describe the task to be performed.
    /// </summary>
    public class DependencyUpdateTask : Task<DependencyUpdateResults>
    {
        /// <summary>
        /// The build infos that would be used during this update.
        /// </summary>
        public IEnumerable<BuildInfo> UsedBuildInfos { get; }

        public IEnumerable<string> ReadableDescriptionLines { get; }

        public DependencyUpdateTask(
            Action updateAction,
            IEnumerable<BuildInfo> usedBuildInfos,
            IEnumerable<string> readableDescriptionLines)
            : base(() =>
            {
                updateAction();
                return new DependencyUpdateResults(usedBuildInfos);
            })
        {
            UsedBuildInfos = usedBuildInfos;
            ReadableDescriptionLines = readableDescriptionLines;
        }
    }
}
