// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Util;
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

        public IEnumerable<BuildInfo> Update(IEnumerable<BuildInfo> buildInfos)
        {
            IEnumerable<BuildInfo> usedBuildInfos;
            string newValue = TryGetDesiredValue(buildInfos, out usedBuildInfos);

            if (newValue == null)
            {
                Trace.TraceError($"Could not find version information to change '{Path}' with '{Regex}'");
            }
            else
            {
                bool replaced = FileUtils.ReplaceFileContents(
                    Path,
                    contents => ReplaceGroupValue(Regex, contents, VersionGroupName, newValue));

                if (replaced)
                {
                    return usedBuildInfos;
                }
            }
            return Enumerable.Empty<BuildInfo>();
        }

        protected abstract string TryGetDesiredValue(
            IEnumerable<BuildInfo> buildInfos,
            out IEnumerable<BuildInfo> usedBuildInfos);

        private static string ReplaceGroupValue(Regex regex, string input, string groupName, string newValue)
        {
            return regex.Replace(input, m =>
            {
                string replacedValue = m.Value;
                Group group = m.Groups[groupName];
                int startIndex = group.Index - m.Index;

                replacedValue = replacedValue.Remove(startIndex, group.Length);
                replacedValue = replacedValue.Insert(startIndex, newValue);

                return replacedValue;
            });
        }
    }
}
