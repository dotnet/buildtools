// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class Generations
    {
        /// <summary>
        /// A cache of path to generation definition file > Generations objects to avoid 
        /// re-reading multiple times for the same app domain.
        /// </summary>
        private static Dictionary<string, Generations> s_generationsCache = new Dictionary<string, Generations>(StringComparer.OrdinalIgnoreCase); // file paths are case insensitive

        private static HashSet<string> s_ignoredReferences = new HashSet<string>()
        {
            "mscorlib",
            "System.Private.Uri",
            "Windows"
        };

        private readonly List<Generation> _generations = new List<Generation>();

        private Generations()
        { }

        public string[] ReferencePaths { get; set; }

        public static Generations Load(string generationsPath)
        {
            Generations result;
            if (s_generationsCache.TryGetValue(generationsPath, out result))
                return result;

            result = new Generations();

            using (var fileStream = File.OpenRead(generationsPath))
            using (var streamReader = new StreamReader(fileStream))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var json = JToken.Load(jsonReader);

                foreach (var generation in EachProperty(json["generations"]).Select(ReadGeneration))
                {
                    result._generations.Add(generation);
                }
            }

            result._generations.Sort((g1, g2) => g1.Version.CompareTo(g2.Version));

            return result;
        }

        public IEnumerable<Version> KnownGenerations { get { return _generations.Select(g => g.Version); } }

        private Dictionary<string, Version> _generationCache = new Dictionary<string, Version>(StringComparer.OrdinalIgnoreCase);
        private List<string> _cycleStack = new List<string>();

        public Version DetermineGeneration(string assemblyPath, ILog log, Version expectedVersion = null)
        {
            Version maxGeneration = null;

            if (_generationCache.TryGetValue(assemblyPath, out maxGeneration))
            {
                return maxGeneration;
            }

            using (PEReader peReader = new PEReader(new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read)))
            {
                MetadataReader reader = peReader.GetMetadataReader();
                AssemblyDefinition assemblyDef = reader.GetAssemblyDefinition();

                string assemblyName = reader.GetString(assemblyDef.Name);

                // break a circular dependency
                int cycleIndex = _cycleStack.IndexOf(assemblyName);
                if (cycleIndex != -1)
                {
                    log.LogError($"Cycle detected {string.Join(" > ", _cycleStack.Skip(cycleIndex).ToArray())} > {assemblyName}");
                    return null;
                }

                _cycleStack.Add(assemblyName);

                if (expectedVersion != null && !VersionUtility.IsCompatibleApiVersion(expectedVersion, assemblyDef.Version))
                {
                    log.LogError($"Expected version {expectedVersion} for referenced assembly {assemblyPath} but found {assemblyDef.Version}");
                }

                // first determine if the identity itself has a generation.
                maxGeneration = DetermineGeneration(assemblyName, assemblyDef.Version, log);

                foreach (var handle in reader.AssemblyReferences)
                {
                    AssemblyReference reference = reader.GetAssemblyReference(handle);
                    string referenceName = reader.GetString(reference.Name);

                    if (s_ignoredReferences.Contains(referenceName))
                    {
                        continue;
                    }

                    Version assemblyGeneration;

                    string contractPath = LocateReference(referenceName, reference.Version);

                    // traverse the indirect dependencies recursively.
                    if (contractPath != null && File.Exists(contractPath))
                    {
                        assemblyGeneration = DetermineGeneration(contractPath, log, reference.Version);
                    }
                    else
                    {
                        assemblyGeneration = DetermineGeneration(referenceName, reference.Version, log);

                        if (assemblyGeneration == null)
                        {
                            log.LogError($"Could not determine generation for {referenceName}, {reference.Version}.  File did not exist and isn't a known mapping.");
                        }
                    }

                    if (maxGeneration == null)
                    {
                        maxGeneration = assemblyGeneration;
                    }
                    else if (assemblyGeneration != null && assemblyGeneration > maxGeneration)
                    {
                        maxGeneration = assemblyGeneration;
                    }
                }
            }

            log.LogMessage(LogImportance.Low, $"Generation of {assemblyPath} is dotnet{maxGeneration}.");
            _generationCache.Add(assemblyPath, maxGeneration);
            _cycleStack.RemoveAt(_cycleStack.Count - 1);

            return maxGeneration;
        }

        public Version DetermineGeneration(string assemblyName, Version assemblyVersion, ILog log)
        {
            // find the lowest generation that supports this assembly version
            Version result = null;
            Generation lowerGeneration = null, upperGeneration = null;

            foreach (var generation in _generations)
            {
                Version currentGenerationAssemblyVersion = null;
                if (generation.Assemblies.TryGetValue(assemblyName, out currentGenerationAssemblyVersion))
                {
                    if (assemblyVersion < currentGenerationAssemblyVersion)
                    {
                        // this generation supports a higher version of the target assembly
                        // last matching generation is the correct one
                        upperGeneration = generation;
                        break;
                    }

                    // this generation supports at least the target assembly
                    lowerGeneration = generation;
                    result = generation.Version;
                }
            }

            if (lowerGeneration == null && upperGeneration == null)
            {
                log.LogMessage(LogImportance.Low, "Assembly {0} is not tracked by generations.", assemblyName);
            }
            else if (lowerGeneration == null && upperGeneration != null)
            {
                log.LogError("Could not determine generation of assembly {0}.  It is lower than the lowest version of the contract supported by any generation.  {1} <= {2}(dotnet{3}).", assemblyName, assemblyVersion, upperGeneration.Assemblies[assemblyName], upperGeneration.Version);
            }
            else if (lowerGeneration != null && upperGeneration == null)
            {
                log.LogMessage(LogImportance.Low, "Assembly {0}, {1} is supported by dotnet{2}. {3}(dotnet{2}) <= {1}", assemblyName, assemblyVersion, lowerGeneration.Version, lowerGeneration.Assemblies[assemblyName]);
            }
            else
            {
                log.LogMessage(LogImportance.Low, "Assembly {0}, {1} is supported by dotnet{2}. {3}(dotnet{2}) <= {1} < {4}(dotnet{5})", assemblyName, assemblyVersion, lowerGeneration.Version, lowerGeneration.Assemblies[assemblyName], upperGeneration.Assemblies[assemblyName], upperGeneration.Version);
            }

            return result;
        }

        public Version DetermineGeneration(params NuGetFramework[] frameworks)
        {
            var generationFxs = _generations.Select(gen => new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetPlatform, gen.Version));

            FrameworkReducer reducer = new FrameworkReducer();

            Version minGeneration = null;

            foreach (var framework in frameworks)
            {
                NuGetFramework closestGeneration = reducer.GetNearest(framework, generationFxs);

                if (closestGeneration == null)
                {
                    throw new ArgumentException($"{framework} is not compatible with generations.", "frameworks");
                }

                if (minGeneration == null || closestGeneration.Version < minGeneration)
                {
                    minGeneration = closestGeneration.Version;
                }
            }

            return minGeneration;
        }

        public Version DetermineContractVersionForGeneration(string assemblyName, Version targetGeneration)
        {
            Version result = null;
            foreach (var generation in _generations)
            {
                if (generation.Version > targetGeneration)
                {
                    break;
                }

                generation.Assemblies.TryGetValue(assemblyName, out result);
            }

            return result;
        }

        private string LocateReference(string assemblyName, Version assemblyVersion)
        {
            string contractPath = null;
            string fileName = assemblyName + ".dll";

            foreach (string referencePath in ReferencePaths)
            {
                string contractNamePath = Path.Combine(referencePath, assemblyName);

                // <path>/<name>/<version>/<name>.dll
                if (File.Exists(contractPath = Path.Combine(contractNamePath, assemblyVersion.ToString(), fileName)))
                {
                    break;
                }

                // nuget paths, just a heuristic.  Could do better here by actually using NugetAPI.
                // <path>/<name>/<3PartVersion>/ref/dotnet/<name>.dll
                if (File.Exists(contractPath = Path.Combine(contractNamePath, assemblyVersion.ToString(3), "ref", "dotnet", fileName)))
                {
                    break;
                }

                // <path>/<name>/<3PartVersion>/lib/dotnet/<name>.dll
                if (File.Exists(contractPath = Path.Combine(contractNamePath, assemblyVersion.ToString(3), "lib", "dotnet", fileName)))
                {
                    break;
                }

                if (Directory.Exists(contractNamePath))
                {
                    // <path>/<name>/<newerVersion>/<name>.dll
                    string foundVersionDir = Directory.EnumerateDirectories(contractNamePath, assemblyVersion.ToString(2) + ".*")
                        .Where(d => new Version(Path.GetFileName(d)) >= assemblyVersion).FirstOrDefault();

                    if (foundVersionDir != null)
                    {
                        contractPath = Path.Combine(foundVersionDir, fileName);
                        break;
                    }
                }

                contractPath = null;
            }

            if (contractPath == null)
            {
                // do an unversioned check
                foreach (string referencePath in ReferencePaths)
                {
                    // <path>/<name>.dll
                    if (File.Exists(contractPath = Path.Combine(referencePath, fileName)))
                    {
                        break;
                    }

                    contractPath = null;
                }
            }

            return contractPath;
        }

        private static Generation ReadGeneration(KeyValuePair<string, JToken> json)
        {
            var generation = new Generation()
            {
                Version = FrameworkUtilities.Ensure4PartVersion(new Version(json.Key))
            };

            foreach (var assembly in EachProperty(json.Value["assemblies"]))
            {
                generation.Assemblies.Add(assembly.Key, new Version(assembly.Value.ToString()));
            }

            return generation;
        }


        private static IEnumerable<KeyValuePair<string, JToken>> EachProperty(JToken json)
        {
            return (json as IEnumerable<KeyValuePair<string, JToken>>)
                ?? Enumerable.Empty<KeyValuePair<string, JToken>>();
        }
    }

    public class Generation
    {
        public Generation()
        {
            // Assembly names are case sensitive
            Assemblies = new Dictionary<string, Version>(StringComparer.Ordinal);
        }

        public Version Version { get; set; }
        public Dictionary<string, Version> Assemblies { get; private set; }
    }
}
