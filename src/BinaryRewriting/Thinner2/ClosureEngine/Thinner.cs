// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using Microsoft.Tools.Transformer.CodeModel;
using ModelFileToCCI2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace Thinner
{
    public class ThinMember : MemberElement
    {
        private ThinType _declaringType;
        private string _memberFullName;
        private string _returnType;
        private MemberTypes _memberType;
        private ITypeDefinitionMember _memberNode;
        private IncludeStatus _includeStatus;
        private VisibilityOverride _visibility;
        private SecurityTransparencyStatus _securityTransparencyStatus;


        public ThinMember(ThinType declaringType, string memberName, string returnType, MemberTypes memberType,
                          IncludeStatus includeStatus, ITypeDefinitionMember memberNode, VisibilityOverride visibility)

            : this(declaringType, memberName, returnType, memberType,
                   includeStatus, memberNode, visibility, SecurityTransparencyStatus.Transparent)
        {
        }


        public ThinMember(ThinType declaringType, string memberName, string returnType, MemberTypes memberType,
                          IncludeStatus includeStatus, ITypeDefinitionMember memberNode, VisibilityOverride visibility,
                          SecurityTransparencyStatus securityTransparencyStatus)
        {
            _memberNode = memberNode;
            _declaringType = declaringType;
            _returnType = returnType;
            _memberType = memberType;
            _includeStatus = includeStatus;
            _visibility = visibility;
            _securityTransparencyStatus = securityTransparencyStatus;

            _memberFullName = memberName;

            CheckVisibility();
        }

        public ThinMember(ThinType declaringType, ThinMember memberToCopy)
        {
            _declaringType = declaringType;

            _memberNode = memberToCopy._memberNode;
            _memberFullName = memberToCopy._memberFullName;
            _returnType = memberToCopy._returnType;
            _memberType = memberToCopy._memberType;
            _includeStatus = memberToCopy._includeStatus;
            _visibility = memberToCopy._visibility;
            _securityTransparencyStatus = memberToCopy._securityTransparencyStatus;

            CheckVisibility();
        }

        public override string Name
        {
            get { return _memberFullName; }
        }

        public ThinMember(ThinType declaringType, ITypeDefinitionMember member, IncludeStatus includeStatus, VisibilityOverride visibility)
        {
            _declaringType = declaringType;

            _memberNode = member;
            _memberType = Util.TypeMemberType(member);
            _returnType = Util.GetConversionOperatorReturnType(member);
            _memberFullName = Util.GetMemberSignature(member, false);

            // Remove the trailing return type for conversion operators
            if (_returnType != null)
            {
                string suffix = " : " + _returnType;
                if (_memberFullName.EndsWith(suffix))
                    _memberFullName = _memberFullName.Substring(0, _memberFullName.Length - suffix.Length);
            }

            _includeStatus = includeStatus;
            _visibility = visibility;
            _securityTransparencyStatus = SecurityTransparencyStatus.Transparent;

            CheckVisibility();
        }

        private string _key;
        public override string Key
        {
            get
            {
                if (_key == null)
                {
                    // Should generate the same string as Util.GetMemberSignature
                    StringBuilder sbKey = new StringBuilder();

                    sbKey.Append(_memberType.ToString());
                    sbKey.Append(" : ");

                    sbKey.Append(_memberFullName);

                    if (_returnType != null)
                    {
                        sbKey.Append(" : ");
                        sbKey.Append(_returnType);
                    }

                    _key = sbKey.ToString();
                }

                return _key;
            }
        }

        public ThinType DeclaringType { get { return _declaringType; } }

        public HostEnvironment HostEnvironment { get { return _declaringType.HostEnvironment; } }
        public override MemberTypes MemberType { get { return _memberType; } }
        public override string ReturnType { get { return _returnType; } }
        public override IncludeStatus IncludeStatus { get { return _includeStatus; } }
        public override VisibilityOverride VisibilityOverride { get { return _visibility; } }
        public override SecurityTransparencyStatus SecurityTransparencyStatus { get { return _securityTransparencyStatus; } }
        public void SetSecurityTransparencyStatus(SecurityTransparencyStatus securityTransparencyStatus)
        {
            if (_securityTransparencyStatus != SecurityTransparencyStatus.Transparent)
                throw new Exception("ERROR: Cannot update SecurityTransparencyStatus from any initial state other than Transparent");
            _securityTransparencyStatus = securityTransparencyStatus;
        }
        public ITypeDefinitionMember Metadata { get { return _memberNode; } }

        public void LoadMetadata(Dictionary<string, ITypeDefinitionMember> memberMap)
        {
            if (null == _declaringType.Metadata)
                throw new Exception("Declaring type is null");

            string fqmn = Key;
            memberMap.TryGetValue(fqmn, out _memberNode);

            if (_memberNode == null)
            {
                // It's really easy to make a subtle syntax error in our file.  
                // ie, does ref go to & or @?  Do we use "System.Bool" or "System.Boolean"?
                Console.WriteLine("Error with your model file, or you were missing a member in the assembly.  On type {1}, cannot load member {0}.", fqmn, DeclaringType.Name);
                Console.WriteLine("Did you perhaps make a typo in a method signature in your model file?");
                String memberName = _memberType.ToString() + " : " + _memberFullName;
                int parenIndex = memberName.IndexOf('(');
                if (parenIndex > 0)
                    memberName = memberName.Substring(0, parenIndex);

                List<String> possibleMatches = new List<String>();
                foreach (KeyValuePair<String, ITypeDefinitionMember> pair in memberMap)
                {
                    if (pair.Key.StartsWith(memberName, StringComparison.OrdinalIgnoreCase))
                    {
                        possibleMatches.Add(Util.GetMemberSignature(pair.Value, true));
                    }
                }

                if (possibleMatches.Count == 0)
                {
                    Console.WriteLine("Couldn't find any members matching {0}", memberName);
                }
                else
                {
                    Console.WriteLine("Found {0} members that had that name in the assembly.", possibleMatches.Count);
                    foreach (String match in possibleMatches)
                        Console.WriteLine("    {0}", match);
                    Console.WriteLine();
                }

                String suggestions = possibleMatches.Count == 0 ? "No members of that name were found in the assembly." :
                    "Look in the build log files for the signatures of " + possibleMatches.Count + " members matching that name.";
                throw new Exception(String.Format("cannot load member {0} from class {1}.  {2}", fqmn, DeclaringType.Name, suggestions));
                //Console.WriteLine("Error: Cannot load member: " + fqmn);
            }
        }

        public void Serialize(XmlWriter writer)
        {
            CheckVisibility();

            writer.WriteStartElement(XmlKeywords.Elements.Member);

            if ((_includeStatus != IncludeStatus.Inherit) &&
                (_includeStatus != _declaringType.IncludeStatus))
            {
                writer.WriteAttributeString(XmlKeywords.Attributes.IncludeStatus, _includeStatus.ToString());
            }

            if (_visibility != VisibilityOverride.None)
            {
                writer.WriteAttributeString(XmlKeywords.Attributes.VisibilityOverride, _visibility.ToString().ToLower());
            }

            if (_memberType != MemberTypes.Method)
                writer.WriteAttributeString(XmlKeywords.Attributes.MemberType, _memberType.ToString());

            writer.WriteAttributeString(XmlKeywords.Attributes.Name, _memberFullName);

            if (_returnType != null)
                writer.WriteAttributeString(XmlKeywords.Attributes.ReturnType, _returnType);

            if (_securityTransparencyStatus != SecurityTransparencyStatus.Transparent
                        && _securityTransparencyStatus != SecurityTransparencyStatus.Undefined)
            {
                writer.WriteAttributeString(XmlKeywords.Attributes.SecurityTransparencyStatus, _securityTransparencyStatus.ToString());
            }

            writer.WriteEndElement();
        }

        private void CheckVisibility()
        {
            //
            // can't have an API-visibility member on an IMPL-visibility type..
            //
            if (((_includeStatus == IncludeStatus.ApiRoot) ||
                 (_includeStatus == IncludeStatus.ApiRoot)) &&
                ((_declaringType.IncludeStatus == IncludeStatus.ImplRoot) ||
                 (_declaringType.IncludeStatus == IncludeStatus.ImplClosure)))
            {
                throw new Exception("inconsistent include status on member" + Key + " of type " + _declaringType.Name);
            }
        }

        public override string ToString()
        {
            return DeclaringType.ToString() + "." + Key;
        }
    }

    public class ThinType : TypeElement
    {
        private ThinAssembly _declaringAssembly;
        private string _typeFullName;
        private INamedTypeDefinition _typeNode;
        private IncludeStatus _includeStatus;
        private VisibilityOverride _visibility;
        private SecurityTransparencyStatus _securityTransparencyStatus;

        private Dictionary<string, MemberElement> _members;

        public ThinType(ThinAssembly declaringAssembly, string typeName,
                        IncludeStatus includeStatus, INamedTypeDefinition typeNode, VisibilityOverride visibility)

            : this(declaringAssembly, typeName, includeStatus, typeNode, visibility, SecurityTransparencyStatus.Transparent)
        {
        }

        public ThinType(ThinAssembly declaringAssembly, string typeName,
                        IncludeStatus includeStatus, INamedTypeDefinition typeNode, VisibilityOverride visibility,
                        SecurityTransparencyStatus securityTransparencyStatus)
        {
            _members = new Dictionary<string, MemberElement>();
            _typeNode = typeNode;
            _declaringAssembly = declaringAssembly;
            _includeStatus = includeStatus;
            _visibility = visibility;
            _securityTransparencyStatus = securityTransparencyStatus;

            //_typeFullName = Util.CCI2StyleTypeName(typeName);
            _typeFullName = typeName;
        }

        public ThinType(ThinAssembly declaringAssembly, ThinType typeToCopy)
        {
            _members = new Dictionary<string, MemberElement>();
            _declaringAssembly = declaringAssembly;

            _typeNode = typeToCopy._typeNode;
            _typeFullName = typeToCopy._typeFullName;
            _includeStatus = typeToCopy._includeStatus;
            _visibility = typeToCopy._visibility;
            _securityTransparencyStatus = typeToCopy._securityTransparencyStatus;
        }

        public string Name { get { return _typeFullName; } }
        public override string Key { get { return Name; } }

        public override IncludeStatus IncludeStatus { get { return _includeStatus; } }
        public override VisibilityOverride VisibilityOverride { get { return _visibility; } }
        public override SecurityTransparencyStatus SecurityTransparencyStatus { get { return _securityTransparencyStatus; } }
        public void SetSecurityTransparencyStatus(SecurityTransparencyStatus securityTransparencyStatus)
        {
            if (_securityTransparencyStatus != SecurityTransparencyStatus.Transparent)
                throw new Exception("ERROR: Cannot update SecurityTransparencyStatus from any initial state other than Transparent");
            _securityTransparencyStatus = securityTransparencyStatus;
        }

        public INamedTypeDefinition Metadata { get { return _typeNode; } }

        public override IDictionary<String, MemberElement> Members { get { return _members; } }

        public ThinAssembly DeclaringAssembly { get { return _declaringAssembly; } }

        public HostEnvironment HostEnvironment { get { return _declaringAssembly.HostEnvironment; } }

        public void LoadMetadata()
        {
            _typeNode = HostEnvironment.LoadFullyQualifiedTypeDef(_typeFullName, _declaringAssembly.Metadata);

            if (_typeNode == null)
            {
                throw new Exception("API type '" + _typeFullName + "' could not be loaded!"
                                  + "  If that is expected, please mark the class's Status as ImplRoot or ImplClosure");
            }

            //
            // Unfortunately, CCI doesn't really allow direct-lookup of methods with method type parameters
            // because you cannot get ahold of the INamedTypeDefinition for the method type parameters without first
            // getting the Method node.  As a result, we have to create a map of all members on this type
            // and look them up by a unique string.  This also takes care of the CCI limitation that doesn't
            // allow direct lookup of methods that override by return value only (such as op_Explicit).
            //
            Dictionary<string, ITypeDefinitionMember> memberMap = new Dictionary<string, ITypeDefinitionMember>();
            foreach (ITypeDefinitionMember memberNode in _typeNode.Members)
            {
                if (Util.GetMemberTypeFromMember(memberNode as IDefinition) != MemberTypes.Unknown)
                {
                    string key = Util.GetMemberSignature(memberNode, true);
                    memberMap.Add(key, memberNode);
                }
            }

            //Console.WriteLine("Loading members for: " + _typeFullName);
            foreach (ThinMember member in Members.Values)
            {
                member.LoadMetadata(memberMap);
            }
        }

        public void Serialize(XmlWriter writer)
        {
            writer.WriteStartElement(XmlKeywords.Elements.Type);

            if ((_includeStatus != IncludeStatus.Inherit) &&
                (_includeStatus != _declaringAssembly.IncludeStatus))
            {
                writer.WriteAttributeString(XmlKeywords.Attributes.IncludeStatus, _includeStatus.ToString());
            }

            if (_visibility != VisibilityOverride.None)
            {
                writer.WriteAttributeString(XmlKeywords.Attributes.VisibilityOverride, _visibility.ToString().ToLower());
            }

            writer.WriteAttributeString(XmlKeywords.Attributes.Name, _typeFullName);

            if (_securityTransparencyStatus != SecurityTransparencyStatus.Transparent
                        && _securityTransparencyStatus != SecurityTransparencyStatus.Undefined)
            {
                writer.WriteAttributeString(XmlKeywords.Attributes.SecurityTransparencyStatus, _securityTransparencyStatus.ToString());
            }

            SortedDictionary<string, MemberElement> sortedApiMembers = new SortedDictionary<string, MemberElement>(_members);
            SortedDictionary<string, MemberElement> sortedImplMembers = new SortedDictionary<string, MemberElement>();
            foreach (ThinMember member in sortedApiMembers.Values)
            {
                if (member.MemberType != MemberTypes.Unknown)
                {
                    if (member.IncludeStatus == IncludeStatus.ImplClosure ||
                        member.IncludeStatus == IncludeStatus.ImplRoot)
                    {
                        sortedImplMembers.Add(member.Key, member);
                        continue;
                    }
                    member.Serialize(writer);
                }
            }

            foreach (ThinMember member in sortedImplMembers.Values)
            {
                member.Serialize(writer);
            }

            writer.WriteEndElement();
        }

        private void CheckVisibility()
        {
            //
            // can't have an API-visibility member on an IMPL-visibility type..
            //
            if (((_includeStatus == IncludeStatus.ApiRoot) ||
                 (_includeStatus == IncludeStatus.ApiRoot)) &&
                ((_declaringAssembly.IncludeStatus == IncludeStatus.ImplRoot) ||
                 (_declaringAssembly.IncludeStatus == IncludeStatus.ImplClosure)))
            {
                throw new Exception("inconsistent include status on type" + Name + " in assembly " + _declaringAssembly.Name);
            }
        }

        public override string ToString()
        {
            return Key;
        }
    }

    public class ThinTypeForwarder : TypeForwarderElement
    {
        private ThinAssembly _declaringAssembly;
        private string _assemblyName;
        private string _typeName;
        private IAliasForType _alias;
        private IncludeStatus _includeStatus;

        public ThinTypeForwarder(ThinAssembly declaringAssembly, string assemblyName, string typeName,
                                 IncludeStatus includeStatus, IAliasForType alias)
        {
            _declaringAssembly = declaringAssembly;
            _assemblyName = assemblyName;
            _typeName = typeName;
            _alias = alias;
            _includeStatus = includeStatus;
        }

        public ThinTypeForwarder(ThinAssembly declaringAssembly, ThinTypeForwarder typeToCopy)
        {
            _declaringAssembly = declaringAssembly;
            _assemblyName = typeToCopy._assemblyName;
            _typeName = typeToCopy._typeName;
            _alias = typeToCopy._alias;
            _includeStatus = typeToCopy._includeStatus;
        }

        public override string AssemblyName { get { return _assemblyName; } }
        public override string TypeName { get { return _typeName; } }
        public override string Key { get { return AssemblyName + " " + TypeName; } }
        public override IncludeStatus IncludeStatus { get { return _includeStatus; } }
        public override VisibilityOverride VisibilityOverride { get { throw new NotSupportedException(); } }
        public override SecurityTransparencyStatus SecurityTransparencyStatus { get { throw new NotSupportedException(); } }
        public IAliasForType Metadata { get { return _alias; } }
        public ThinAssembly DeclaringAssembly { get { return _declaringAssembly; } }
        public HostEnvironment HostEnvironment { get { return _declaringAssembly.HostEnvironment; } }

        public void LoadMetadata()
        {
            foreach (var alias in _declaringAssembly.Metadata.ExportedTypes)
            {
                if (Util.GetDefiningAssembly(alias.AliasedType).Name.Value == _assemblyName &&
                   Util.GetTypeName(alias.AliasedType) == _typeName)
                {
                    _alias = alias;
                    break;
                }
            }

            if (_alias == null)
            {
                throw new Exception("API type forwarder to '" + Key + "' could not be loaded!");
            }
        }

        public void Serialize(XmlWriter writer)
        {
            writer.WriteStartElement(XmlKeywords.Elements.TypeForwarder);

            if ((_includeStatus != IncludeStatus.Inherit) &&
                (_includeStatus != _declaringAssembly.IncludeStatus))
            {
                writer.WriteAttributeString(XmlKeywords.Attributes.IncludeStatus, _includeStatus.ToString());
            }
            writer.WriteAttributeString(XmlKeywords.Attributes.AssemblyName, _assemblyName);
            writer.WriteAttributeString(XmlKeywords.Attributes.TypeName, _typeName);

            writer.WriteEndElement();
        }

        private void CheckVisibility()
        {
            //
            // can't have an API-visibility member on an IMPL-visibility type..
            //
            if (((_includeStatus == IncludeStatus.ApiRoot) ||
                 (_includeStatus == IncludeStatus.ApiRoot)) &&
                ((_declaringAssembly.IncludeStatus == IncludeStatus.ImplRoot) ||
                 (_declaringAssembly.IncludeStatus == IncludeStatus.ImplClosure)))
            {
                throw new Exception("inconsistent include status on type forwarder '" + Key + "' in assembly " + _declaringAssembly.Name);
            }
        }

        public override string ToString()
        {
            return Key;
        }
    }

    public class ThinAssembly : AssemblyElement
    {
        private string _assemblyName;
        private IAssembly _assemblyNode;
        private IncludeStatus _includeStatus;

        private ThinModel _thinModel;

        public ThinModel ThinModel
        {
            get { return _thinModel; }
        }

        private Dictionary<string, TypeElement> _types;
        private Dictionary<string, TypeForwarderElement> _typeForwaders;

        public override IDictionary<string, TypeElement> Types { get { return _types; } }
        public override IDictionary<string, TypeForwarderElement> TypeForwarders { get { return _typeForwaders; } }

        public ThinAssembly(ThinModel thinModel, string assemblyName, IncludeStatus includeStatus, IAssembly assemblyNode)
        {
            _types = new Dictionary<string, TypeElement>();
            _typeForwaders = new Dictionary<string, TypeForwarderElement>();
            _assemblyNode = assemblyNode;
            _assemblyName = assemblyName;
            _includeStatus = includeStatus;

            _thinModel = thinModel;
        }

        public ThinAssembly(ThinModel declaringModel, ThinAssembly assyToCopy)
        {
            _types = new Dictionary<string, TypeElement>();
            _typeForwaders = new Dictionary<string, TypeForwarderElement>();

            _assemblyNode = assyToCopy._assemblyNode;
            _assemblyName = assyToCopy._assemblyName;
            _includeStatus = assyToCopy._includeStatus;

            _thinModel = declaringModel;
        }

        public string Name { get { return _assemblyName; } }
        public override String Key { get { return Name; } }

        public override IncludeStatus IncludeStatus { get { return _includeStatus; } }

        public IAssembly Metadata { get { return _assemblyNode; } }

        public HostEnvironment HostEnvironment { get { return _thinModel.Options.Environment; } }

        internal void LoadMetadata()
        {
            IAssembly assembly = _thinModel.Options.Environment.FindWeaklyNamedAssembly(_assemblyName);
            if (assembly == null)
            {
                assembly = _thinModel.Options.Environment.LoadWeaklyNamedAssembly(_assemblyName, new String[] { Directory.GetCurrentDirectory() });
            }
            LoadMetadataFrom(assembly);
        }

        internal void LoadMetadataFrom(IAssembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException("assembly");
            _assemblyNode = assembly;

            foreach (ThinType type in _types.Values)
            {
                type.LoadMetadata();
            }
            foreach (ThinTypeForwarder typeForwarder in _typeForwaders.Values)
            {
                typeForwarder.LoadMetadata();
            }
        }

        public void Serialize(XmlWriter writer)
        {
            writer.WriteStartElement(XmlKeywords.Elements.Assembly);

            if (_includeStatus != IncludeStatus.Inherit)
                writer.WriteAttributeString(XmlKeywords.Attributes.IncludeStatus, _includeStatus.ToString());

            writer.WriteAttributeString(XmlKeywords.Attributes.Name, _assemblyName);

            SortedDictionary<string, TypeElement> sortedApiTypes = new SortedDictionary<string, TypeElement>(_types);
            SortedDictionary<string, TypeElement> sortedImplTypes = new SortedDictionary<string, TypeElement>();
            SortedDictionary<string, TypeForwarderElement> sortedTypeForwarders = new SortedDictionary<string, TypeForwarderElement>(_typeForwaders);
            foreach (ThinType type in sortedApiTypes.Values)
            {
                if (type.IncludeStatus == IncludeStatus.ImplClosure ||
                    type.IncludeStatus == IncludeStatus.ImplRoot)
                {
                    sortedImplTypes.Add(type.Name, type);
                    continue;
                }

                type.Serialize(writer);
            }

            foreach (ThinType type in sortedImplTypes.Values)
            {
                type.Serialize(writer);
            }

            foreach (ThinTypeForwarder typeForwarder in sortedTypeForwarders.Values)
            {
                typeForwarder.Serialize(writer);
            }
            writer.WriteEndElement();
        }

        public override string ToString()
        {
            return Name;
        }
    }

    internal class ThinReader : ModelReader
    {
        //Dictionary<string, ThinAssembly> _assemblies;

        private ThinModel _thinModel;
        private ModelReaderOptions _options;

        public ThinReader(ThinModel thinModel, ModelReaderOptions options)
        {
            _thinModel = thinModel;
            _options = options;
        }

        public override bool IncludeBuild(string platform, string architecture, string flavor, string condition)
        {
            return HasPlatform(platform) &&
                    HasArchitecture(architecture) &&
                    HasFlavor(flavor) &&
                    _options.ParseCondition(condition);
        }

        public override AssemblyElement CreateAssemblyElement(ModelElement model, string assemblyName, IncludeStatus includeStatus)
        {
            includeStatus = AdjustIncludeStatus(includeStatus);
            ThinAssembly assembly = new ThinAssembly(_thinModel, assemblyName, includeStatus, null);
            _thinModel.Assemblies.Add(assembly.Name, assembly);
            return assembly;
        }

        public override TypeElement CreateTypeElement(AssemblyElement assembly, string typeName,
                                                      IncludeStatus includeStatus, VisibilityOverride visibilityOverride,
                                                      SecurityTransparencyStatus securityTransparencyStatus)
        {
            ThinAssembly declaringAssembly = (ThinAssembly)assembly;
            includeStatus = AdjustIncludeStatus(includeStatus);
            ThinType type = new ThinType(declaringAssembly, typeName, includeStatus, null, visibilityOverride, securityTransparencyStatus);

            String key = type.Name;
            if (declaringAssembly.Types.ContainsKey(key))
            {
                String errMsg = String.Format("XML file error: duplicate Type element \"{0}\"", key);
                Console.Error.WriteLine(errMsg);
                throw new FormatException(errMsg);
            }

            declaringAssembly.Types.Add(key, type);

            return type;
        }

        public override TypeForwarderElement CreateTypeForwarderElement(AssemblyElement parent, string assemblyName, string typeName,
                                                                        IncludeStatus includeStatus)
        {
            ThinAssembly declaringAssembly = (ThinAssembly)parent;
            includeStatus = AdjustIncludeStatus(includeStatus);
            ThinTypeForwarder typeForwarder = new ThinTypeForwarder(declaringAssembly, assemblyName, typeName, includeStatus, null);

            String key = typeForwarder.Key;
            if (declaringAssembly.Types.ContainsKey(key))
            {
                String errMsg = String.Format("XML file error: duplicate TypeForwarder element for \"{0}\"", key);
                Console.Error.WriteLine(errMsg);
                throw new FormatException(errMsg);
            }

            declaringAssembly.TypeForwarders.Add(key, typeForwarder);

            return typeForwarder;
        }

        public override MemberElement CreateMemberElement(TypeElement type, string memberName, string returnType, MemberTypes memberType,
                                                          IncludeStatus includeStatus, VisibilityOverride visibilityOverride,
                                                          SecurityTransparencyStatus securityTransparencyStatus)
        {
            ThinType declaringType = (ThinType)type;
            includeStatus = AdjustIncludeStatus(includeStatus);
            ThinMember member = new ThinMember(declaringType, memberName, returnType, memberType,
                                               includeStatus, null, visibilityOverride, securityTransparencyStatus);

            String key = member.Key;
            if (declaringType.Members.ContainsKey(key))
            {
                String errMsg = String.Format("XML file error: duplicate Member element \"{0}\" in type \"{1}\"", key, type.Key);
                Console.Error.WriteLine(errMsg);
                throw new FormatException(errMsg);
            }

            declaringType.Members.Add(key, member);
            return member;
        }

        private bool HasPlatform(string platform)
        {
            if (_options.Platform == null || platform == null)
                return true;

            char[] separaters = new char[] { ',' };

            string[] platforms = null;
            if (platform != null)
                platforms = platform.Split(separaters);

            return Contains(platforms, _options.Platform);
        }

        private bool HasArchitecture(string architecture)
        {
            if (_options.Architecture == null || architecture == null)
                return true;

            char[] separaters = new char[] { ',' };

            string[] architectures = null;
            if (architecture != null)
                architectures = architecture.Split(separaters);

            return Contains(architectures, _options.Architecture);
        }

        private bool HasFlavor(string flavor)
        {
            if (_options.Flavor == null || flavor == null)
                return true;

            char[] separaters = new char[] { ',' };

            string[] flavors = null;
            if (flavor != null)
                flavors = flavor.Split(separaters);

            return Contains(flavors, _options.Flavor);
        }

        private IncludeStatus AdjustIncludeStatus(IncludeStatus includeStatus)
        {
            if (_options.TreatFxInternalAsPublic && includeStatus == IncludeStatus.ApiFxInternal)
                return IncludeStatus.ApiRoot;

            return includeStatus;
        }

        private static bool Contains(string[] list, string token)
        {
            foreach (string s in list)
                if (string.Compare(s, token, true) == 0)
                    return true;

            return false;
        }
    }  // class ThinReade

    public struct ThinnerOptions
    {
        public ThinnerOptions(HostEnvironment environment, IEnumerable<AssemblyIdentity> includedAssemblies)
            : this()
        {
            Environment = environment;
            IncludedAssemblies = includedAssemblies;
        }

        public IEnumerable<AssemblyIdentity> IncludedAssemblies;
        public HostEnvironment Environment;
    }

    public struct ModelReaderOptions
    {
        public ModelReaderOptions(String platform, String architecture, String flavor, Boolean treatFxInternalAsPublic, String defines)
            : this()
        {
            Platform = platform;
            Architecture = architecture;
            Flavor = flavor;
            TreatFxInternalAsPublic = treatFxInternalAsPublic;
            if (defines != null && defines.Length > 0)
                _conditionParser = new ConditionParser(defines.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
        }

        public bool ParseCondition(String condition)
        {
            if (condition == null || _conditionParser == null)
                return true;

            return _conditionParser.Parse(condition);
        }

        public String Platform { get; set; }
        public String Architecture { get; set; }
        public String Flavor { get; set; }
        public Boolean TreatFxInternalAsPublic { get; set; }
        private ConditionParser _conditionParser;
    }

    public class ThinModel : ModelElement
    {
        private ThinnerOptions _options;

        private Dictionary<string, AssemblyElement> _assemblies = new Dictionary<string, AssemblyElement>();

        public override IDictionary<string, AssemblyElement> Assemblies
        {
            get { return _assemblies; }
        }

        public ThinnerOptions Options
        {
            get { return _options; }
        }

        public ThinModel()
            : this(new ThinnerOptions())
        {
        }

        public ThinModel(ThinnerOptions options)
        {
            _options = options;
            // Initialize defaults
            if (_options.Environment == null)
            {
                _options.Environment = new HostEnvironment(new Microsoft.Cci.NameTable());
            }
        }

        //public bool LoadOldModel(string xmlInputFileName)
        //{
        //    XmlReaderSettings settings = new XmlReaderSettings();
        //    settings.IgnoreComments = true;
        //    settings.IgnoreProcessingInstructions = true;
        //    settings.IgnoreWhitespace = true;
        //    XmlReader reader = XmlReader.Create(xmlInputFileName, settings);
        //    LegacyReader.RecursivelyReadBlock(reader, _assemblies);
        //    reader.Close();
        //    return true;
        //} 

        public bool LoadModel(string xmlInputFileName, ModelReaderOptions options)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreComments = true;
            settings.IgnoreProcessingInstructions = true;
            settings.IgnoreWhitespace = true;
            XmlReader reader = XmlReader.Create(xmlInputFileName, settings);
            ThinReader modelReader = new ThinReader(this, options);
            modelReader.RecursivelyReadBlock(reader, this, true);
            reader.Dispose();
            return true;
        }

        public void LoadFrom(ModelElement otherModel)
        {
            AddElementsFrom(otherModel, true);
        }

        public void SaveModel(string xmlOutputFileName)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            using (FileStream sourceStream = File.OpenWrite(xmlOutputFileName))
            {
                XmlWriter writer = XmlWriter.Create(sourceStream, settings);
                Serialize(writer);
                writer.Dispose();
            }
        }

        public void LoadMetadata()
        {
            foreach (ThinAssembly assembly in _assemblies.Values)
            {
                assembly.LoadMetadata();
            }
        }

        public void LoadMetadataFrom(IAssembly assemblyMetadata)
        {
            foreach (ThinAssembly assembly in _assemblies.Values)
            {
                if (assembly.Name == assemblyMetadata.Name.Value)
                {
                    assembly.LoadMetadataFrom(assemblyMetadata);
                }
            }
        }

        public void Serialize(XmlWriter writer)
        {
            writer.WriteStartElement(XmlKeywords.Elements.ThinModel);
            foreach (ThinAssembly assem in _assemblies.Values)
            {
                assem.Serialize(writer);
            }
            writer.WriteEndElement();
        }

        public ThinModel CalculateImplementationClosure(bool isCSharp, FieldOptions fieldOptions)
        {
            ImplementationModel implModel = new ImplementationModel(this);

            implModel.ImportRoots(IncludeStatus.ImplRoot);
            implModel.ImportRoots(IncludeStatus.ApiRoot);
            implModel.ImportRoots(IncludeStatus.ApiClosure);
            implModel.ImportRoots(IncludeStatus.ApiFxInternal);

            //implModel.PrintStats();
            implModel.CalculateImplementationClosure(isCSharp, fieldOptions);

            return implModel.ExportModel(IncludeStatus.ImplClosure);
        }

        public ThinModel CalculateApiClosure()
        {
            ImplementationModel apiModel = new ImplementationModel(this);

            apiModel.ImportRoots(IncludeStatus.ApiRoot);
            apiModel.ImportRoots(IncludeStatus.ApiFxInternal);

            //apiModel.PrintStats();
            apiModel.CalculateApiClosure();

            ThinModel apiClosure = apiModel.ExportModel(IncludeStatus.ApiClosure);
            CombineIncludeStatusWithModel(IncludeStatus.ImplRoot, apiClosure);
            return apiClosure;
        }

        private void CombineIncludeStatusWithModel(IncludeStatus targetStatusToAdd, ThinModel otherModel)
        {
            // TODO: which one should we keep, the one in this model or the other model?
            foreach (ThinAssembly assembly in Assemblies.Values)
            {
                AssemblyElement newAssembly;
                if (!otherModel.Assemblies.TryGetValue(assembly.Name, out newAssembly) &&
                    (assembly.IncludeStatus == targetStatusToAdd))
                {
                    newAssembly = new ThinAssembly(otherModel, assembly);
                    otherModel.Assemblies.Add(newAssembly.Key, newAssembly);
                }

                foreach (ThinTypeForwarder typeForwarder in assembly.TypeForwarders.Values)
                {
                    TypeForwarderElement newTypeForwarder;
                    if (!newAssembly.TypeForwarders.TryGetValue(typeForwarder.Key, out newTypeForwarder) &&
                        (typeForwarder.IncludeStatus == targetStatusToAdd))
                    {
                        newTypeForwarder = new ThinTypeForwarder((ThinAssembly)newAssembly, typeForwarder);
                        newAssembly.TypeForwarders.Add(newTypeForwarder.Key, newTypeForwarder);
                    }
                }

                foreach (ThinType type in assembly.Types.Values)
                {
                    TypeElement newType;
                    if (!newAssembly.Types.TryGetValue(type.Name, out newType) &&
                        (type.IncludeStatus == targetStatusToAdd))
                    {
                        newType = new ThinType((ThinAssembly)newAssembly, type);
                        newAssembly.Types.Add(newType.Key, newType);
                    }
                    foreach (ThinMember member in type.Members.Values)
                    {
                        MemberElement newMember;
                        if (!newType.Members.TryGetValue(member.Key, out newMember) &&
                            (member.IncludeStatus == targetStatusToAdd))
                        {
                            newMember = new ThinMember((ThinType)newType, member);
                            newType.Members.Add(newMember.Key, newMember);
                        }
                    }
                }
            }
        }

        //public ThinModel DumpApi()
        //{
        //    ThinModel model = new ThinModel();

        //    foreach (ThinAssembly assembly in Assemblies.Values)
        //    {
        //        ThinAssembly newAssembly = new ThinAssembly(assembly.Name, IncludeStatus.Unknown, assembly.Metadata);
        //        model.Assemblies.Add(newAssembly.Name, newAssembly);
        //        foreach (INamedTypeDefinition type in newAssembly.Metadata.Types)
        //        {
        //            if (type.IsPublic)
        //            {
        //                ThinType thinType = new ThinType(newAssembly, type.ToString(), IncludeStatus.Unknown, type, VisibilityOverride.None);
        //                newAssembly.Types.Add(thinType.Name, thinType);

        //                DumpMembers(type, newAssembly, thinType);
        //            }
        //        }
        //    }

        //    return model;
        //}

        public void AddElementsFrom(ModelElement otherModel, bool overwriteExisting)
        {
            foreach (KeyValuePair<String, AssemblyElement> assembly in otherModel.Assemblies)
            {
                if (!_assemblies.ContainsKey(assembly.Key) || overwriteExisting)
                {
                    ThinAssembly newAssembly = new ThinAssembly(this, assembly.Value.Key, assembly.Value.IncludeStatus, null);
                    _assemblies[newAssembly.Key] = newAssembly;
                }

                ThinAssembly currentAssembly = (ThinAssembly)_assemblies[assembly.Key];

                foreach (var typeForwarder in assembly.Value.TypeForwarders)
                {
                    if (!currentAssembly.TypeForwarders.ContainsKey(typeForwarder.Key) || overwriteExisting)
                    {
                        TypeForwarderElement forwarder = typeForwarder.Value;
                        ThinTypeForwarder newTypeForwarder = new ThinTypeForwarder(currentAssembly, forwarder.AssemblyName, forwarder.TypeName,
                                                                                   forwarder.IncludeStatus, null);
                    }
                }
                foreach (KeyValuePair<String, TypeElement> type in assembly.Value.Types)
                {
                    if (!currentAssembly.Types.ContainsKey(type.Key) || overwriteExisting)
                    {
                        ThinType newType = new ThinType(currentAssembly, type.Value.Key,
                                                        type.Value.IncludeStatus, null, type.Value.VisibilityOverride);
                        currentAssembly.Types[newType.Key] = newType;
                    }
                    ThinType currentType = (ThinType)currentAssembly.Types[type.Key];

                    foreach (KeyValuePair<String, MemberElement> member in type.Value.Members)
                    {
                        if (!currentType.Members.ContainsKey(member.Key) || overwriteExisting)
                        {
                            ThinMember newMember = new ThinMember(currentType, member.Value.Name, member.Value.ReturnType, member.Value.MemberType,
                                                                  member.Value.IncludeStatus, null, member.Value.VisibilityOverride);
                            currentType.Members[newMember.Key] = newMember;
                        }
                    }
                }
            }
        }

        //public void AnnotateWithSecurity(string mscorlibPath, string inputFileName, string securityAnnotationsFileName)
        //{
        //    AssemblyAnnotations inputAnnotations = new AssemblyAnnotations(mscorlibPath, securityAnnotationsFileName);

        //    foreach (ThinAssembly assembly in Assemblies.Values)
        //    {
        //        foreach (ThinType thinType in assembly.Types.Values)
        //        {
        //            UpdateSecurityTransparencyStatus(thinType, inputAnnotations, thinType.Metadata);
        //            AnnotateMembers(inputAnnotations, thinType);
        //        }
        //    }

        //}

        //public void AnnotateMembers(AssemblyAnnotations inputAnnotations, ThinType thinType)
        //{
        //    INamedTypeDefinition type = thinType.Metadata;
        //    foreach (ThinMember thinMember in thinType.Members.Values)
        //    {
        //        // cannot have nested classes in ThinType
        //        UpdateSecurityTransparencyStatus(thinMember, inputAnnotations, thinMember.Metadata);
        //    }
        //}

        //private void UpdateSecurityTransparencyStatus(ThinType thinType, AssemblyAnnotations inputAnnotations, ITypeDefinitionMember member)
        //{
        //    SecurityAnnotations sa;
        //    if (member != null)
        //    {
        //        sa = inputAnnotations.TryGetAnnotations(FxCopHelper.UniqueMemberId(FxCopHelper.MemberFixupForGenerics(member)));
        //    }
        //    else
        //    {
        //        // For some reason we could not load metadata node for this member (shows up as ...: ~0 matches in
        //        // output of thinner while loading XML
        //        Console.WriteLine("WARNING: NULL MetaData for {0}", thinType.Name);
        //        sa = inputAnnotations.TryGetAnnotations(thinType.Name);
        //    }
        //    if (sa == null)
        //    {
        //        return;
        //    }
        //    if (sa.TreatAsSafe)
        //    {
        //        thinType.SetSecurityTransparencyStatus(SecurityTransparencyStatus.SafeCritical);
        //    }
        //    else if (sa.Critical)
        //    {
        //        thinType.SetSecurityTransparencyStatus(SecurityTransparencyStatus.Critical);
        //    }

        //}

        //private void UpdateSecurityTransparencyStatus(ThinMember thinMember, AssemblyAnnotations inputAnnotations, ITypeDefinitionMember member)
        //{
        //    SecurityAnnotations sa;
        //    if (member != null)
        //    {
        //        sa = inputAnnotations.TryGetAnnotations(FxCopHelper.UniqueMemberId(FxCopHelper.MemberFixupForGenerics(member)));
        //    }
        //    else
        //    {
        //        // For some reason we could not load metadata node for this member (shows up as ...: ~0 matches in
        //        // output of thinner while loading XML
        //        Console.WriteLine("WARNING: NULL MetaData for {0}", thinMember.Name);
        //        sa = inputAnnotations.TryGetAnnotations(thinMember.Name);
        //    }
        //    if (sa == null)
        //    {
        //        return;
        //    }
        //    if (sa.TreatAsSafe)
        //    {
        //        thinMember.SetSecurityTransparencyStatus(SecurityTransparencyStatus.SafeCritical);
        //    }
        //    else if (sa.Critical)
        //    {
        //        thinMember.SetSecurityTransparencyStatus(SecurityTransparencyStatus.Critical);
        //    }

        //}

        // TODO: Consolidate with RemoveElementsPresentIn?
        public ICollection<Element> FindExclusionViolations(ModelElement excludeModel)
        {
            List<Element> result = new List<Element>();
            foreach (KeyValuePair<String, AssemblyElement> assembly in excludeModel.Assemblies)
            {
                if (!_assemblies.ContainsKey(assembly.Key)) continue;
                if (assembly.Value.IncludeStatus == IncludeStatus.Exclude)
                {
                    result.Add(assembly.Value);
                    continue;
                }
                AssemblyElement currentAssembly = _assemblies[assembly.Key];

                foreach (KeyValuePair<String, TypeForwarderElement> typeForwarder in assembly.Value.TypeForwarders)
                {
                    if (!currentAssembly.TypeForwarders.ContainsKey(typeForwarder.Key)) continue;
                    if (typeForwarder.Value.IncludeStatus == IncludeStatus.Exclude)
                    {
                        result.Add(typeForwarder.Value);
                        continue;
                    }
                }

                foreach (KeyValuePair<String, TypeElement> type in assembly.Value.Types)
                {
                    if (!currentAssembly.Types.ContainsKey(type.Key)) continue;
                    if (type.Value.IncludeStatus == IncludeStatus.Exclude)
                    {
                        result.Add(type.Value);
                        continue;
                    }
                    TypeElement currentType = currentAssembly.Types[type.Key];

                    foreach (KeyValuePair<String, MemberElement> member in type.Value.Members)
                    {
                        if (!currentType.Members.ContainsKey(member.Key)) continue;
                        if (member.Value.IncludeStatus == IncludeStatus.Exclude)
                        {
                            result.Add(member.Value);
                        }
                    }
                }
            }
            return result;
        }

        public void RemoveElementsPresentIn(ModelElement otherModel)
        {
            foreach (KeyValuePair<String, AssemblyElement> assembly in otherModel.Assemblies)
            {
                if (!_assemblies.ContainsKey(assembly.Key))  // Can't remove anything that doesn't exist
                    throw new Exception(String.Format("Invalid assembly defined in exclude list:  {0}", assembly.Value.ToString()));

                if (assembly.Value.IncludeStatus == IncludeStatus.Exclude)
                {
                    _assemblies.Remove(assembly.Key);
                    continue; // Once we have removed the assembly, all nested types are automatically removed so we move to the next assembly
                }
                AssemblyElement currentAssembly = _assemblies[assembly.Key];

                foreach (KeyValuePair<String, TypeForwarderElement> typeForwarder in assembly.Value.TypeForwarders)
                {
                    if (!currentAssembly.TypeForwarders.ContainsKey(typeForwarder.Key))  // Can't remove anything that doesn't exist
                        throw new Exception(String.Format("Invalid type forwarder defined in exclude list: {0}",
                                                                     typeForwarder.Value.ToString()));

                    if (typeForwarder.Value.IncludeStatus == IncludeStatus.Exclude)
                    {
                        currentAssembly.TypeForwarders.Remove(typeForwarder.Key);
                        continue;
                    }
                }

                foreach (KeyValuePair<String, TypeElement> type in assembly.Value.Types)
                {
                    if (!currentAssembly.Types.ContainsKey(type.Key))  // Can't remove anything that doesn't exist
                        throw new Exception(String.Format("Invalid type defined in exclude list: {0}", type.Value.ToString()));

                    if (type.Value.IncludeStatus == IncludeStatus.Exclude)
                    {
                        currentAssembly.Types.Remove(type.Key);
                        continue; // Once we have removed the type, all nested members are automatically removed
                    }
                    TypeElement currentType = currentAssembly.Types[type.Key];

                    foreach (KeyValuePair<String, MemberElement> member in type.Value.Members)
                    {
                        if (!currentType.Members.ContainsKey(member.Key))  // Can't remove anything that doesn't exist
                            throw new Exception(String.Format("Invalid member defined in exclude list: {0}", member.Value.ToString()));

                        if (member.Value.IncludeStatus == IncludeStatus.Exclude)
                        {
                            currentType.Members.Remove(member.Key);
                        }
                    }
                }
            }
        }
    }
}
