// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.VersionTools.Dependencies
{
    public abstract class FileRegexUpdater : IDependencyUpdater
    {
        public string Path { get; set; }
        public Regex Regex { get; set; }
        public string VersionGroupName { get; set; }

        public IEnumerable<DependencyUpdateTask> GetUpdateTasks(IEnumerable<DependencyBuildInfo> dependencyBuildInfos)
        {
            IEnumerable<BuildInfo> usedBuildInfos;
            string newValue = TryGetDesiredValue(dependencyBuildInfos, out usedBuildInfos);

            if (newValue == null)
            {
                Trace.TraceError($"Could not find version information to change '{Path}' with '{Regex}'");
            }
            else
            {
                string originalValue = null;

                Action update = FileUtils.GetUpdateFileContentsTask(
                    Path,
                    contents => ReplaceGroupValue(
                        Regex,
                        contents,
                        VersionGroupName,
                        newValue,
                        out originalValue));

                if (update != null)
                {
                    var messageLines = new[]
                    {
                        $"In '{Path}', '{originalValue}' must be '{newValue}' based on build info " +
                            $"'{string.Join(", ", usedBuildInfos.Select(info => info.Name))}'"
                    };
                    yield return new DependencyUpdateTask(update, usedBuildInfos, messageLines);
                }
            }
        }

        protected abstract string TryGetDesiredValue(
            IEnumerable<DependencyBuildInfo> dependencyBuildInfos,
            out IEnumerable<BuildInfo> usedBuildInfos);

        private static string ReplaceGroupValue(
            Regex regex,
            string input,
            string groupName,
            string newValue,
            out string outOriginalValue)
        {
            string originalValue = null;
            string replacement = regex.Replace(input, m =>
            {
                string match = m.Value;
                Group group = m.Groups[groupName];
                int startIndex = group.Index - m.Index;
                originalValue = group.Value;

                return match
                    .Remove(startIndex, group.Length)
                    .Insert(startIndex, newValue);
            });
            // Assign out to captured variable.
            outOriginalValue = originalValue;
            return replacement;
        }
    }
}
