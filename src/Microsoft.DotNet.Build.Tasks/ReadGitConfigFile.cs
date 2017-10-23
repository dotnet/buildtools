// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public class ReadGitConfigFile : Task
    {
        [Required]
        public string File { get; set; }

        /// <summary>
        /// An item for each submodule section that has "Path" and "Url" variables. Includes
        /// certain variable values as metadata.
        /// 
        /// ItemSpec = subsection (submodule name)
        /// Path = value of the "path" variable
        /// Url = value of the "url" variable
        /// Branch = value of the "branch" variable, or empty string
        /// VersionToolsAutoUpdate = value of the "versionToolsAutoUpdate" variable, or empty string
        /// </summary>
        [Output]
        public ITaskItem[] SubmoduleConfiguration { get; set; }

        public override bool Execute()
        {
            var r = new Exec
            {
                BuildEngine = BuildEngine,
                Command = $"git config --file {File} --list",
                ConsoleToMSBuild = true
            };
            if (!r.Execute())
            {
                return false;
            }

            var options = r.ConsoleOutput
                .Select(item => ConfigOption.Parse(item.ItemSpec))
                .ToArray();

            SubmoduleConfiguration = options
                .Where(o => o.Section == "submodule")
                .GroupBy(o => o.Subsection)
                .Select(g =>
                {
                    string path = g.FirstOrDefault(o => o.Name == "path")?.Value;
                    string url = g.FirstOrDefault(o => o.Name == "url")?.Value;

                    if (string.IsNullOrEmpty(path) ||
                        string.IsNullOrEmpty(url))
                    {
                        return null;
                    }

                    string branch = g.FirstOrDefault(o => o.Name == "branch")?.Value ?? string.Empty;

                    // "git --list" converts entries to lowercase, but camelCase is standard
                    // capitalization within the config file.
                    string versionToolsAutoUpdate = g
                        .FirstOrDefault(o => string.Equals(o.Name, "versionToolsAutoUpdate", StringComparison.OrdinalIgnoreCase))
                        ?.Value ?? string.Empty;

                    return new TaskItem(
                        g.Key,
                        new Dictionary<string, string>
                        {
                            ["Path"] = path,
                            ["Url"] = url,
                            ["Branch"] = branch,
                            ["VersionToolsAutoUpdate"] = versionToolsAutoUpdate
                        });
                })
                .Where(item => item != null)
                .ToArray();

            return true;
        }

        private class ConfigOption
        {
            /// <summary>
            /// Parse a "git config" line.
            /// 
            /// There must be no '=' in the section, subsection, or name.
            /// There must be no '.' in the section or name.
            /// </summary>
            /// <remarks>
            /// Expected lines look like:
            ///   "submodule.src/sdk.path=src/sdk",
            ///   "submodule.src/sdk.url=https://github.com/dotnet/sdk.git",
            ///   "submodule.src/sdk.branch=release/2.0.0"
            /// </remarks>
            public static ConfigOption Parse(string line)
            {
                int valueCut = line.IndexOf('=');
                string sectionAndName = line.Substring(0, valueCut);
                string value = line.Substring(valueCut + 1);

                int nameCut = sectionAndName.LastIndexOf('.');
                string sectionAndSubsection = sectionAndName.Substring(0, nameCut);
                string name = sectionAndName.Substring(nameCut + 1);

                int subsectionCut = sectionAndSubsection.LastIndexOf('.');
                string section;
                string subsection;
                if (subsectionCut > -1)
                {
                    section = sectionAndSubsection.Substring(0, subsectionCut);
                    subsection = sectionAndSubsection.Substring(subsectionCut + 1);
                }
                else
                {
                    section = sectionAndSubsection;
                    subsection = string.Empty;
                }

                return new ConfigOption(section, subsection, name, value);
            }

            public string Section { get; }

            public string Subsection { get; }

            public string Name { get; }

            public string Value { get; }

            public ConfigOption(string section, string subsection, string name, string value)
            {
                Section = section;
                Name = name;
                Value = value;
                Subsection = subsection;
            }
        }
    }
}
