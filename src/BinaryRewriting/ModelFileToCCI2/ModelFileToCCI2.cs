// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using Microsoft.Tools.Transformer.CodeModel;

namespace ModelFileToCCI2
{
    public class VisibilityFile
    {
        private ModelFileReader _reader;
        private ModelSigFormatter _formatter = new ModelSigFormatter();
        public ModelElement Model { get { return _reader.Model; } }

        // Simple class to allow filtering of model.xml by platform/architecture/flavor
        private class IncludePredicate : IIncludePredicate
        {
            private static char[] s_delim = new char[] { ',' };

            private string _platform;
            private string _architecture;
            private string _flavor;
            private ConditionParser _conditionParser;

            internal IncludePredicate(string platform, string architecture, string flavor, string defines)
            {
                _platform = (platform == null || platform.Length == 0) ? null : platform;
                _architecture = (architecture == null || architecture.Length == 0) ? null : architecture;
                _flavor = (flavor == null || flavor.Length == 0) ? null : flavor;
                _conditionParser = (defines == null || defines.Length == 0) ? null : new ConditionParser(defines.Split(new char[] { ';' }));
            }

            /// <summary>
            /// Match if either the curString is null (meaning no restriction) or the
            /// ref string is in curString list.
            /// </summary>
            /// <param name="refString">String that we want to find entries matching</param>
            /// <param name="curString">String found in xml file</param>
            private bool IsMatch(string refString, string curString)
            {
                if (curString == null) return true;
                foreach (string s in curString.Split(s_delim))
                    if (s == refString) return true;
                return false;
            }

            public bool Include(string platforms, string architectures, string flavors, string condition)
            {
                bool conditionMatches = _conditionParser == null || condition == null || _conditionParser.Parse(condition);
                return IsMatch(_platform, platforms) && IsMatch(_architecture, architectures) && IsMatch(_flavor, flavors) && conditionMatches;
            }
        }

        // Constructor
        public VisibilityFile(string filename) : this(filename, null, null, null, null) { }

        /// <summary>
        /// Processes an API model file so that its data can be queried based on CCI2 objects.  Architecture,
        /// Platform, Flavor and Condition strings based on C# defines may be used to include entries that are
        /// restricted to certain builds.
        /// </summary>
        /// <param name="filename">Filename of model file to open</param>
        /// <param name="platform">Platform string if needed to pull in extra entries</param>
        /// <param name="architecture">Architecture string if needed to pull in extra entries</param>
        /// <param name="flavor">Flavor string if needed to pull in extra entries</param>
        /// <param name="defines">C# defines in ';' separated string if needed to pull in extra entries</param>
        public VisibilityFile(string filename, string platform, string architecture, string flavor, string defines)
        {
            IIncludePredicate pred = new IncludePredicate(platform, architecture, flavor, defines);
            _reader = new ModelFileReader(filename, pred);
        }

        private string GetAssemblyName(ITypeReference typeDef)
        {
            while (typeDef is INestedTypeDefinition) typeDef = ((INestedTypeReference)typeDef).ContainingType;
            IUnitReference unit = ((INamespaceTypeReference)typeDef).ContainingUnitNamespace.Unit;
            return unit.Name.Value;
        }

        /// <summary>
        /// Returns the data in model file that corresponds to passed CCI2 type
        /// </summary>
        /// <param name="typeDef">The CCI2 type to look up</param>
        /// <returns>TypeElementBase of the data loaded from model file, or null if no entry exists</returns>
        public TypeElement this[INamedTypeDefinition typeDef]
        {
            get
            {
                AssemblyElement assembly;
                string assemblyName = GetAssemblyName(typeDef);
                if (_reader.Model.Assemblies.TryGetValue(assemblyName, out assembly))
                {
                    TypeElement type;
                    string typeName = _formatter.GetTypeSignature(typeDef);
                    if (assembly.Types.TryGetValue(typeName, out type))
                        return type;
                }
                return null;
            }
        }

        /// <summary>
        /// Returns the data in model file that corresponds to passed CCI2 type
        /// </summary>
        /// <param name="typeForwarderDef">The CCI2 type tforwarder o look up</param>
        public TypeForwarderElement this[IAliasForType typeForwarderDef]
        {
            get
            {
                IAssembly assemblyDef = Util.GetDefiningAssembly(typeForwarderDef);
                if (assemblyDef != null)
                {
                    AssemblyElement assembly;
                    string assemblyName = assemblyDef.Name.Value;
                    if (_reader.Model.Assemblies.TryGetValue(assemblyName, out assembly))
                    {
                        TypeForwarderElement typeForwarder;
                        string typeForwarderSig = Util.GetTypeForwarderSignature(typeForwarderDef);
                        if (assembly.TypeForwarders.TryGetValue(typeForwarderSig, out typeForwarder))
                            return typeForwarder;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Returns the data in model file that corresponds to passed CCI2 type member
        /// </summary>
        /// <param name="typeDef">The CCI2 type member to look up</param>
        /// <returns>MemberElementBase of the data loaded from model file, or null if no entry exists</returns>
        public MemberElement this[ITypeDefinitionMember memberDef]
        {
            get
            {
                INamedTypeDefinition typeDef = Util.ContainingTypeDefinition(memberDef);
                AssemblyElement assembly;
                string assemblyName = GetAssemblyName(typeDef);
                if (_reader.Model.Assemblies.TryGetValue(assemblyName, out assembly))
                {
                    TypeElement type;
                    string typeName = _formatter.GetTypeSignature(typeDef);
                    if (assembly.Types.TryGetValue(typeName, out type))
                    {
                        // Try to return the member data if there
                        MemberElement member;
                        string memberName = _formatter.GetMemberSignature(memberDef, true);
                        if (type.Members.TryGetValue(memberName, out member))
                            return member;
                    }
                }
                return null;
            }
        }
    }
}
