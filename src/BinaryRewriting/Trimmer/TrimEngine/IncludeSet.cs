// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using Microsoft.Tools.Transformer.CodeModel;
using ModelFileToCCI2;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace TrimBin
{
    public class IncludeSet : ModelElement
    {
        public IncludeSet()
        {
            //_types = new Dictionary<string, TrimType>();
            _assemblies = new Dictionary<string, AssemblyElement>();
        }

        public override IDictionary<string, AssemblyElement> Assemblies
        {
            get { return _assemblies; }
        }

        public void LoadFrom(ModelElement otherModel)
        {
            foreach (AssemblyElement assembly in otherModel.Assemblies.Values)
            {
                TrimAssembly newAssembly = new TrimAssembly(assembly.Key, assembly.IncludeStatus);
                _assemblies.Add(newAssembly.Name, newAssembly);
                foreach (KeyValuePair<String, TypeForwarderElement> typeForwarder in assembly.TypeForwarders)
                {
                    TrimTypeForwarder newTypeForwarder = new TrimTypeForwarder(typeForwarder.Value.AssemblyName, typeForwarder.Value.TypeName, typeForwarder.Value.IncludeStatus);
                    newAssembly.TypeForwarders.Add(newTypeForwarder.Key, newTypeForwarder);
                }
                foreach (KeyValuePair<String, TypeElement> type in assembly.Types)
                {
                    TrimType newType = new TrimType(type.Value.Key, type.Value.IncludeStatus, type.Value.VisibilityOverride, type.Value.SecurityTransparencyStatus);
                    newAssembly.Types.Add(newType.Key, newType);
                    foreach (KeyValuePair<String, MemberElement> member in type.Value.Members)
                    {
                        TrimMember newMember = new TrimMember(newType, member.Value.Name, member.Value.ReturnType, member.Value.MemberType, member.Value.IncludeStatus, member.Value.VisibilityOverride, member.Value.SecurityTransparencyStatus);
                        newType.Members.Add(newMember.Key, newMember);
                    }
                }
            }
        }

        public void ReadIncludeFile(String includeFile)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreComments = true;
            settings.IgnoreProcessingInstructions = true;
            settings.IgnoreWhitespace = true;
            XmlReader reader = XmlReader.Create(includeFile, settings);

            ModelReader modelReader = new TrimReader(/*_types*/);
            modelReader.RecursivelyReadBlock(reader, this, true);
        }

        public void AddAssembly(TrimAssembly assembly)
        {
            _assemblies.Add(assembly.Key, assembly);
        }

        public TrimAssembly GetAssemblyElement(String assemblyName)
        {
            AssemblyElement result = null;
            _assemblies.TryGetValue(assemblyName, out result);
            return result as TrimAssembly;
        }

        public ICollection<AssemblyElement> GetAllAssemblies()
        {
            return _assemblies.Values;
        }

        private static void MangleTypeNameError(string fqtn)
        {
            throw new Exception("error parsing type name: " + fqtn);
        }

        private Dictionary<string, AssemblyElement> _assemblies;
        //private Dictionary<string, TrimType> _types;

        private const string FIELD_PREFIX = "Field: ";
        private const string PROPERTY_PREFIX = "Property: ";
        private const string EVENT_PREFIX = "Event: ";
    }

    public class TrimAssembly : AssemblyElement
    {
        public TrimAssembly(string assemblyName, IncludeStatus status)
        {
            _name = assemblyName;
            _includeStatus = status;

            _types = new Dictionary<string, TypeElement>();
            _typeForwarders = new Dictionary<string, TypeForwarderElement>();
        }

        public override IncludeStatus IncludeStatus { get { return _includeStatus; } }

        public override string Key
        {
            get { return _name; }
        }

        public string Name
        {
            get { return _name; }
        }

        public override IDictionary<string, TypeForwarderElement> TypeForwarders
        {
            get { return _typeForwarders; }
        }

        private string _name;
        private IncludeStatus _includeStatus;
        private Dictionary<string, TypeElement> _types;
        private Dictionary<string, TypeForwarderElement> _typeForwarders;

        public override IDictionary<string, TypeElement> Types
        {
            get { return _types; }
        }
    }

    public class TrimType : TypeElement
    {
        public TrimType(String typeName, IncludeStatus includeStatus, VisibilityOverride visibilityOverride, SecurityTransparencyStatus securityTransparencyStatus)
        {
            _typeName = typeName;
            _typeMembers = new Dictionary<string, MemberElement>();
            _includeStatus = includeStatus;
            _visibilityOverride = visibilityOverride;
            _securityTransparencyStatus = securityTransparencyStatus;
        }

        public override IncludeStatus IncludeStatus { get { return _includeStatus; } }
        public override VisibilityOverride VisibilityOverride { get { return _visibilityOverride; } }
        public override SecurityTransparencyStatus SecurityTransparencyStatus { get { return _securityTransparencyStatus; } }

        public override string Key
        {
            get
            {
                return _typeName;
            }
        }

        public virtual TrimMember GetMemberElementFromMember(ITypeMemberReference member)
        {
            if (member == null || member.GetType().ToString().Contains("Dummy"))
            { throw new ArgumentNullException("member"); }
            MemberElement memberElement = null;
            _typeMembers.TryGetValue(Util.MemberKeyFromMember(member), out memberElement);

            return memberElement as TrimMember;
        }
        public String TypeName { get { return _typeName; } }

        public void AddMember(TrimMember member)
        {
            _typeMembers.Add(member.Key, member);
        }

        public override IDictionary<string, MemberElement> Members
        {
            get { return _typeMembers; }
        }

        private String _typeName;
        private Dictionary<string, MemberElement> _typeMembers;
        private IncludeStatus _includeStatus;
        private VisibilityOverride _visibilityOverride;
        private SecurityTransparencyStatus _securityTransparencyStatus;
    }

    public class TrimTypeForwarder : TypeForwarderElement
    {
        private String _assemblyName;
        private String _typeName;
        private IncludeStatus _includeStatus;

        public TrimTypeForwarder(String assemblyName, String typeName, IncludeStatus includeStatus)
        {
            _assemblyName = assemblyName;
            _typeName = typeName;
            _includeStatus = includeStatus;
        }

        public override IncludeStatus IncludeStatus { get { return _includeStatus; } }
        public override VisibilityOverride VisibilityOverride { get { throw new NotSupportedException(); } }
        public override SecurityTransparencyStatus SecurityTransparencyStatus { get { throw new NotSupportedException(); } }

        public override string AssemblyName
        {
            get { return _assemblyName; }
        }

        public override string TypeName
        {
            get { return _typeName; }
        }
    }

    // Represents a special type that's not included in model.xml.
    public class SpecialTrimType : TrimType
    {
        public SpecialTrimType(string typeName)
            : base(typeName, IncludeStatus.ImplRoot, VisibilityOverride.None, SecurityTransparencyStatus.Transparent)
        {
        }

        public override TrimMember GetMemberElementFromMember(ITypeMemberReference member)
        {
            return new SpecialTrimMember(this, member.Name.Value, null, Util.GetMemberTypeFromMember(member));
        }
    }

    public class TrimMember : MemberElement
    {
        public TrimMember(TrimType declaringType, string memberName, string returnType, /*string paramListString, */MemberTypes memberType, IncludeStatus includeStatus, VisibilityOverride visibilityOverride, SecurityTransparencyStatus securityTransparencyStatus)
        {
            _declaringType = declaringType;
            _memberName = memberName;
            _returnType = returnType;
            _memberType = memberType;
            _includeStatus = includeStatus;
            _securityTransparencyStatus = securityTransparencyStatus;
            //_paramListString = paramListString;
            //if (null == paramListString)
            //    _paramQualifiedTypeNames = new List<string>();
            //else
            //    _paramQualifiedTypeNames = SplitParameterList(paramListString);
            _visibilityOverride = visibilityOverride;
        }

        public override string Name
        {
            get { return _memberName; }
        }

        public override string ReturnType
        {
            get { return _returnType; }
        }

        public override VisibilityOverride VisibilityOverride
        {
            get
            {
                return _visibilityOverride;
            }
        }

        public override SecurityTransparencyStatus SecurityTransparencyStatus { get { return _securityTransparencyStatus; } }

        public override IncludeStatus IncludeStatus { get { return _includeStatus; } }

        private TrimType _declaringType;
        private string _memberName;
        private string _returnType;
        private string _key;
        private MemberTypes _memberType;
        private IncludeStatus _includeStatus;
        //IList<string> _paramQualifiedTypeNames;
        //string _paramListString;
        private VisibilityOverride _visibilityOverride;
        private SecurityTransparencyStatus _securityTransparencyStatus;

        public TrimType DeclaringType { get { return _declaringType; } }
        public override MemberTypes MemberType { get { return _memberType; } }
        public string MemberName { get { return _memberName; } }
        //public IList<string> ParamQualifiedTypeNames { get { return _paramQualifiedTypeNames; } }
        //public string ParamListString { get { return _paramListString; } }

        public bool IsVisibleExternally
        {
            get
            {
                return (_includeStatus == IncludeStatus.ApiClosure || _includeStatus == IncludeStatus.ApiRoot || _includeStatus == IncludeStatus.ApiFxInternal);
            }
        }
        public override string Key
        {
            get
            {
                if (_key == null)
                {
                    StringBuilder sbKey = new StringBuilder();
                    sbKey.Append(MemberType.ToString());
                    sbKey.Append(" : ");
                    sbKey.Append(_memberName);

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
    }

    public class SpecialTrimMember : TrimMember
    {
        public SpecialTrimMember(TrimType trimType, string memberName, string returnType, MemberTypes memberType)
            : base(trimType, memberName, returnType, memberType, IncludeStatus.ImplRoot, VisibilityOverride.None, SecurityTransparencyStatus.Transparent)
        {
        }
    }

    internal class TrimReader : ModelReader
    {
        //Dictionary<string, TrimType> _types;
        //bool _changeVisibility;

        public TrimReader(/*Dictionary<string, TrimType> types*/)
        {
            //_types = types;
        }

        public override AssemblyElement CreateAssemblyElement(ModelElement model, string assemblyName, IncludeStatus includeStatus)
        {
            IncludeSet includeSet = (IncludeSet)model;
            TrimAssembly trimAssembly = new TrimAssembly(assemblyName, includeStatus); ;
            includeSet.AddAssembly(trimAssembly);
            return trimAssembly;
        }

        public override TypeElement CreateTypeElement(AssemblyElement assembly, string typeName, IncludeStatus includeStatus, VisibilityOverride visibilityOverride, SecurityTransparencyStatus securityTransparencyStatus)
        {
            TrimAssembly trimAssembly = (TrimAssembly)assembly;

            TrimType typeIncludeInfo = new TrimType(typeName, includeStatus, visibilityOverride, securityTransparencyStatus);
            trimAssembly.Types.Add(typeIncludeInfo.Key, typeIncludeInfo);
            return typeIncludeInfo;
        }

        public override TypeForwarderElement CreateTypeForwarderElement(AssemblyElement parent, string assemblyName, string typeName, IncludeStatus includeStatus)
        {
            TrimAssembly trimAssembly = (TrimAssembly)parent;
            TrimTypeForwarder typeForwarder = new TrimTypeForwarder(assemblyName, typeName, includeStatus);
            trimAssembly.TypeForwarders.Add(typeForwarder.Key, typeForwarder);
            return typeForwarder;
        }

        public override MemberElement CreateMemberElement(TypeElement type, string memberName, string returnType, MemberTypes memberType, IncludeStatus includeStatus, VisibilityOverride visibilityOverride, SecurityTransparencyStatus securityTransparencyStatus)
        {
            TrimType typeInfo = (TrimType)type;

            TrimMember id = new TrimMember(typeInfo, memberName, returnType, /*paramListString, */memberType, includeStatus, visibilityOverride, securityTransparencyStatus);
            typeInfo.Members.Add(id.Key, id);
            return id;
        }

        public override bool IncludeBuild(string platform, string architecture, string flavor, string condition)
        {
            // TODO: Trimmer currently doesn't support filtering. All our filtering for createMscorlibSmall is done in Thinner.
            return true;
        }
    }
}
