﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Cci;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.MutableCodeModel;
using Microsoft.Fx.CommandLine;

namespace GenFacades
{
    public class Program
    {
        private const uint ReferenceAssemblyFlag = 0x70;

        public static void Main(string[] args)
        {
            string seeds = null;
            string contracts = null;
            string facadePath = null;
            Version assemblyFileVersion = null;
            bool clearBuildAndRevision = false;
            bool ignoreMissingTypes = false;
            bool buildDesignTimeFacades = false;
            string inclusionContracts = null;
            ErrorTreatment seedLoadErrorTreatment = ErrorTreatment.Default;
            ErrorTreatment contractLoadErrorTreatment = ErrorTreatment.Default;
            string[] seedTypePreferencesUnsplit = null;
            bool forceZeroVersionSeeds = false;
            bool producePdb = true;
            string partialFacadeAssemblyPath = null;

            bool parsingSucceeded = CommandLineParser.ParseForConsoleApplication((parser) =>
            {
                parser.DefineQualifier("facadePath", ref facadePath, "Path to output the facades.");
                parser.DefineQualifier("seeds", ref seeds, "Path to the seed assemblies. Can contain multiple assemblies or directories delimited by ',' or ';'.");
                parser.DefineQualifier("contracts", ref contracts, "Path to the contract assemblies. Can contain multiple assemblies or directories delimited by ',' or ';'.");
                parser.DefineOptionalQualifier("assemblyFileVersion", ref assemblyFileVersion, "Override the AssemblyFileVersion attribute from the contract with the given version for the generated facade.");
                parser.DefineOptionalQualifier("clearBuildAndRevision", ref clearBuildAndRevision, "Generate facade assembly version x.y.0.0 for contract version x.y.z.w");
                parser.DefineOptionalQualifier("ignoreMissingTypes", ref ignoreMissingTypes, "Ignore types that cannot be found in the seed assemblies. This is not recommended but is sometimes helpful while hacking around or trying to produce partial facades.");
                parser.DefineOptionalQualifier("designTime", ref buildDesignTimeFacades, "Enable design-time facade generation (marks facades with reference assembly flag and attribute).");
                parser.DefineOptionalQualifier("include", ref inclusionContracts, "Add types from these contracts to the facades. Can contain multiple assemblies or directories delimited by ',' or ';'.");
                parser.DefineOptionalQualifier("seedError", ref seedLoadErrorTreatment, "Error handling for seed assembly load failure.");
                parser.DefineOptionalQualifier("contractError", ref seedLoadErrorTreatment, "Error handling for contract assembly load failure.");
                parser.DefineOptionalQualifier("preferSeedType", ref seedTypePreferencesUnsplit, "Set which seed assembly to choose for a given type when it is defined in more than one assembly. Format: FullTypeName=PreferredSeedAssemblyName");
                parser.DefineOptionalQualifier("forceZeroVersionSeeds", ref forceZeroVersionSeeds, "Forces all seed assembly versions to 0.0.0.0, regardless of their true version.");
                parser.DefineOptionalQualifier("partialFacadeAssemblyPath", ref partialFacadeAssemblyPath, "Specifies the path to a single partial facade assembly, into which appropriate type forwards will be added to satisfy the given contract. If this option is specified, only a single partial assembly and a single contract may be given.");
                parser.DefineOptionalQualifier("producePdb", ref producePdb, "Specifices if a PDB file should be produced for the resulting partial facade.");
            }, args);

            if (!parsingSucceeded)
            {
                return;
            }

            CommandLineTraceHandler.Enable();

            if (!Directory.Exists(facadePath))
                Directory.CreateDirectory(facadePath);

            var nameTable = new NameTable();
            var internFactory = new InternFactory();

            try
            {
                Dictionary<string, string> seedTypePreferences = ParseSeedTypePreferences(seedTypePreferencesUnsplit);

                using (var contractHost = new HostEnvironment(nameTable, internFactory))
                using (var seedHost = new HostEnvironment(nameTable, internFactory))
                {
                    contractHost.LoadErrorTreatment = contractLoadErrorTreatment;
                    seedHost.LoadErrorTreatment = seedLoadErrorTreatment;

                    var contractAssemblies = LoadAssemblies(contractHost, contracts);
                    IReadOnlyDictionary<string, IEnumerable<string>> docIdTable = GenerateDocIdTable(contractAssemblies, inclusionContracts);

                    IAssembly[] seedAssemblies = LoadAssemblies(seedHost, seeds).ToArray();

                    IAssemblyReference seedCoreAssemblyRef = ((Microsoft.Cci.Immutable.PlatformType)seedHost.PlatformType).CoreAssemblyRef;

                    if (forceZeroVersionSeeds)
                    {
                        // Create a deep copier, copy the seed assemblies, and zero out their versions.
                        var copier = new MetadataDeepCopier(seedHost);

                        for (int i = 0; i < seedAssemblies.Length; i++)
                        {
                            var mutableSeed = copier.Copy(seedAssemblies[i]);
                            mutableSeed.Version = new Version(0, 0, 0, 0);
                            // Copy the modified seed assembly back.
                            seedAssemblies[i] = mutableSeed;

                            if (mutableSeed.Name.UniqueKey == seedCoreAssemblyRef.Name.UniqueKey)
                            {
                                seedCoreAssemblyRef = mutableSeed;
                            }
                        }
                    }

                    var typeTable = GenerateTypeTable(seedAssemblies);
                    var facadeGenerator = new FacadeGenerator(seedHost, contractHost, docIdTable, typeTable, seedTypePreferences, clearBuildAndRevision, buildDesignTimeFacades, assemblyFileVersion);

                    if (partialFacadeAssemblyPath != null)
                    {
                        if (contractAssemblies.Count() != 1)
                        {
                            throw new FacadeGenerationException(
                                "When partialFacadeAssemblyPath is specified, only exactly one corresponding contract assembly can be specified.");
                        }

                        IAssembly contractAssembly = contractAssemblies.First();
                        IAssembly partialFacadeAssembly = seedHost.LoadAssembly(partialFacadeAssemblyPath);
                        if (contractAssembly.Name != partialFacadeAssembly.Name
                            || contractAssembly.Version != partialFacadeAssembly.Version
                            || contractAssembly.GetPublicKeyToken() != partialFacadeAssembly.GetPublicKeyToken())
                        {
                            throw new FacadeGenerationException(
                                string.Format("The partial facade assembly's name, version, and public key token must exactly match the contract to be filled. Contract: {0}, Facade: {1}",
                                    contractAssembly.AssemblyIdentity,
                                    partialFacadeAssembly.AssemblyIdentity));
                        }

                        Assembly filledPartialFacade = facadeGenerator.GenerateFacade(contractAssembly, seedCoreAssemblyRef, ignoreMissingTypes, overrideContractAssembly: partialFacadeAssembly);

                        string pdbLocation = null;

                        if (producePdb)
                        {
                            string pdbFolder = Path.GetDirectoryName(partialFacadeAssemblyPath);
                            pdbLocation = Path.Combine(pdbFolder, contractAssembly.Name + ".pdb");
                            if (producePdb && !File.Exists(pdbLocation))
                            {
                                pdbLocation = null;
                                Trace.TraceWarning("No PDB file present for un-transformed partial facade. No PDB will be generated.");
                            }
                        }

                        OutputFacadeToFile(facadePath, seedHost, filledPartialFacade, contractAssembly, pdbLocation);
                    }
                    else
                    {
                        foreach (var contract in contractAssemblies)
                        {
                            Assembly facade = facadeGenerator.GenerateFacade(contract, seedCoreAssemblyRef, ignoreMissingTypes);
                            if (facade == null)
                            {
#if !COREFX
                                Debug.Assert(Environment.ExitCode != 0);
#endif
                                continue;
                            }

                            OutputFacadeToFile(facadePath, seedHost, facade, contract);
                        }
                    }
                }
            }
            catch (FacadeGenerationException ex)
            {
                Trace.TraceError(ex.Message);
#if !COREFX
                Debug.Assert(Environment.ExitCode != 0);
#endif
            }
        }

        private static void OutputFacadeToFile(string facadePath, HostEnvironment seedHost, Assembly facade, IAssembly contract, string pdbLocation = null)
        {
            // Use the filename (including extension .dll/.winmd) so people can have some control over the output facade file name.
            string facadeFileName = Path.GetFileName(contract.Location);
            string facadeOutputPath = Path.Combine(facadePath, facadeFileName);
            using (Stream peOutStream = File.Create(facadeOutputPath))
            {
                if (pdbLocation != null)
                {
                    if (File.Exists(pdbLocation))
                    {
                        string pdbOutputPath = Path.Combine(facadePath, contract.Name + ".pdb");
                        using (Stream pdbReadStream = File.OpenRead(pdbLocation))
                        using (PdbReader pdbReader = new PdbReader(pdbReadStream, seedHost))
                        using (PdbWriter pdbWriter = new PdbWriter(pdbOutputPath, pdbReader))
                        {
                            PeWriter.WritePeToStream(facade, seedHost, peOutStream, pdbReader, pdbReader, pdbWriter);
                        }
                    }
                    else
                    {
                        throw new FacadeGenerationException("Couldn't find the pdb at the given location: " + pdbLocation);
                    }
                }
                else
                {
                    PeWriter.WritePeToStream(facade, seedHost, peOutStream);
                }
            }
        }

        private static Dictionary<string, string> ParseSeedTypePreferences(string[] preferences)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);

            if (preferences != null)
            {
                foreach (string preference in preferences)
                {
                    int i = preference.IndexOf('=');
                    if (i < 0)
                    {
                        throw new FacadeGenerationException("Invalid seed type preference. Correct usage is /preferSeedType:FullTypeName=AssemblyName");
                    }

                    string key = preference.Substring(0, i);
                    string value = preference.Substring(i + 1);

                    if (!key.StartsWith("T:", StringComparison.Ordinal))
                    {
                        key = "T:" + key;
                    }

                    string existingValue;
                    if (dictionary.TryGetValue(key, out existingValue))
                    {
                        Trace.TraceWarning("Overriding /preferSeedType:{0}={1} with /preferSeedType:{2}={3}.", key, existingValue, key, value);
                    }

                    dictionary[key] = value;
                }
            }

            return dictionary;
        }

        private static IEnumerable<IAssembly> LoadAssemblies(HostEnvironment host, string assemblyPaths)
        {
            host.UnifyToLibPath = true;
            string[] splitPaths = HostEnvironment.SplitPaths(assemblyPaths);

            foreach (string path in splitPaths)
            {
                if (Directory.Exists(path))
                {
                    host.AddLibPath(Path.GetFullPath(path));
                }
                else if (File.Exists(path))
                {
                    host.AddLibPath(Path.GetDirectoryName(Path.GetFullPath(path)));
                }
            }

            return host.LoadAssemblies(splitPaths);
        }

        private static IReadOnlyDictionary<string, IEnumerable<string>> GenerateDocIdTable(IEnumerable<IAssembly> contractAssemblies, string inclusionContracts)
        {
            Dictionary<string, HashSet<string>> mutableDocIdTable = new Dictionary<string, HashSet<string>>();
            foreach (IAssembly contractAssembly in contractAssemblies)
            {
                string simpleName = contractAssembly.AssemblyIdentity.Name.Value;
                if (mutableDocIdTable.ContainsKey(simpleName))
                    throw new FacadeGenerationException(string.Format("Multiple contracts named \"{0}\" specified on -contracts.", simpleName));
                mutableDocIdTable[simpleName] = new HashSet<string>(EnumerateDocIdsToForward(contractAssembly));
            }

            if (inclusionContracts != null)
            {
                foreach (string inclusionContractPath in HostEnvironment.SplitPaths(inclusionContracts))
                {
                    // Assembly identity conflicts are permitted and normal in the inclusion contract list so load each one in a throwaway host to avoid problems.
                    using (HostEnvironment inclusionHost = new HostEnvironment(new NameTable(), new InternFactory()))
                    {
                        IAssembly inclusionAssembly = inclusionHost.LoadAssemblyFrom(inclusionContractPath);
                        if (inclusionAssembly == null || inclusionAssembly is Dummy)
                            throw new FacadeGenerationException(string.Format("Could not load assembly \"{0}\".", inclusionContractPath));
                        string simpleName = inclusionAssembly.Name.Value;
                        HashSet<string> hashset;
                        if (!mutableDocIdTable.TryGetValue(simpleName, out hashset))
                        {
                            Trace.TraceWarning("An assembly named \"{0}\" was specified in the -include list but no contract was specified named \"{0}\". Ignoring.", simpleName);
                        }
                        else
                        {
                            foreach (string docId in EnumerateDocIdsToForward(inclusionAssembly))
                            {
                                hashset.Add(docId);
                            }
                        }
                    }
                }
            }

            Dictionary<string, IEnumerable<string>> docIdTable = new Dictionary<string, IEnumerable<string>>();
            foreach (KeyValuePair<string, HashSet<string>> kv in mutableDocIdTable)
            {
                string key = kv.Key;
                IEnumerable<string> sortedDocIds = kv.Value.OrderBy(s => s);
                docIdTable.Add(key, sortedDocIds);
            }
            return docIdTable;
        }

        private static IEnumerable<string> EnumerateDocIdsToForward(IAssembly contractAssembly)
        {
            // Make note that all type forwards (including nested) implement INamespaceAliasForType, so do
            // not be tempted to filter using it, instead, we look at the aliased type to them.
            var typeForwardsToForward = contractAssembly.ExportedTypes.Select(alias => alias.AliasedType)
                                                                      .OfType<INamespaceTypeReference>();

            var typesToForward = contractAssembly.GetAllTypes().Where(t => TypeHelper.IsVisibleOutsideAssembly(t))
                                                               .OfType<INamespaceTypeDefinition>();

            return typeForwardsToForward.Concat(typesToForward)
                                        .Select(type => TypeHelper.GetTypeName(type, NameFormattingOptions.DocumentationId));
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<INamespaceTypeDefinition>> GenerateTypeTable(IEnumerable<IAssembly> seedAssemblies)
        {
            var typeTable = new Dictionary<string, IReadOnlyList<INamespaceTypeDefinition>>();
            foreach (var assembly in seedAssemblies)
            {
                foreach (var type in assembly.GetAllTypes().OfType<INamespaceTypeDefinition>())
                {
                    if (!TypeHelper.IsVisibleOutsideAssembly(type))
                        continue;

                    IReadOnlyList<INamespaceTypeDefinition> seedTypes;
                    string docId = TypeHelper.GetTypeName(type, NameFormattingOptions.DocumentationId);
                    if (!typeTable.TryGetValue(docId, out seedTypes))
                    {
                        seedTypes = new List<INamespaceTypeDefinition>(1);
                        typeTable.Add(docId, seedTypes);
                    }

                    ((List<INamespaceTypeDefinition>)seedTypes).Add(type);
                }
            }
            return typeTable;
        }

        private class FacadeGenerator
        {
            private readonly IMetadataHost _seedHost;
            private readonly IMetadataHost _contractHost;
            private readonly IReadOnlyDictionary<string, IEnumerable<string>> _docIdTable;
            private readonly IReadOnlyDictionary<string, IReadOnlyList<INamespaceTypeDefinition>> _typeTable;
            private readonly IReadOnlyDictionary<string, string> _seedTypePreferences;
            private readonly bool _clearBuildAndRevision;
            private readonly bool _buildDesignTimeFacades;
            private readonly Version _assemblyFileVersion;

            public FacadeGenerator(
                IMetadataHost seedHost,
                IMetadataHost contractHost,
                IReadOnlyDictionary<string, IEnumerable<string>> docIdTable,
                IReadOnlyDictionary<string, IReadOnlyList<INamespaceTypeDefinition>> typeTable,
                IReadOnlyDictionary<string, string> seedTypePreferences,
                bool clearBuildAndRevision,
                bool buildDesignTimeFacades,
                Version assemblyFileVersion
                )
            {
                _seedHost = seedHost;
                _contractHost = contractHost;
                _docIdTable = docIdTable;
                _typeTable = typeTable;
                _seedTypePreferences = seedTypePreferences;
                _clearBuildAndRevision = clearBuildAndRevision;
                _buildDesignTimeFacades = buildDesignTimeFacades;
                _assemblyFileVersion = assemblyFileVersion;
            }

            public Assembly GenerateFacade(IAssembly contractAssembly, IAssemblyReference seedCoreAssemblyReference, bool ignoreMissingTypes, IAssembly overrideContractAssembly = null)
            {
                Assembly assembly;
                if (overrideContractAssembly != null)
                {
                    MetadataDeepCopier copier = new MetadataDeepCopier(_seedHost);
                    assembly = copier.Copy(overrideContractAssembly); // Use non-empty partial facade if present
                }
                else
                {
                    MetadataDeepCopier copier = new MetadataDeepCopier(_contractHost);
                    assembly = copier.Copy(contractAssembly);
                    ReferenceAssemblyToFacadeRewriter rewriter = new ReferenceAssemblyToFacadeRewriter(_seedHost, _contractHost, seedCoreAssemblyReference, _assemblyFileVersion != null);
                    rewriter.Rewrite(assembly);
                }

                IEnumerable<string> docIds = _docIdTable[contractAssembly.AssemblyIdentity.Name.Value];

                // Add all the type forwards
                bool error = false;

                HashSet<string> existingDocIds = new HashSet<string>(assembly.AllTypes.Select(typeDef => typeDef.RefDocId()));
                IEnumerable<string> missingDocIds = docIds.Where(id => !existingDocIds.Contains(id));
                foreach (string docId in missingDocIds)
                {
                    IReadOnlyList<INamespaceTypeDefinition> seedTypes;
                    if (!_typeTable.TryGetValue(docId, out seedTypes))
                    {
                        if (!ignoreMissingTypes)
                        {
                            Trace.TraceError("Did not find type '{0}' in any of the seed assemblies.", docId);
                            error = true;
                        }
                        continue;
                    }

                    INamespaceTypeDefinition seedType = GetSeedType(docId, seedTypes);
                    if (seedType == null)
                    {
                        TraceDuplicateSeedTypeError(docId, seedTypes);
                        error = true;
                        continue;
                    }

                    AddTypeForward(assembly, seedType);
                }

                if (error)
                {
                    return null;
                }

                if (_assemblyFileVersion != null)
                {
                    assembly.AssemblyAttributes.Add(CreateAttribute("System.Reflection.AssemblyFileVersionAttribute", seedCoreAssemblyReference.ResolvedAssembly, _assemblyFileVersion.ToString()));
                    assembly.AssemblyAttributes.Add(CreateAttribute("System.Reflection.AssemblyInformationalVersionAttribute", seedCoreAssemblyReference.ResolvedAssembly, _assemblyFileVersion.ToString()));
                }

                if (_buildDesignTimeFacades)
                {
                    assembly.AssemblyAttributes.Add(CreateAttribute("System.Runtime.CompilerServices.ReferenceAssemblyAttribute", seedCoreAssemblyReference.ResolvedAssembly));
                    assembly.Flags |= ReferenceAssemblyFlag;
                }

                if (_clearBuildAndRevision)
                {
                    assembly.Version = new Version(assembly.Version.Major, assembly.Version.Minor, 0, 0);
                }

                AddWin32VersionResource(contractAssembly.Location, assembly);

                return assembly;
            }

            private INamespaceTypeDefinition GetSeedType(string docId, IReadOnlyList<INamespaceTypeDefinition> seedTypes)
            {
                Debug.Assert(seedTypes.Count != 0); // we should already have checked for non-existent types.

                if (seedTypes.Count == 1)
                {
                    return seedTypes[0];
                }

                string preferredSeedAssembly;
                if (_seedTypePreferences.TryGetValue(docId, out preferredSeedAssembly))
                {
                    return seedTypes.SingleOrDefault(t => String.Equals(t.ContainingUnitNamespace.Unit.Name.Value, preferredSeedAssembly, StringComparison.OrdinalIgnoreCase));
                }

                return null;
            }

            private static void TraceDuplicateSeedTypeError(string docId, IReadOnlyList<INamespaceTypeDefinition> seedTypes)
            {
                Trace.TraceError("The type '{0}' is defined in multiple seed assemblies. If this is intentional, specify one of the following arguments to choose the preferred seed type:", docId);

                foreach (INamespaceTypeDefinition type in seedTypes)
                {
                    Trace.TraceError("  /preferSeedType:{0}={1}", docId.Substring("T:".Length), type.ContainingUnitNamespace.Unit.Name.Value);
                }
            }

            private void AddTypeForward(Assembly assembly, INamedTypeDefinition seedType)
            {
                var alias = new NamespaceAliasForType();
                alias.AliasedType = ConvertDefinitionToReferenceIfTypeIsNested(seedType, _seedHost);
                alias.IsPublic = true;

                if (assembly.ExportedTypes == null)
                    assembly.ExportedTypes = new List<IAliasForType>();
                assembly.ExportedTypes.Add(alias);

                // Recursively add forwarders for all nested types regardless of their accessibility. This is
                // how the C# compiler emits type forwarders for nested types. We might not need them for
                // nested types that are not visible outside the assembly, but it is safer to replicate how
                // the C# compiler works. Plus, it helps when diffing the output from ildasm for facades
                // that were built from source vs. those that were produced by this tool.
                //
                // NOTE: Some design-time tools can resolve forwarded nested types with only the top-level forwarder,
                //       but the runtime currently throws a TypeLoadException without explicit forwarders for the nested 
                //       types.
                foreach (var nestedType in seedType.NestedTypes.OrderBy(t => t.Name.Value))
                    AddTypeForward(assembly, nestedType);
            }

            private void AddWin32VersionResource(string contractLocation, Assembly facade)
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(contractLocation);
                var versionSerializer = new VersionResourceSerializer(
                    true,
                    versionInfo.Comments,
                    versionInfo.CompanyName,
                    versionInfo.FileDescription,
                    _assemblyFileVersion == null ? versionInfo.FileVersion : _assemblyFileVersion.ToString(),
                    versionInfo.InternalName,
                    versionInfo.LegalCopyright,
                    versionInfo.LegalTrademarks,
                    versionInfo.OriginalFilename,
                    versionInfo.ProductName,
                    _assemblyFileVersion == null ? versionInfo.ProductVersion : _assemblyFileVersion.ToString(),
                    facade.Version);

                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream, Encoding.Unicode, true))
                {
                    versionSerializer.WriteVerResource(writer);

                    var resource = new Win32Resource();
                    resource.Id = 1;
                    resource.TypeId = 0x10;
                    resource.Data = stream.ToArray().ToList();

                    facade.Win32Resources.Add(resource);
                }
            }

            // This shouldn't be necessary, but CCI is putting a nonzero TypeDefId in the ExportedTypes table
            // for nested types if NamespaceAliasForType.AliasedType is set to an ITypeDefinition
            // so we make an ITypeReference copy as a workaround.
            private static INamedTypeReference ConvertDefinitionToReferenceIfTypeIsNested(INamedTypeDefinition typeDef, IMetadataHost host)
            {
                var nestedTypeDef = typeDef as INestedTypeDefinition;
                if (nestedTypeDef == null)
                    return typeDef;

                var typeRef = new NestedTypeReference();
                typeRef.Copy(nestedTypeDef, host.InternFactory);
                return typeRef;
            }

            private ICustomAttribute CreateAttribute(string typeName, IAssembly seedCoreAssembly, string argument = null)
            {
                var type = seedCoreAssembly.GetAllTypes().FirstOrDefault(t => t.FullName() == typeName);
                if (type == null)
                {
                    throw new FacadeGenerationException(String.Format("Cannot find {0} type in seed core assembly.", typeName));
                }

                IEnumerable<IMethodDefinition> constructors = type.GetMembersNamed(_seedHost.NameTable.Ctor, false).OfType<IMethodDefinition>();

                IMethodDefinition constructor = null;
                if (argument != null)
                {
                    constructor = constructors.SingleOrDefault(m => m.ParameterCount == 1 && m.Parameters.First().Type.AreEquivalent("System.String"));
                }
                else
                {
                    constructor = constructors.SingleOrDefault(m => m.ParameterCount == 0);
                }

                if (constructor == null)
                {
                    throw new FacadeGenerationException(String.Format("Cannot find {0} constructor taking single string argument in seed core assembly.", typeName));
                }

                var attribute = new CustomAttribute();
                attribute.Constructor = constructor;

                if (argument != null)
                {
                    var argumentExpression = new MetadataConstant();
                    argumentExpression.Type = _seedHost.PlatformType.SystemString;
                    argumentExpression.Value = argument;

                    attribute.Arguments = new List<IMetadataExpression>(1);
                    attribute.Arguments.Add(argumentExpression);
                }

                return attribute;
            }
        }

        private class ReferenceAssemblyToFacadeRewriter : MetadataRewriter
        {
            private IMetadataHost _seedHost;
            private IMetadataHost _contractHost;
            private IAssemblyReference _seedCoreAssemblyReference;
            private bool _stripFileVersionAttributes;

            public ReferenceAssemblyToFacadeRewriter(
                IMetadataHost seedHost,
                IMetadataHost contractHost,
                IAssemblyReference seedCoreAssemblyReference,
                bool stripFileVersionAttributes)
                : base(seedHost)
            {
                _seedHost = seedHost;
                _contractHost = contractHost;
                _stripFileVersionAttributes = stripFileVersionAttributes;
                _seedCoreAssemblyReference = seedCoreAssemblyReference;
            }

            public override IAssemblyReference Rewrite(IAssemblyReference assemblyReference)
            {
                if (assemblyReference == null)
                    return assemblyReference;

                if (assemblyReference.UnifiedAssemblyIdentity.Equals(_contractHost.CoreAssemblySymbolicIdentity) &&
                    !assemblyReference.ModuleIdentity.Equals(host.CoreAssemblySymbolicIdentity))
                {
                    assemblyReference = _seedCoreAssemblyReference;
                }

                return base.Rewrite(assemblyReference);
            }

            public override void RewriteChildren(RootUnitNamespace rootUnitNamespace)
            {
                var assemblyReference = rootUnitNamespace.Unit as IAssemblyReference;
                if (assemblyReference != null)
                    rootUnitNamespace.Unit = Rewrite(assemblyReference).ResolvedUnit;

                base.RewriteChildren(rootUnitNamespace);
            }

            public override List<INamespaceMember> Rewrite(List<INamespaceMember> namespaceMembers)
            {
                // Ignore traversing or rewriting any namspace members.
                return base.Rewrite(new List<INamespaceMember>());
            }

            public override void RewriteChildren(Assembly assembly)
            {
                // Clear all win32 resources. The version resource will get repopulated.
                assembly.Win32Resources = new List<IWin32Resource>();

                // Remove all the references they will get repopulated while outputing.
                assembly.AssemblyReferences.Clear();

                // Remove all the module references (aka native references)
                assembly.ModuleReferences = new List<IModuleReference>();

                // Remove all file references (ex: *.nlp files in mscorlib)
                assembly.Files = new List<IFileReference>();

                // Remove all security attributes (ex: permissionset in IL)
                assembly.SecurityAttributes = new List<ISecurityAttribute>();

                // Reset the core assembly symbolic identity to the seed core assembly (e.g. mscorlib, corefx)
                // and not the contract core (e.g. System.Runtime).
                assembly.CoreAssemblySymbolicIdentity = _seedCoreAssemblyReference.AssemblyIdentity;

                // Add reference to seed core assembly up-front so that we keep the same order as the C# compiler.
                assembly.AssemblyReferences.Add(_seedCoreAssemblyReference);

                // Remove all type definitions except for the "<Module>" type.
                assembly.AllTypes.RemoveAll(t => t.Name.Value != "<Module>");

                // Remove any preexisting typeforwards.
                assembly.ExportedTypes = new List<IAliasForType>();

                // Remove any preexisting resources.
                assembly.Resources = new List<IResourceReference>();

                // Clear the reference assembly flag from the contract.
                // For design-time facades, it will be added back later.
                assembly.Flags &= ~ReferenceAssemblyFlag;

                // This flag should not be set until the delay-signed assembly we emit is actually signed.
                assembly.StrongNameSigned = false;

                base.RewriteChildren(assembly);
            }

            public override List<ICustomAttribute> Rewrite(List<ICustomAttribute> customAttributes)
            {
                if (customAttributes == null)
                    return customAttributes;

                List<ICustomAttribute> newCustomAttributes = new List<ICustomAttribute>();

                // Remove all of them except for the ones that begin with Assembly
                // Also remove AssemblyFileVersion and AssemblyInformationVersion if stripFileVersionAttributes is set
                foreach (ICustomAttribute attribute in customAttributes)
                {
                    ITypeReference attributeType = attribute.Type;
                    if (attributeType is Dummy)
                        continue;

                    string typeName = TypeHelper.GetTypeName(attributeType, NameFormattingOptions.OmitContainingNamespace | NameFormattingOptions.OmitContainingType);

                    if (!typeName.StartsWith("Assembly"))
                        continue;

                    // We need to remove the signature key attribute otherwise we will not be able to re-sign these binaries.
                    if (typeName == "AssemblySignatureKeyAttribute")
                        continue;

                    if (_stripFileVersionAttributes && ((typeName == "AssemblyFileVersionAttribute" || typeName == "AssemblyInformationalVersionAttribute")))
                        continue;

                    newCustomAttributes.Add(attribute);
                }
                return base.Rewrite(newCustomAttributes);
            }
        }
    }
}
