﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.VersionTools.Dependencies;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public static class DependencyUpdateUtils
    {
        /// <summary>
        /// Runs the updaters given using given dependency info sources. Returns information about
        /// the updates performed, such as a recommended commit name.
        /// </summary>
        public static DependencyUpdateResults Update(
            IEnumerable<IDependencyUpdater> updaters,
            IEnumerable<IDependencyInfo> externalDependencyInfos,
            Action<IDependencyInfo> modifyUpdaterDependencyInfo = null)
        {
            IEnumerable<DependencyUpdateTask> updateTasks = GetUpdateTasks(
                updaters,
                externalDependencyInfos,
                modifyUpdaterDependencyInfo);

            IDependencyInfo[] distinctUsedDependencyInfos = updateTasks
                .Select(task =>
                {
                    task.Start();
                    return task.Result;
                })
                .SelectMany(results => results.UsedInfos)
                .Distinct()
                .ToArray();

            return new DependencyUpdateResults(distinctUsedDependencyInfos);
        }

        /// <summary>
        /// Returns the tasks necessary to update dependencies.
        /// </summary>
        public static IEnumerable<DependencyUpdateTask> GetUpdateTasks(
            IEnumerable<IDependencyUpdater> updaters,
            IEnumerable<IDependencyInfo> externalDependencyInfos,
            Action<IDependencyInfo> modifyUpdaterDependencyInfo = null)
        {
            IDependencyInfo[] updaterDependencyInfos = updaters
                .OfType<IDependencyInfoProvider>()
                .SelectMany(updater => updater.CreateDependencyInfos())
                .ToArray();

            foreach (var info in updaterDependencyInfos)
            {
                modifyUpdaterDependencyInfo?.Invoke(info);
            }

            var allDependencyInfos = updaterDependencyInfos.Concat(externalDependencyInfos);

            return updaters.SelectMany(updater => updater.GetUpdateTasks(allDependencyInfos));
        }
    }
}
