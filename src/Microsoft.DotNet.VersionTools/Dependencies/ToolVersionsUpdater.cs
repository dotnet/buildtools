// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Dependencies
{
    public class ToolVersionsUpdater : IDependencyUpdater
    {
        public string Path { get; set; }

        public IEnumerable<DependencyUpdateTask> GetUpdateTasks(
            IEnumerable<DependencyBuildInfo> dependencyBuildInfos)
        {
            var lineResults = new List<ToolUpdateLineResult>();

            Action updateTask = FileUtils.GetUpdateFileContentsTask(
                Path,
                content =>
                {
                    var reader = new StringReader(content);
                    var writer = new StringWriter();

                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var result = new ToolUpdateLineResult(line, dependencyBuildInfos);
                        writer.WriteLine(result.Content);
                        if (result.UsedBuildInfo != null)
                        {
                            lineResults.Add(result);
                        }
                    }

                    return writer.ToString();
                });

            if (updateTask != null)
            {
                yield return new DependencyUpdateTask(
                    updateTask,
                    lineResults.Select(c => c.UsedBuildInfo),
                    lineResults.Select(c => $"In '{Path}', '{c.ToolName}' '{c.OriginalVersion}' must be '{c.NewVersion}'."));
            }
        }

        private class ToolUpdateLineResult
        {
            public string Content { get; }

            public BuildInfo UsedBuildInfo { get; }

            public string ToolName { get; }

            public string OriginalVersion { get; }

            public string NewVersion { get; }

            public ToolUpdateLineResult(string line, IEnumerable<DependencyBuildInfo> buildInfos)
            {
                int separatorIndex = line.IndexOf('=');
                string name = line.Substring(0, separatorIndex);
                string version = line.Substring(separatorIndex + 1);

                Content = line;
                OriginalVersion = version;
                ToolName = name;

                foreach (var info in buildInfos)
                {
                    if (info.RawPackages.TryGetValue(name, out version))
                    {
                        Content = $"{name}={version}";
                        NewVersion = version;
                        UsedBuildInfo = info.BuildInfo;
                        break;
                    }
                }
            }
        }
    }
}
