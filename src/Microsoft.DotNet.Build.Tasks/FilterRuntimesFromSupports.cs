using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.DotNet.Build.Tasks
{
    public class FilterRuntimesFromSupports : Task
    {
        public string[] Frameworks { get; set; }

        [Required]
        public string[] RuntimeIds { get; set; }

        [Required]
        public string RuntimeGraphFile { get; set; }

        [Required]
        public string SupportsFile { get; set; }

        [Required]
        public string InputProjectJson { get; set; }

        public override bool Execute()
        {
            if (!File.Exists(RuntimeGraphFile))
            {
                Log.LogError("Cannot find specified runtime.json - '{0}'", RuntimeGraphFile);
                return false;
            }

            if (!File.Exists(SupportsFile))
            {
                Log.LogError("Cannot find specified runtime.json - '{0}'", RuntimeGraphFile);
                return false;
            }

            RuntimeGraph runtimeGraph = ReadRuntimeGraph(RuntimeGraphFile);
            RuntimeGraph supportsGraph = ReadRuntimeGraph(SupportsFile);

            List<CompatibilityProfile> filteredSupports = new List<CompatibilityProfile>();
            List<CompatibilityProfile> filteredRuntimes = new List<CompatibilityProfile>();

            if (Frameworks!=null && Frameworks.Length > 0)
            {
                foreach (var key in supportsGraph.Supports.Keys)
                {
                    var compat = DefaultCompatibilityProvider.Instance;
                    CompatibilityProfile compatibilityProfile = null;
                    supportsGraph.Supports.TryGetValue(key, out compatibilityProfile);
                    bool isFrameworkFiltered = false;
                    foreach (var tfmRidPair in compatibilityProfile.RestoreContexts)
                    {
                        foreach (string tfm in Frameworks)
                        {
                            if (compat.IsCompatible(NuGetFramework.Parse(tfm), tfmRidPair.Framework))
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
                    List<bool> toRemove = new List<bool>();
                    List<FrameworkRuntimePair> frameworkRuntimePairs = new List<FrameworkRuntimePair>();
                    foreach (var tfmRidPair in compatibilityProfile.RestoreContexts)
                    {
                        bool isRuntimeFiltered = false;
                        foreach (string runtimeId in RuntimeIds)
                        {
                            if (runtimeGraph.AreCompatible(runtimeId, tfmRidPair.RuntimeIdentifier))
                            {
                                isRuntimeFiltered = true;
                                break;
                            }
                        }
                        toRemove.Add(isRuntimeFiltered);
                    }
                    for (int i = 0, k = 0; i < toRemove.Count; i++)
                    {
                        if (!toRemove[i])
                        {
                            compatibilityProfile.RestoreContexts.RemoveAt(k);
                        }
                        else
                        {
                            k++;
                        }
                    }
                    filteredRuntimes.Add(compatibilityProfile);
                }
            }
            else
            {
                filteredRuntimes = filteredSupports;
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
                        map.Add(tfmRidPair.Framework.GetShortFolderName(), new List<string>() { tfmRidPair.RuntimeIdentifier });
                    }
                }
                var tfmRidsForSupports = new JObject();
                foreach (var item in map)
                {
                    tfmRidsForSupports[item.Key] = new JArray(item.Value);
                }
                supportsClause[compatibilityProfile.Name] = tfmRidsForSupports;
            }

            if (projectJson["supports"]!=null)
            {
                projectJson["supports"] = supportsClause;
            }

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


        public static RuntimeGraph ReadRuntimeGraph(string filePath)
        {
            using (var fileStream = File.OpenRead(filePath))
            {
                return ReadRuntimeGraph(fileStream);
            }
        }

        public static RuntimeGraph ReadRuntimeGraph(Stream stream)
        {
            using (var streamReader = new StreamReader(stream))
            {
                return ReadRuntimeGraph(streamReader);
            }
        }

        public static RuntimeGraph ReadRuntimeGraph(TextReader textReader)
        {
            using (var jsonReader = new JsonTextReader(textReader))
            {
                return ReadRuntimeGraph(JToken.Load(jsonReader));
            }
        }

        public static RuntimeGraph ReadRuntimeGraph(JToken json)
        {
            return new RuntimeGraph(
                EachProperty(json["runtimes"]).Select(ReadRuntimeDescription),
                EachProperty(json["supports"]).Select(ReadCompatibilityProfile));
        }

        private static CompatibilityProfile ReadCompatibilityProfile(KeyValuePair<string, JToken> json)
        {
            var name = json.Key;
            var sets = new List<FrameworkRuntimePair>();
            foreach (var property in EachProperty(json.Value))
            {
                var profiles = ReadCompatibilitySets(property);
                sets.AddRange(profiles);
            }
            return new CompatibilityProfile(name, sets);
        }

        private static IEnumerable<FrameworkRuntimePair> ReadCompatibilitySets(KeyValuePair<string, JToken> property)
        {
            var framework = NuGetFramework.Parse(property.Key);
            switch (property.Value.Type)
            {
                case JTokenType.Array:
                    foreach (var value in (JArray)property.Value)
                    {
                        yield return new FrameworkRuntimePair(framework, value.Value<string>());
                    }
                    break;
                case JTokenType.String:
                    yield return new FrameworkRuntimePair(framework, property.Value.ToString());
                    break;
                    // Other token types are not supported
            }
        }

        private static RuntimeDescription ReadRuntimeDescription(KeyValuePair<string, JToken> json)
        {
            var name = json.Key;
            IList<string> inheritedRuntimes = new List<string>();
            IList<RuntimeDependencySet> additionalDependencies = new List<RuntimeDependencySet>();
            foreach (var property in EachProperty(json.Value))
            {
                if (property.Key == "#import")
                {
                    var imports = property.Value as JArray;
                    foreach (var import in imports)
                    {
                        inheritedRuntimes.Add(import.Value<string>());
                    }
                }
                else
                {
                    var dependency = ReadRuntimeDependencySet(property);
                    additionalDependencies.Add(dependency);
                }
            }
            return new RuntimeDescription(name, inheritedRuntimes, additionalDependencies);
        }

        private static RuntimeDependencySet ReadRuntimeDependencySet(KeyValuePair<string, JToken> json)
        {
            return new RuntimeDependencySet(
                json.Key,
                EachProperty(json.Value).Select(ReadRuntimePackageDependency));
        }

        private static RuntimePackageDependency ReadRuntimePackageDependency(KeyValuePair<string, JToken> json)
        {
            return new RuntimePackageDependency(json.Key, VersionRange.Parse(json.Value.Value<string>()));
        }

        private static IEnumerable<KeyValuePair<string, JToken>> EachProperty(JToken json)
        {
            return (json as IEnumerable<KeyValuePair<string, JToken>>)
                   ?? Enumerable.Empty<KeyValuePair<string, JToken>>();
        }

        private static IEnumerable<KeyValuePair<string, JToken>> EachProperty(JToken json, string defaultPropertyName)
        {
            return (json as IEnumerable<KeyValuePair<string, JToken>>)
                   ?? new[] { new KeyValuePair<string, JToken>(defaultPropertyName, json) };
        }

        private static IEnumerable<JToken> EachArray(JToken json)
        {
            return (IEnumerable<JToken>)(json as JArray)
                   ?? new[] { json };
        }
    }

}