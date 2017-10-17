// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Dependencies.BuildOutput
{
    public class FilePackageUpdater : IDependencyUpdater
    {
        public string Path { get; set; }

        public string PackageId { get; set; }

        public IEnumerable<DependencyUpdateTask> GetUpdateTasks(
            IEnumerable<IDependencyInfo> dependencyInfos)
        {
            foreach (BuildDependencyInfo info in dependencyInfos.OfType<BuildDependencyInfo>())
            {
                string version;
                if (info.RawPackages.TryGetValue(PackageId, out version))
                {
                    string originalValue = null;

                    Action updateTask = FileUtils.GetUpdateFileContentsTask(
                        Path,
                        content =>
                        {
                            int firstLineLength = content.IndexOf(Environment.NewLine);
                            // Handle files with no newline ending.
                            if (firstLineLength == -1)
                            {
                                firstLineLength = content.Length;
                            }

                            originalValue = content.Substring(0, firstLineLength);
                            return content
                                .Remove(0, firstLineLength)
                                .Insert(0, version);
                        });

                    if (updateTask != null)
                    {
                        yield return new DependencyUpdateTask(
                            updateTask,
                            new[] { info },
                            new[] { $"In '{Path}', '{originalValue}' must be '{version}'." });
                    }
                    yield break;
                }
            }
            Trace.TraceError($"For '{Path}', Could not find '{PackageId}' package version information.");
        }
    }
}
