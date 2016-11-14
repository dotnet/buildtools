using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using Microsoft.DotNet.Build.Common.Desktop;
using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.DotNet.Build.Tasks
{
    public class FilterRuntimesFromSupports : Task
    {
        public string[] Frameworks { get; set; }

        [Required]
        public string[] RuntimeIds { get; set; }

        [Required]
        public string SupportsFile { get; set; }

        [Required]
        public string InputProjectJson { get; set; }

        public override bool Execute()
        {
            AssemblyResolver.Enable();

            if (!File.Exists(SupportsFile))
            {
                Log.LogError("Cannot find specified runtime.json - '{0}'", SupportsFile);
                return false;
            }

            Debugger.Launch();

            RuntimeGraph supportsGraph = JsonRuntimeFormat.ReadRuntimeGraph(SupportsFile);

            List<CompatibilityProfile> filteredSupports = new List<CompatibilityProfile>();
            List<CompatibilityProfile> filteredRuntimes = new List<CompatibilityProfile>();

            if (Frameworks != null && Frameworks.Length > 0)
            {
                foreach (var key in supportsGraph.Supports.Keys)
                {
                    CompatibilityProfile compatibilityProfile = null;
                    supportsGraph.Supports.TryGetValue(key, out compatibilityProfile);
                    bool isFrameworkFiltered = false;
                    foreach (var tfmRidPair in compatibilityProfile.RestoreContexts)
                    {
                        foreach (string tfm in Frameworks)
                        {
                            if (NuGetFramework.Parse(tfm).Equals(tfmRidPair.Framework))
                            {
                                isFrameworkFiltered = true;
                                filteredSupports.Add(compatibilityProfile);
                                break;
                            }
                        }
                        if (isFrameworkFiltered)
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                foreach (var key in supportsGraph.Supports.Keys)
                {
                    CompatibilityProfile compatibilityProfile = null;
                    supportsGraph.Supports.TryGetValue(key, out compatibilityProfile);
                    filteredSupports.Add(compatibilityProfile);
                }
            }

            if (RuntimeIds.Length > 0)
            {
                foreach (var compatibilityProfile in filteredSupports)
                {
                    for (int i = compatibilityProfile.RestoreContexts.Count - 1; i >= 0; i--)
                    {
                        foreach (string runtimeId in RuntimeIds)
                        {
                            if (!runtimeId.Equals(compatibilityProfile.RestoreContexts[i].RuntimeIdentifier))
                            {
                                compatibilityProfile.RestoreContexts.RemoveAt(i);
                            }
                        }
                    }
                    if (compatibilityProfile.RestoreContexts.Count > 0)
                    {
                        filteredRuntimes.Add(compatibilityProfile);
                    }
                }
            }
            else
            {
                filteredRuntimes = filteredSupports;
            }

            if (filteredRuntimes.Count == 0)
            {
                Log.LogError(
                    "No compatible runtimes found in supports. Please select correct tfm/rid combination to restore or add it to the TestSuite runtime.json and test-runtime proejct.json.");
            }

            JObject projectJson = ReadProject(InputProjectJson);
            JObject supportsClause = new JObject();
            foreach (var compatibilityProfile in filteredRuntimes)
            {
                Dictionary<string, List<string>> map = new Dictionary<string, List<string>>();
                foreach (var tfmRidPair in compatibilityProfile.RestoreContexts)
                {
                    if (map.ContainsKey(tfmRidPair.Framework.GetShortFolderName()))
                    {
                        List<string> str = new List<string>();
                        map.TryGetValue(tfmRidPair.Framework.GetShortFolderName(), out str);
                        str.Add(tfmRidPair.RuntimeIdentifier);
                    }
                    else
                    {
                        map.Add(tfmRidPair.Framework.GetShortFolderName(),
                            new List<string>() {tfmRidPair.RuntimeIdentifier});
                    }
                }
                var tfmRidsForSupports = new JObject();
                foreach (var item in map)
                {
                    tfmRidsForSupports[item.Key] = new JArray(item.Value);
                }
                supportsClause[compatibilityProfile.Name] = tfmRidsForSupports;
            }

            projectJson["supports"] = supportsClause;

            string projectJsonRoot = JsonConvert.SerializeObject(projectJson, Formatting.Indented) + Environment.NewLine;

            if (!projectJson.Equals(File.ReadAllText(InputProjectJson)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(InputProjectJson));
                File.WriteAllText(InputProjectJson, projectJsonRoot);
            }

            return true;
        }

        private static JObject ReadProject(string projectJsonPath)
        {
            using (TextReader projectFileReader = File.OpenText(projectJsonPath))
            {
                var projectJsonReader = new JsonTextReader(projectFileReader);
                var serializer = new JsonSerializer();
                return serializer.Deserialize<JObject>(projectJsonReader);
            }
        }
    }

}