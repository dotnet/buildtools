// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Dependencies;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public static class DependencyUpdateUtils
    {
        /// <summary>
        /// Runs the updaters given using buildInfo sources, and returns information about the
        /// updates performed, such as a recommended commit name based on the BuildInfos used.
        /// </summary>
        public static DependencyUpdateResults Update(
            IEnumerable<IDependencyUpdater> updaters,
            IEnumerable<DependencyBuildInfo> buildInfoDependencies)
        {
            IEnumerable<BuildInfo> distinctUsedBuildInfos = GetUpdateTasks(updaters, buildInfoDependencies)
                .Select(task =>
                {
                    task.Start();
                    return task.Result;
                })
                .SelectMany(results => results.UsedBuildInfos)
                .Distinct()
                .ToArray();

            return new DependencyUpdateResults(distinctUsedBuildInfos);
        }

        /// <summary>
        /// Returns the tasks necessary to update dependencies.
        /// </summary>
        public static IEnumerable<DependencyUpdateTask> GetUpdateTasks(
            IEnumerable<IDependencyUpdater> updaters,
            IEnumerable<DependencyBuildInfo> buildInfoDependencies)
        {
            return updaters.SelectMany(updater => updater.GetUpdateTasks(buildInfoDependencies));
        }
    }
}
