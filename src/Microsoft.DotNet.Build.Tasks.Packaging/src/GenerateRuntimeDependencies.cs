// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class GenerateRuntimeDependencies : PackagingTask
    {
        private const string c_emptyDependency = "none";

        [Required]
        public ITaskItem[] Dependencies
        {
            get;
            set;
        }

        [Required]
        public string PackageId
        {
            get;
            set;
        }

        public ITaskItem RuntimeJsonTemplate
        {
            get;
            set;
        }

        [Required]
        public ITaskItem RuntimeJson
        {
            get;
            set;
        }

        public bool EnsureBase
        {
            get;
            set;
        }


        public override bool Execute()
        {
            if (Dependencies == null || Dependencies.Length == 0)
            {
                Log.LogError("Dependencies argument must be specified");
                return false;
            }

            if (String.IsNullOrEmpty(PackageId))
            {
                Log.LogError("PackageID argument must be specified");
                return false;
            }

            if (RuntimeJson == null)
            {
                Log.LogError("RuntimeJson argument must be specified");
                return false;
            }

            RuntimeFileFormatter formatter = new RuntimeFileFormatter();
            RuntimeFile runtimeFile = null;
            string sourceRuntimeFilePath = null;
            if (RuntimeJsonTemplate != null)
            {
                sourceRuntimeFilePath = RuntimeJsonTemplate.GetMetadata("FullPath");
            }
            string destRuntimeFilePath = RuntimeJson.GetMetadata("FullPath");

            // read in existing JSON, if it was provided so that we preserve any 
            // hand authored #imports or dependencies
            if (!String.IsNullOrEmpty(sourceRuntimeFilePath))
            {
                runtimeFile = formatter.ReadRuntimeFile(sourceRuntimeFilePath);
            }
            else
            {
                runtimeFile = new RuntimeFile();
            }

            Dictionary<string, string> packageAliases = new Dictionary<string, string>();
            foreach (var dependency in Dependencies)
            {
                string alias = dependency.GetMetadata("PackageAlias");

                if (String.IsNullOrEmpty(alias))
                {
                    continue;
                }

                Log.LogMessage(LogImportance.Low, "Aliasing {0} -> {1}", alias, dependency.ItemSpec);
                packageAliases[alias] = dependency.ItemSpec;
            }

            foreach (var dependency in Dependencies)
            {
                string targetRuntimeId = dependency.GetMetadata("TargetRuntime");
                string targetPackageId = dependency.GetMetadata("TargetPackage");
                string targetPackageAlias = dependency.GetMetadata("TargetPackageAlias");
                string dependencyId = dependency.ItemSpec;
                string dependencyVersion = dependency.GetMetadata("version");

                if (String.IsNullOrEmpty(targetRuntimeId))
                {
                    Log.LogMessage(LogImportance.Low, "Skipping dependency {0} since it doesn't have a TargetRuntime.", dependency.ItemSpec);
                    continue;
                }

                if (!String.IsNullOrEmpty(targetPackageAlias) && !packageAliases.TryGetValue(targetPackageAlias, out targetPackageId))
                {
                    Log.LogWarning("Dependency {0} specified TargetPackageAlias {1} but no package was found defining this alias.", dependency.ItemSpec, targetPackageAlias);
                }
                else
                {
                    Log.LogMessage(LogImportance.Low, "Using {0} for TargetPackageAlias {1}", targetPackageId, targetPackageAlias);
                }

                RuntimeSpec targetRuntime = null;
                if (!runtimeFile.Runtimes.TryGetValue(targetRuntimeId, out targetRuntime))
                {
                    targetRuntime = new RuntimeSpec() { Name = targetRuntimeId };
                    runtimeFile.Runtimes.Add(targetRuntimeId, targetRuntime);
                }

                if (String.IsNullOrEmpty(targetPackageId))
                {
                    Log.LogMessage(LogImportance.Low, "Dependency {0} has no parent so will assume {1}.", dependency.ItemSpec, PackageId);
                    targetPackageId = PackageId;
                }

                DependencySpec targetPackage = null;
                if (!targetRuntime.Dependencies.TryGetValue(targetPackageId, out targetPackage))
                {
                    targetPackage = new DependencySpec() { Name = targetPackageId };
                    targetRuntime.Dependencies.Add(targetPackageId, targetPackage);
                }

                if (dependencyId == c_emptyDependency)
                {
                    targetPackage.Implementations.Clear();
                }
                else
                {
                    if (String.IsNullOrEmpty(dependencyVersion))
                    {
                        Log.LogWarning("Dependency {0} has no version", dependency.ItemSpec);
                    }

                    ImplementationSpec existing;

                    if (targetPackage.Implementations.TryGetValue(dependencyId, out existing))
                    {
                        string newVersion = CompareSemanticVersion(dependencyVersion, existing.Version) > 0 ? dependencyVersion : existing.Version;
                        Log.LogMessage(LogImportance.Low, "Dependency {0} has been added more than once, {1}, {2}, using {3}", dependencyId, existing.Version, dependencyVersion, newVersion);
                        dependencyVersion = newVersion;
                    }

                    targetPackage.Implementations[dependencyId] = new ImplementationSpec() { Name = dependencyId, Version = dependencyVersion };
                }
            }

            if (EnsureBase)
            {
                // RID base is used to lift the library packages up to a baseline version
                // we don't want to obscure these associations or else we may bring in
                // old reference packages prior to an implementation package split
                // and thus, clashing implementations.

                // EG: Version 1 of System.Banana had a single implementation that was
                //     windows specific.
                //     Version 2 split the implementation to Windows and Unix
                //     If Version 1 was referenced we'd get both the V1 windows implementation
                //     and the V2 unix implementation when resolving for Unix

                // To avoid this problem we always ensure that we have the matching
                // reference package in the runtime graph.

                RuntimeSpec baseRuntime = null;
                if (runtimeFile.Runtimes.TryGetValue("base", out baseRuntime))
                {
                    // look at all sections other than base
                    foreach (var runtime in runtimeFile.Runtimes.Values.Where(rt => rt != baseRuntime))
                    {
                        // examine each dependency and copy the content from base, if it exists
                        foreach (var dependency in runtime.Dependencies)
                        {
                            string packageName = dependency.Key;
                            DependencySpec packageDependencies = dependency.Value;

                            // are there any entries for this package in base?
                            DependencySpec baseDependencies = null;
                            if (!baseRuntime.Dependencies.TryGetValue(dependency.Key, out baseDependencies))
                            {
                                continue;
                            }

                            // copy all entries from base to this runtime
                            foreach (var baseImplementation in baseDependencies.Implementations)
                            {
                                packageDependencies.Implementations.Add(baseImplementation.Key, baseImplementation.Value);
                            }
                        }
                    }
                }
            }

            string destRuntimeFileDir = Path.GetDirectoryName(destRuntimeFilePath);
            if (!String.IsNullOrEmpty(destRuntimeFileDir) && !Directory.Exists(destRuntimeFileDir))
            {
                Directory.CreateDirectory(destRuntimeFileDir);
            }

            formatter.WriteRuntimeFile(destRuntimeFilePath, runtimeFile);

            return true;
        }

        private static char[] s_splitChars = new[] { '-' };
        private static int CompareSemanticVersion(string lhs, string rhs)
        {
            // LHS < RHS : result < 0 
            // LHS = RHS : result = 0
            // LHS > RHS : result > 0

            if (lhs == rhs)
            {
                return 0;
            }

            if (lhs == null)
            {
                // LHS < RHS
                // null is less than any version
                return -1;
            }

            if (rhs == null)
            {
                // LHS > RHS
                // any version is greater than null
                return 1;
            }

            string[] lhsSplit = lhs.Split(s_splitChars, 2);
            string[] rhsSplit = rhs.Split(s_splitChars, 2);

            Version lhsVersion = new Version(lhsSplit[0]);
            Version rhsVersion = new Version(rhsSplit[0]);

            if (lhsVersion > rhsVersion)
            {
                // LHS > RHS
                return 1;
            }

            if (lhsVersion < rhsVersion)
            {
                // LHS < RHS
                return -1;
            }

            // equal versions
            string lhsPrerelease = null;
            if (lhsSplit.Length > 1)
                lhsPrerelease = lhsSplit[1];

            string rhsPrerelease = null;
            if (rhsSplit.Length > 1)
                rhsPrerelease = rhsSplit[1];

            Debug.Assert(lhsPrerelease != rhsPrerelease, "fast path should have caught this");

            if (lhsPrerelease == null)
            {
                // LHS (stable) > RHS (prerelease)
                // stable is greater than prerelease
                return 1;
            }

            if (rhsPrerelease == null)
            {
                // LHS (prerelease) < RHS (stable)
                // prerelease is less than stable
                return -1;
            }

            return String.Compare(lhsPrerelease, rhsPrerelease, StringComparison.OrdinalIgnoreCase);
        }
    }


    #region Runtime model
    // copied from https://github.com/aspnet/dnx.git 
    //       > dnx/src/Microsoft.Framework.PackageManager/Restore/RuntimeModel/RuntimeFile.cs
    //       > dnx/src/Microsoft.Framework.PackageManager/Restore/RuntimeModel/RuntimeFormatter.cs
    // Ported to use C# 5.0
    // currently not built as a PCL so we have to copy the source.

    public class RuntimeFile
    {
        public RuntimeFile()
        {
            Runtimes = new Dictionary<string, RuntimeSpec>();
            Supports = new Dictionary<string, SupportSpec>();
        }

        public Dictionary<string, RuntimeSpec> Runtimes { get; set; }
        public Dictionary<string, SupportSpec> Supports { get; set; }
    }

    public class RuntimeSpec
    {
        public RuntimeSpec()
        {
            Import = new List<string>();
            Dependencies = new Dictionary<string, DependencySpec>();
        }

        public string Name { get; set; }
        public List<string> Import { get; set; }
        public Dictionary<string, DependencySpec> Dependencies { get; set; }
    }

    public class SupportSpec
    {
        public SupportSpec()
        {
            FrameworkRuntimeTuples = new Dictionary<string, string[]>();
        }

        public string Name { get; set; }
        public Dictionary<string, string[]> FrameworkRuntimeTuples { get; set; }
    }

    public class DependencySpec
    {
        public DependencySpec()
        {
            Implementations = new Dictionary<string, ImplementationSpec>();
        }

        public string Name { get; set; }
        public Dictionary<string, ImplementationSpec> Implementations { get; set; }
    }

    public class ImplementationSpec
    {
        public string Name { get; set; }
        public string Version { get; set; }
    }

    public class RuntimeFileFormatter
    {
        public RuntimeFile ReadRuntimeFile(string filePath)
        {
            using (var fileStream = File.OpenRead(filePath))
            {
                using (var streamReader = new StreamReader(fileStream))
                {
                    return ReadRuntimeFile(streamReader);
                }
            }
        }

        public RuntimeFile ReadRuntimeFile(TextReader textReader)
        {
            using (var jsonReader = new JsonTextReader(textReader))
            {
                return ReadRuntimeFile(JToken.Load(jsonReader));
            }
        }

        public void WriteRuntimeFile(string filePath, RuntimeFile runtimeFile)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                using (var textWriter = new StreamWriter(fileStream))
                {
                    using (var jsonWriter = new JsonTextWriter(textWriter))
                    {
                        jsonWriter.Formatting = Formatting.Indented;
                        var json = new JObject();
                        WriteRuntimeFile(json, runtimeFile);
                        json.WriteTo(jsonWriter);
                    }
                }
            }
        }

        public RuntimeFile ReadRuntimeFile(JToken json)
        {
            var file = new RuntimeFile();
            foreach (var runtimeSpec in EachProperty(json["runtimes"]).Select(ReadRuntimeSpec))
            {
                file.Runtimes.Add(runtimeSpec.Name, runtimeSpec);
            }
            foreach (var supportSpec in EachProperty(json["supports"]).Select(ReadSupportSpec))
            {
                file.Supports.Add(supportSpec.Name, supportSpec);
            }
            return file;
        }

        private void WriteRuntimeFile(JObject json, RuntimeFile runtimeFile)
        {
            var runtimes = new JObject();
            json["runtimes"] = runtimes;
            foreach (var x in runtimeFile.Runtimes.Values)
            {
                WriteRuntimeSpec(runtimes, x);
            }

            if (runtimeFile.Supports.Count > 0)
            {
                var supports = new JObject();
                json["supports"] = supports;
                foreach(var s in runtimeFile.Supports.Values)
                {
                    WriteSupportSpec(supports, s);
                }
            }
        }

        private void WriteSupportSpec(JObject json, SupportSpec data)
        {
            var value = new JObject();
            json[data.Name] = value;
            
            if (data.FrameworkRuntimeTuples.Count > 0)
            {
                foreach(var fr in data.FrameworkRuntimeTuples)
                {
                    value[fr.Key] = new JArray(fr.Value);
                }
            }
        }

        private void WriteRuntimeSpec(JObject json, RuntimeSpec data)
        {
            var value = new JObject();
            json[data.Name] = value;
            if (data.Import.Count > 0)
            {
                value["#import"] = new JArray(data.Import.Select(x => new JValue(x)));
            }
            foreach (var x in data.Dependencies.Values)
            {
                WriteDependencySpec(value, x);
            }
        }

        private void WriteDependencySpec(JObject json, DependencySpec data)
        {
            var value = new JObject();
            json[data.Name] = value;
            foreach (var x in data.Implementations.Values)
            {
                WriteImplementationSpec(value, x);
            }
        }

        private void WriteImplementationSpec(JObject json, ImplementationSpec data)
        {
            json[data.Name] = new JValue(data.Version);
        }
        private SupportSpec ReadSupportSpec(KeyValuePair<string, JToken> json)
        {
            var support = new SupportSpec();
            support.Name = json.Key;
            foreach(var property in EachProperty(json.Value))
            {
                support.FrameworkRuntimeTuples.Add(property.Key,
                    EachArray(property.Value).Select(j => j.ToString()).ToArray());
            }
            return support;
        }

        private RuntimeSpec ReadRuntimeSpec(KeyValuePair<string, JToken> json)
        {
            var runtime = new RuntimeSpec();
            runtime.Name = json.Key;
            foreach (var property in EachProperty(json.Value))
            {
                if (property.Key == "#import")
                {
                    var imports = property.Value as JArray;
                    foreach (var import in imports)
                    {
                        runtime.Import.Add(import.Value<string>());
                    }
                }
                else
                {
                    var dependency = ReadDependencySpec(property);
                    runtime.Dependencies.Add(dependency.Name, dependency);
                }
            }
            return runtime;
        }

        public DependencySpec ReadDependencySpec(KeyValuePair<string, JToken> json)
        {
            var dependency = new DependencySpec();
            dependency.Name = json.Key;
            foreach (var implementation in EachProperty(json.Value).Select(ReadImplementationSpec))
            {
                dependency.Implementations.Add(implementation.Name, implementation);
            }
            return dependency;
        }

        public ImplementationSpec ReadImplementationSpec(KeyValuePair<string, JToken> json)
        {
            var implementation = new ImplementationSpec();
            implementation.Name = json.Key;
            foreach (var property in EachProperty(json.Value, "version"))
            {
                if (property.Key == "version")
                {
                    implementation.Version = property.Value.ToString();
                }
            }
            return implementation;
        }

        private IEnumerable<KeyValuePair<string, JToken>> EachProperty(JToken json)
        {
            return (json as IEnumerable<KeyValuePair<string, JToken>>)
                ?? Enumerable.Empty<KeyValuePair<string, JToken>>();
        }

        private IEnumerable<KeyValuePair<string, JToken>> EachProperty(JToken json, string defaultPropertyName)
        {
            return (json as IEnumerable<KeyValuePair<string, JToken>>)
                ?? new[] { new KeyValuePair<string, JToken>(defaultPropertyName, json) };
        }

        private IEnumerable<JToken> EachArray(JToken json)
        {
            return (IEnumerable<JToken>)(json as JArray)
                ?? new[] { json };
        }
    }
    #endregion
}
