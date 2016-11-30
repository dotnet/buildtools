// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Dependencies;
using System;
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
            var results = new List<DependencyUpdateResults>();
            var exceptions = new List<UpdateTargetNotFoundException>();

            foreach (var task in GetUpdateTasks(updaters, buildInfoDependencies))
            {
                try
                {
                    task.Start();
                    results.Add(task.Result);
                }
                catch (AggregateException ex)
                {
                    // Gather all exceptions to avoid losing info about later ones.
                    ex.Handle(inner =>
                    {
                        var targetNotFound = inner as UpdateTargetNotFoundException;
                        if (targetNotFound != null)
                        {
                            exceptions.Add(targetNotFound);
                            return true;
                        }
                        return false;
                    });
                }
            }

            if (exceptions.Any())
            {
                throw new UpdateTargetNotFoundException(
                    $"Failed to find update targets for {exceptions.Count} updates.",
                    new AggregateException(exceptions));
            }

            IEnumerable<BuildInfo> distinctUsedBuildInfos = results
                .SelectMany(r => r.UsedBuildInfos)
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
