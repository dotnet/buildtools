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
        private readonly string _generationIdentifier;

        private Generations(bool useNetPlatform)
        {
            ReferencePaths = new string[0];
            _generationIdentifier = useNetPlatform ? "dotnet" : "netstandard";
        }

        public string[] ReferencePaths { get; set; }

        public static Generations Load(string generationsPath, bool useNetPlatform)
        {
            Generations result;
            string cachePath = $"{generationsPath}:{useNetPlatform}";

            if (s_generationsCache.TryGetValue(cachePath, out result))
                return result;

            result = new Generations(useNetPlatform);

            using (var fileStream = File.OpenRead(generationsPath))
            using (var streamReader = new StreamReader(fileStream))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var json = JToken.Load(jsonReader);

                foreach (var generation in EachProperty(json["generations"]).Select(ReadGeneration))
                {
                    if (useNetPlatform)
                    {
                        // net platform, dotnet, is 4.1 ahead of netstandard.  EG: netstandard1.0 = dotnet5.1, and so on.
                        generation.Version = new Version(generation.Version.Major + 4, generation.Version.Minor + 1, 0, 0);
                    }

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
                log.LogMessage(LogImportance.Low, $"Generation of {assemblyPath} is {_generationIdentifier}{maxGeneration} from cache.");
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

            log.LogMessage(LogImportance.Low, $"Generation of {assemblyPath} is {_generationIdentifier}{maxGeneration}.");
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
                log.LogMessage(LogImportance.Low, $"Assembly {assemblyName} is not tracked by generations.");
            }
            else if (lowerGeneration == null && upperGeneration != null)
            {
                log.LogError($"Could not determine generation of assembly {assemblyName}.  It is lower than the lowest version of the contract supported by any generation.  {assemblyVersion} <= {upperGeneration.Assemblies[assemblyName]}({_generationIdentifier}{upperGeneration.Version}).");
            }
            else if (lowerGeneration != null && upperGeneration == null)
            {
                log.LogMessage(LogImportance.Low, $"Assembly {assemblyName}, {assemblyVersion} is supported by {_generationIdentifier}{lowerGeneration.Version}. {lowerGeneration.Assemblies[assemblyName]}({_generationIdentifier}{lowerGeneration.Version}) <= {assemblyVersion}");
            }
            else
            {
                log.LogMessage(LogImportance.Low, $"Assembly {assemblyName}, {assemblyVersion} is supported by {_generationIdentifier}{lowerGeneration.Version}. {lowerGeneration.Assemblies[assemblyName]}({_generationIdentifier}{lowerGeneration.Version}) <= {assemblyVersion} < { upperGeneration.Assemblies[assemblyName]}({_generationIdentifier}{upperGeneration.Version})");
            }

            return result;
        }

        public static Version DetermineGenerationForFramework(NuGetFramework framework, bool useNetPlatform)
        {
            FrameworkExpander expander = new FrameworkExpander();
            var generationFramework = useNetPlatform ? FrameworkConstants.FrameworkIdentifiers.NetPlatform : FrameworkConstants.FrameworkIdentifiers.NetStandard;
            var generationFxs = expander.Expand(framework).Where(fx => fx.Framework == generationFramework).Select(fx => fx.Version);

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
