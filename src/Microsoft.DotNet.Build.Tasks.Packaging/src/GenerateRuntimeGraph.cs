// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Newtonsoft.Json;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class GenerateRuntimeGraph : PackagingTask
    {

        /// <summary>
        /// A set of RuntimeGroups that can be used to generate a runtime graph
        ///   Identity: the base string for the RID, without version architecture, or qualifiers.
        ///   Parent: the base string for the parent of this RID.  This RID will be imported by the baseRID, architecture-specific, 
        ///     and qualifier-specific RIDs (with the latter two appending appropriate architecture and qualifiers).
        ///   Versions: A list of strings delimited by semi-colons that represent the versions for this RID.
        ///   TreatVersionsAsCompatible: Default is true.  When true, version-specific RIDs will import the previous 
        ///     version-specific RID in the Versions list, with the first version importing the version-less RID.  
        ///     When false all version-specific RIDs will import the version-less RID (bypassing previous version-specific RIDs)
        ///   OmitVersionDelimiter: Default is false.  When true no characters will separate the base RID and version (EG: win7).
        ///     When false a '.' will separate the base RID and version (EG: osx.10.12).
        ///   ApplyVersionsToParent: Default is false.  When true, version-specific RIDs will import version-specific Parent RIDs
        ///     similar to is done for architecture and qualifier (see Parent above).
        ///   Architectures: A list of strings delimited by semi-colons that represent the architectures for this RID.
        ///   AdditionalQualifiers: A list of strings delimited by semi-colons that represent the additional qualifiers for this RID.
        ///     Additional qualifers do not stack, each only applies to the qualifier-less RIDs (so as not to cause combinatorial 
        ///     exponential growth of RIDs).
        ///   OmitRIDs: A list of strings delimited by semi-colons that represent RIDs calculated from this RuntimeGroup that should
        ///     be omitted from the RuntimeGraph.  This is useful in cases where overlapping RuntimeGroups are used and one is 
        ///     designated to be "best" for some set of calculated RIDs, the others may omit the overlapping RIDs.
        /// </summary>
        public ITaskItem[] RuntimeGroups
        {
            get;
            set;
        }

        /// <summary>
        /// A set of QualifierPolicies that can be used to define the ordering of qualifer-specific RIDs in imports.
        ///   Identity: the qualifier name.  This should match what is used in RuntimeGroups.AdditionalQualifiers
        ///   VersionPrecedence: default is false.  When set to true, a qualifier-specific RID should import the 
        ///     qualifier-less RID before any other RIDs that are not of the same version.  When false, the qualifier-
        ///     less RID should be imported after all qualifier specific RIDs.
        ///     
        ///     Given the RID of  foo.1.0-x64 : [ foo.1.0, foo.0.8-x64 ]
        ///     Applying the qualifier "bar" the following is the result for each value of VersionPrecedence:
        ///       True: foo.1.0-x64-bar : [ foo.1.0-bar, foo.1.0-x64, foo.0.8-x64-bar ]
        ///       False: foo.1.0-x64-bar : [ foo.1.0-bar, foo.0.8-x64-bar, foo.1.0-x64 ]
        /// </summary>
        public ITaskItem[] QualifierPolicies
        {
            get;
            set;
        }


        /// <summary>
        /// Optional source Runtime.json to use as a starting point when merging additional RuntimeGroups
        /// </summary>
        public ITaskItem SourceRuntimeJson
        {
            get;
            set;
        }

        /// <summary>
        /// Where to write the final runtime.json
        /// </summary>
        public ITaskItem RuntimeJson
        {
            get;
            set;
        }

        /// <summary>
        /// When defined, specifies the file to write compatibility precedence for each RID in the graph.
        /// </summary>
        public ITaskItem CompatibilityMap
        {
            get;
            set;
        }

        /// <summary>
        /// When defined, specifies the file to write a DGML representation of the runtime graph.
        /// </summary>
        public ITaskItem RuntimeDirectedGraph
        {
            get;
            set;
        }

        private Dictionary<string, QualifierPolicy> qualifierPolicies = new Dictionary<string, QualifierPolicy>();

        public override bool Execute()
        {

            if (RuntimeGroups != null && RuntimeGroups.Any() && RuntimeJson == null)
            {
                Log.LogError($"{nameof(RuntimeJson)} argument must be specified when {nameof(RuntimeGroups)} is specified.");
                return false;
            }

            RuntimeGraph runtimeGraph;
            if (SourceRuntimeJson != null)
            {
                var sourceRuntimeFilePath = SourceRuntimeJson.GetMetadata("FullPath");

                if (!File.Exists(sourceRuntimeFilePath))
                {
                    Log.LogError($"{nameof(SourceRuntimeJson)} did not exist at {sourceRuntimeFilePath}.");
                    return false;
                }

                runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(sourceRuntimeFilePath);
            }
            else
            {
                runtimeGraph = new RuntimeGraph();
            }

            foreach(var qualifierPolicy in QualifierPolicies.NullAsEmpty().Select(i => new QualifierPolicy(i)))
            {
                qualifierPolicies[qualifierPolicy.Qualifier] = qualifierPolicy;
            }
            
            foreach(var runtimeGroup in RuntimeGroups.NullAsEmpty().Select(i => new RuntimeGroup(i)))
            {
                runtimeGraph = SafeMerge(runtimeGraph, runtimeGroup);
            }

            ValidateImports(runtimeGraph);

            if (RuntimeJson != null)
            {
                JsonRuntimeFormat.WriteRuntimeGraph(RuntimeJson.ItemSpec, runtimeGraph);
            }

            if (CompatibilityMap != null)
            {
                WriteCompatibilityMap(runtimeGraph, CompatibilityMap.ItemSpec);
            }

            if (RuntimeDirectedGraph != null)
            {
                WriteRuntimeGraph(runtimeGraph, RuntimeDirectedGraph.ItemSpec);
            }

            return !Log.HasLoggedErrors;
        }


        private RuntimeGraph SafeMerge(RuntimeGraph existingGraph, RuntimeGroup runtimeGroup)
        {
            var runtimeGraph = runtimeGroup.GetRuntimeGraph(qualifierPolicies);

            foreach (var existingRuntimeDescription in existingGraph.Runtimes.Values)
            {
                RuntimeDescription newRuntimeDescription;

                if (runtimeGraph.Runtimes.TryGetValue(existingRuntimeDescription.RuntimeIdentifier, out newRuntimeDescription))
                {
                    // overlapping RID, ensure that the imports match (same ordering and content)
                    if (!existingRuntimeDescription.InheritedRuntimes.SequenceEqual(newRuntimeDescription.InheritedRuntimes))
                    {
                        Log.LogError($"RuntimeGroup {runtimeGroup.BaseRID} defines RID {newRuntimeDescription.RuntimeIdentifier} with imports {String.Join(";", newRuntimeDescription.InheritedRuntimes)} which differ from existing imports {String.Join(";", existingRuntimeDescription.InheritedRuntimes)}.  You may avoid this by specifying OmitRIDs metadata with {newRuntimeDescription.RuntimeIdentifier}.");
                    }
                }
            }

            return RuntimeGraph.Merge(existingGraph, runtimeGraph);
        }

        private void ValidateImports(RuntimeGraph runtimeGraph)
        {
            foreach(var runtimeDescription in runtimeGraph.Runtimes.Values)
            {
                foreach(var import in runtimeDescription.InheritedRuntimes)
                {
                    if (!runtimeGraph.Runtimes.ContainsKey(import))
                    {
                        Log.LogError($"Runtime {runtimeDescription.RuntimeIdentifier} imports {import} which is not defined.");
                    }
                }
            }
        }

        class QualifierPolicy
        {
            public QualifierPolicy(ITaskItem item)
            {
                Qualifier = item.ItemSpec;
                VersionPrecedence = item.GetBoolean(nameof(VersionPrecedence));
            }

            public string Qualifier { get; }
            public bool VersionPrecedence { get; }
        }

        class RuntimeGroup
        {
            private const string rootRID = "any";
            private const char VersionDelimiter = '.';
            private const char ArchitectureDelimiter = '-';
            private const char QualifierDelimiter = '-';

            public RuntimeGroup(ITaskItem item)
            {
                BaseRID = item.ItemSpec;
                Parent = item.GetString(nameof(Parent));
                Versions = item.GetStrings(nameof(Versions));
                TreatVersionsAsCompatible = item.GetBoolean(nameof(TreatVersionsAsCompatible), true);
                OmitVersionDelimiter = item.GetBoolean(nameof(OmitVersionDelimiter));
                ApplyVersionsToParent = item.GetBoolean(nameof(ApplyVersionsToParent));
                Architectures = item.GetStrings(nameof(Architectures));
                AdditionalQualifiers = item.GetStrings(nameof(AdditionalQualifiers));
                OmitRIDs = new HashSet<string>(item.GetStrings(nameof(OmitRIDs)));
            }

            public string BaseRID { get; }
            public string Parent { get; }
            public IEnumerable<string> Versions { get; }
            public bool TreatVersionsAsCompatible { get; }
            public bool OmitVersionDelimiter { get; }
            public bool ApplyVersionsToParent { get; }
            public IEnumerable<string> Architectures { get; }
            public IEnumerable<string> AdditionalQualifiers { get; }
            public ICollection<string> OmitRIDs { get; }

            public IEnumerable<RuntimeDescription> GetRuntimeDescriptions()
            {
                // define the base as importing the parent
                yield return new RuntimeDescription(BaseRID, Parent == null ? Enumerable.Empty<string>() : new[] { Parent });

                // define each arch as importing base and parent-arch
                foreach(var architecture in Architectures)
                {
                    var imports = new List<string>();
                    imports.Add(BaseRID);

                    if (Parent != null && Parent != rootRID)
                    {
                        imports.Add($"{Parent}-{architecture}");
                    }

                    yield return new RuntimeDescription($"{BaseRID}-{architecture}", imports);
                }

                var versionDelimiter = OmitVersionDelimiter ? String.Empty : VersionDelimiter.ToString();
                string lastVersion = null;
                foreach (var version in Versions)
                {
                    var imports = new List<string>();

                    // define each version as importing the version-less base or previous version
                    if ((lastVersion == null) || !TreatVersionsAsCompatible)
                    {
                        imports.Add(BaseRID);
                    }
                    else
                    {
                        imports.Add($"{BaseRID}{versionDelimiter}{lastVersion}");
                    }

                    if (ApplyVersionsToParent)
                    {
                        imports.Add($"{Parent}{versionDelimiter}{version}");
                    }

                    yield return new RuntimeDescription($"{BaseRID}{versionDelimiter}{version}", imports);

                    foreach (var architecture in Architectures)
                    {
                        // define each arch-specific version as importing the versioned base and either a previous version arch-specific RID or the base arch-specific RID
                        var archImports = new List<string>();

                        archImports.Add($"{BaseRID}{versionDelimiter}{version}");

                        if ((lastVersion == null) || !TreatVersionsAsCompatible)
                        {
                            archImports.Add($"{BaseRID}{ArchitectureDelimiter}{architecture}");
                        }
                        else
                        {
                            archImports.Add($"{BaseRID}{versionDelimiter}{lastVersion}{ArchitectureDelimiter}{architecture}");
                        }

                        if (ApplyVersionsToParent)
                        {
                            archImports.Add($"{Parent}{versionDelimiter}{version}{ArchitectureDelimiter}{architecture}");
                        }


                        yield return new RuntimeDescription($"{BaseRID}{versionDelimiter}{version}{ArchitectureDelimiter}{architecture}", archImports);
                    }

                    lastVersion = version;
                }
            }

            private IEnumerable<RuntimeDescription> AddQualifiers(IEnumerable<RuntimeDescription> unQualifiedruntimeDescriptions, Dictionary<string, QualifierPolicy> qualifierPolicies)
            {
                foreach(var runtimeDescription in unQualifiedruntimeDescriptions)
                {
                    yield return runtimeDescription;

                    foreach(var qualifier in AdditionalQualifiers)
                    {
                        var imports = new List<string>(runtimeDescription.InheritedRuntimes
                                            .NullAsEmpty()
                                            .Select(rid => rid == null || rid == rootRID ? qualifier : $"{rid}-{qualifier}"));


                        QualifierPolicy qualifierPolicy = null;

                        if (qualifierPolicies.TryGetValue(qualifier, out qualifierPolicy) && qualifierPolicy.VersionPrecedence)
                        {
                            var versionedRid = runtimeDescription.RuntimeIdentifier;

                            var archIndex = versionedRid.IndexOf('-');

                            if (archIndex != -1)
                            {
                                versionedRid = versionedRid.Substring(0, archIndex);
                            }

                            int insertAt = 0;


                            while (insertAt < imports.Count && imports[insertAt].StartsWith(versionedRid))
                            {
                                insertAt++;
                            }

                            imports.Insert(insertAt, runtimeDescription.RuntimeIdentifier);
                        }
                        else
                        {
                            imports.Add(runtimeDescription.RuntimeIdentifier);
                        }


                        yield return new RuntimeDescription($"{runtimeDescription.RuntimeIdentifier}{QualifierDelimiter}{qualifier}", imports);
                        
                    }
                }
            }

            public RuntimeGraph GetRuntimeGraph(Dictionary<string, QualifierPolicy> qualifierPolicies)
            {
                var rids = GetRuntimeDescriptions();

                rids = rids.Where(rid => !OmitRIDs.Contains(rid.RuntimeIdentifier));

                rids = AddQualifiers(rids, qualifierPolicies);

                rids = rids.Where(rid => !OmitRIDs.Contains(rid.RuntimeIdentifier));

                return new RuntimeGraph(rids);
            }
        }


        private static void WriteCompatibilityMap(RuntimeGraph graph, string mapFile)
        {
            Dictionary<string, IEnumerable<string>> compatibilityMap = new Dictionary<string, IEnumerable<string>>();

            foreach(var rid in graph.Runtimes.Keys.OrderBy(rid  => rid, StringComparer.Ordinal))
            {
                compatibilityMap.Add(rid, graph.ExpandRuntime(rid));
            }

            var serializer = new JsonSerializer()
            {
                Formatting = Formatting.Indented,
                StringEscapeHandling = StringEscapeHandling.EscapeNonAscii
            };

            string directory = Path.GetDirectoryName(mapFile);
            if (!String.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var file = File.CreateText(mapFile))
            {
                serializer.Serialize(file, compatibilityMap);
            }
        }


        private static XNamespace s_dgmlns = @"http://schemas.microsoft.com/vs/2009/dgml";
        private static void WriteRuntimeGraph(RuntimeGraph graph, string dependencyGraphFilePath)
        {

            var doc = new XDocument(new XElement(s_dgmlns + "DirectedGraph"));
            var nodesElement = new XElement(s_dgmlns + "Nodes");
            var linksElement = new XElement(s_dgmlns + "Links");
            doc.Root.Add(nodesElement);
            doc.Root.Add(linksElement);

            var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var runtimeDescription in graph.Runtimes.Values)
            {
                nodesElement.Add(new XElement(s_dgmlns + "Node",
                    new XAttribute("Id", runtimeDescription.RuntimeIdentifier)));

                foreach (var import in runtimeDescription.InheritedRuntimes)
                {
                    linksElement.Add(new XElement(s_dgmlns + "Link",
                        new XAttribute("Source", runtimeDescription.RuntimeIdentifier),
                        new XAttribute("Target", import)));
                }
            }

            using (var file = File.Create(dependencyGraphFilePath))
            {
                doc.Save(file);
            }
        }
    }
}
