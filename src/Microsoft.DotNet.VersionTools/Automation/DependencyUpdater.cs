// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Dependencies;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class DependencyUpdater
    {
        /// <summary>
        /// Runs the updaters given using buildInfo sources, and returns information about the
        /// update, such as a recommended commit name based on the BuildInfos used.
        /// </summary>
        public DependencyUpdateResults Update(
            IEnumerable<IDependencyUpdater> updaters,
            IEnumerable<BuildInfo> buildInfos)
        {
            IEnumerable<BuildInfo> usedBuildInfos = Enumerable.Empty<BuildInfo>();

            foreach (IDependencyUpdater updater in updaters)
            {
                IEnumerable<BuildInfo> newUsedBuildInfos = updater.Update(buildInfos);
                usedBuildInfos = usedBuildInfos.Union(newUsedBuildInfos);
            }

            return new DependencyUpdateResults(usedBuildInfos.ToArray());
        }
    }
}
