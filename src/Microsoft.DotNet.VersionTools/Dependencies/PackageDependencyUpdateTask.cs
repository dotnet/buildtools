// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Dependencies
{
    /// <summary>
    /// A dependency update task with additional information about the package update.
    /// </summary>
    public class PackageDependencyUpdateTask : DependencyUpdateTask
    {
        public IEnumerable<PackageDependencyChange> Changes { get; }

        public PackageDependencyUpdateTask(
            PackageDependencyChange[] changes,
            string projectJsonFile,
            Action updateAction)
            : base(
                () =>
                {
                    // Check for updates that are needed but can't be performed.
                    bool errors = false;

                    foreach (PackageDependencyChange change in changes.Where(change => change.After == null))
                    {
                        Trace.TraceError($"No update target found for {change} in '{projectJsonFile}'");
                        errors = true;
                    }

                    if (errors)
                    {
                        throw new UpdateTargetNotFoundException(
                            $"Could not find update target for package(s) in '{projectJsonFile}'");
                    }

                    updateAction?.Invoke();
                },
                changes.Select(change => change.BuildInfo),
                changes.Select(change => $"In '{projectJsonFile}', {change.ToString()}"))
        {
            Changes = changes;
        }
    }
}
