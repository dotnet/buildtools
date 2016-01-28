// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        private readonly List<Generation> _generations = new List<Generation>();

        private Generations()
        {
            ReferencePaths = new string[0];
        }

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

        public Version DetermineGenerationFromFile(string assemblyPath, ILog log, Version expectedVersion = null, IDictionary<string, string> candidateRefs = null, ICollection<string> ignoredRefs = null)
        {
            Version maxGeneration = null;

            if (_generationCache.TryGetValue(assemblyPath, out maxGeneration))
            {
                log.LogMessage(LogImportance.Low, $"Generation of {assemblyPath} is dotnet{maxGeneration} from cache.");
                return maxGeneration;
            }

            string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
            if (ignoredRefs != null && ignoredRefs.Contains(assemblyName))
            {
                return null;
            }

            using (PEReader peReader = new PEReader(new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read)))
            {
                MetadataReader reader = peReader.GetMetadataReader();
                AssemblyDefinition assemblyDef = reader.GetAssemblyDefinition();

                assemblyName = reader.GetString(assemblyDef.Name);
                if (ignoredRefs != null && ignoredRefs.Contains(assemblyName))
                {
                    return null;
                }

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
                maxGeneration = DetermineGenerationFromSeeds(assemblyName, assemblyDef.Version, log);

                foreach (var handle in reader.AssemblyReferences)
                {
                    AssemblyReference reference = reader.GetAssemblyReference(handle);
                    string referenceName = reader.GetString(reference.Name);

                    if (ignoredRefs != null && ignoredRefs.Contains(referenceName))
                    {
                        continue;
                    }

                    // indirect dependency: prefer the seed value if it exists since we only care about
                    // reference assembly generation for indirect dependencies
                    Version assemblyGeneration = DetermineGenerationFromSeeds(referenceName, reference.Version, log);

                    if (assemblyGeneration == null)
                    {
                        string contractPath = null;
                        if (candidateRefs != null && candidateRefs.TryGetValue(referenceName, out contractPath) &&
                            File.Exists(contractPath))
                        {
                            // traverse the indirect dependencies recursively.
                            assemblyGeneration = DetermineGenerationFromFile(contractPath, log, reference.Version, candidateRefs);
                        }
                        else
                        {
                            log.LogError($"Cannot resolve indirect dependency {referenceName}, Version={reference.Version}");
                        }
                    }

                    if (assemblyGeneration == null)
                    {
                        log.LogError($"Could not determine generation for {referenceName}, {reference.Version}.  File did not exist and isn't a known mapping.");
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

        public Version DetermineGenerationFromSeeds(string assemblyName, Version assemblyVersion, ILog log)
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

        public static Version DetermineGenerationForFramework(NuGetFramework framework)
        {
            FrameworkExpander expander = new FrameworkExpander();

            var generationFxs = expander.Expand(framework).Where(fx => fx.Framework == FrameworkConstants.FrameworkIdentifiers.NetPlatform).Select(fx => fx.Version);

            return generationFxs.Max();
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

                if (!Directory.Exists(contractNamePath))
                {
                    continue;
                }

                // <path>/<name>/<version>/<name>.dll
                if (File.Exists(contractPath = Path.Combine(contractNamePath, assemblyVersion.ToString(), fileName)))
                {
                    break;
                }

                // bugfix version
                string versionDir = Directory.EnumerateDirectories(contractNamePath, $"{assemblyVersion.ToString(2)}.*.0").LastOrDefault();
                if (versionDir != null && File.Exists(contractPath = Path.Combine(versionDir, fileName)))
                {
                    break;
                }

                // nuget paths, just a heuristic.  Could do better here by actually using NugetAPI.
                versionDir = Path.Combine(contractNamePath, assemblyVersion.ToString(3));
                if (!Directory.Exists(versionDir))
                {
                    versionDir = Directory.EnumerateDirectories(contractNamePath, $"{assemblyVersion.ToString(3)}-*").LastOrDefault();
                }

                if (!Directory.Exists(versionDir))
                {
                    versionDir = Directory.EnumerateDirectories(contractNamePath, $"{assemblyVersion.ToString(2)}.*-*").LastOrDefault();
                }

                if (versionDir != null)
                {
                    string libDir = null;

                    if (Directory.Exists(Path.Combine(versionDir, "ref")))
                    {
                        libDir = Directory.EnumerateDirectories(Path.Combine(versionDir, "ref"), "dotnet*").FirstOrDefault();
                    }

                    if (libDir == null && Directory.Exists(Path.Combine(versionDir, "lib")))
                    {
                        libDir = Directory.EnumerateDirectories(Path.Combine(versionDir, "lib"), "dotnet*").FirstOrDefault();
                    }

                    if (libDir != null)
                    {
                        if (File.Exists(contractPath = Path.Combine(libDir, fileName)))
                        {
                            break;
                        }
                    }
                }


                contractPath = null;
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
