// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Tools.Transformer.CodeModel
{
    public enum VisibilityOverride
    {
        None,
        Internal,
    }

    public enum MemberTypes
    {
        Unknown,
        Event,
        Field,
        Method,
        Property,
        Type
    }

    public enum IncludeStatus
    {
        Inherit,
        ApiRoot,
        ApiClosure,
        ApiFxInternal,
        ImplRoot,
        ImplClosure,
        Exclude
    }

    public enum SecurityTransparencyStatus
    {
        Transparent,
        Critical,
        SafeCritical,
        Undefined
    }

    public class XmlKeywords
    {
        public class Elements
        {
            public const string Xml = "xml";
            public const string ThinModel = "ThinModel";
            public const string Assembly = "Assembly";
            public const string Type = "Type";
            public const string TypeForwarder = "TypeForwarder";
            public const string Member = "Member";
        }

        public class Attributes
        {
            public const string IncludeStatus = "Status";
            public const string MemberType = "MemberType";
            public const string Name = "Name";
            public const string ReturnType = "ReturnType";
            public const string VisibilityOverride = "VO";
            public const string SecurityTransparencyStatus = "SecurityTransparencyStatus";
            public const string Platform = "Platform";
            public const string Architecture = "Architecture";
            public const string Flavor = "Flavor";
            public const string Condition = "Condition";
            public const string AssemblyName = "AssemblyName";
            public const string TypeName = "TypeName";
        }

        internal static IncludeStatus ParseIncludeStatus(string value)
        {
            return (IncludeStatus)Enum.Parse(typeof(IncludeStatus), value);
        }

        internal static MemberTypes ParseMemberType(string value)
        {
            switch (value)
            {
                case "Event":
                    return MemberTypes.Event;
                case "Field":
                    return MemberTypes.Field;
                case "Method":
                    return MemberTypes.Method;
                case "Property":
                    return MemberTypes.Property;
                default:
                    return MemberTypes.Method;      // METHOD is the default!
            }
        }

        internal static VisibilityOverride ParseVisibilityOverride(string value)
        {
            switch (value)
            {
                case "internal":
                    return VisibilityOverride.Internal;
                default:
                    return VisibilityOverride.None;
            }
        }
        internal static SecurityTransparencyStatus ParseSecurityTransparencyStatus(string value)
        {
            switch (value)
            {
                case "Critical":
                    return SecurityTransparencyStatus.Critical;
                case "SafeCritical":
                    return SecurityTransparencyStatus.SafeCritical;
                default:
                    return SecurityTransparencyStatus.Transparent;
            }
        }
    }

    abstract public class Element
    {
        public abstract IncludeStatus IncludeStatus { get; }
        public abstract VisibilityOverride VisibilityOverride { get; }
        public abstract SecurityTransparencyStatus SecurityTransparencyStatus { get; }
        public abstract string Key { get; }

        public bool ShouldMakeInternal
        {
            get
            {
                return VisibilityOverride == VisibilityOverride.Internal ||
                       IncludeStatus == IncludeStatus.ApiFxInternal;
            }
        }

        public bool IsFriendAccessAllowed
        {
            get
            {
                return IncludeStatus == IncludeStatus.ApiFxInternal;
            }
        }
    }

    abstract public class ModelElement : Element
    {
        public override IncludeStatus IncludeStatus { get { throw new NotImplementedException(); } }
        public override VisibilityOverride VisibilityOverride { get { throw new NotImplementedException(); } }
        public override SecurityTransparencyStatus SecurityTransparencyStatus { get { throw new NotImplementedException(); } }
        public override string Key { get { throw new NotImplementedException(); } }
        public abstract IDictionary<String, AssemblyElement> Assemblies { get; }
    }

    abstract public class AssemblyElement : Element
    {
        public override VisibilityOverride VisibilityOverride { get { throw new NotImplementedException(); } }
        public override SecurityTransparencyStatus SecurityTransparencyStatus { get { throw new NotImplementedException(); } }
        public abstract IDictionary<String, TypeElement> Types { get; }
        public abstract IDictionary<String, TypeForwarderElement> TypeForwarders { get; }

        public TypeElement GetTypeElement(String typeName)
        {
            TypeElement result = null;
            Types.TryGetValue(typeName, out result);
            return result;
        }

        public TypeForwarderElement GetTypeForwarderElement(String assemblyName, String typeName)
        {
            TypeForwarderElement result = null;
            TypeForwarders.TryGetValue(Util.GetTypeForwarderSignature(assemblyName, typeName), out result);
            return result;
        }
    }

    abstract public class TypeElement : Element
    {
        public abstract IDictionary<String, MemberElement> Members { get; }
    }

    abstract public class TypeForwarderElement : Element
    {
        public abstract String AssemblyName { get; }
        public abstract String TypeName { get; }

        public override String Key
        {
            get { return Util.GetTypeForwarderSignature(AssemblyName, TypeName); }
        }
    }

    abstract public class MemberElement : Element
    {
        public abstract String Name { get; }
        public abstract String ReturnType { get; }
        public abstract MemberTypes MemberType { get; }
    }

    internal class CodeModelElement : ModelElement
    {
        internal Dictionary<string, AssemblyElement> m_assemblies;

        internal CodeModelElement()
        {
            m_assemblies = new Dictionary<string, AssemblyElement>();
        }

        public override IDictionary<String, AssemblyElement> Assemblies
        {
            get { return m_assemblies; }
        }
    }

    internal class CodeAssemblyElement : AssemblyElement
    {
        internal Dictionary<string, TypeElement> m_types;
        internal Dictionary<string, TypeForwarderElement> m_typeForwarders;

        private IncludeStatus _includeStatus;
        private string _key;

        internal CodeAssemblyElement(IncludeStatus includeStatus, string key)
        {
            m_types = new Dictionary<string, TypeElement>();
            m_typeForwarders = new Dictionary<string, TypeForwarderElement>();

            _includeStatus = includeStatus;
            _key = key;
        }

        public override String Key { get { return _key; } }
        public override IncludeStatus IncludeStatus { get { return _includeStatus; } }
        public override IDictionary<String, TypeElement> Types { get { return m_types; } }
        public override IDictionary<String, TypeForwarderElement> TypeForwarders { get { return m_typeForwarders; } }
    }

    internal class CodeTypeElement : TypeElement
    {
        internal Dictionary<string, MemberElement> m_members;

        private IncludeStatus _includeStatus;
        private VisibilityOverride _visibilityOverride;
        private SecurityTransparencyStatus _securityTransparencyStatus;
        private string _key;

        internal CodeTypeElement(IncludeStatus includeStatus, VisibilityOverride visibilityOverride, SecurityTransparencyStatus securityTransparencyStatus, string key)
        {
            m_members = new Dictionary<string, MemberElement>();

            _includeStatus = includeStatus;
            _visibilityOverride = visibilityOverride;
            _securityTransparencyStatus = securityTransparencyStatus;
            _key = key;
        }

        public override String Key { get { return _key; } }
        public override IncludeStatus IncludeStatus { get { return _includeStatus; } }
        public override VisibilityOverride VisibilityOverride { get { return _visibilityOverride; } }
        public override SecurityTransparencyStatus SecurityTransparencyStatus { get { return _securityTransparencyStatus; } }
        public override IDictionary<String, MemberElement> Members { get { return m_members; } }
    }

    internal class CodeTypeForwarderElement : TypeForwarderElement
    {
        private IncludeStatus _includeStatus;
        private string _assemblyName;
        private string _typeName;

        internal CodeTypeForwarderElement(IncludeStatus includeStatus, string assemblyName, string typeName)
        {
            _includeStatus = includeStatus;
            _assemblyName = assemblyName;
            _typeName = typeName;
        }

        public override string AssemblyName
        {
            get { return _assemblyName; }
        }
        public override string TypeName
        {
            get { return _typeName; }
        }
        public override SecurityTransparencyStatus SecurityTransparencyStatus
        {
            get { throw new NotSupportedException(); }
        }
        public override VisibilityOverride VisibilityOverride
        {
            get { throw new NotSupportedException(); }
        }
        public override IncludeStatus IncludeStatus { get { return _includeStatus; } }
    }

    internal class CodeMemberElement : MemberElement
    {
        private IncludeStatus _includeStatus;
        private VisibilityOverride _visibilityOverride;
        private SecurityTransparencyStatus _securityTransparencyStatus;
        private string _key;
        private string _returnType;
        private MemberTypes _memberType;
        private string _name;

        public override string Name
        {
            get { return _name; }
        }

        public CodeMemberElement(IncludeStatus includeStatus, VisibilityOverride visibilityOverride, SecurityTransparencyStatus securityTransparencyStatus, string key, string returnType, MemberTypes memberType, string name)
        {
            _includeStatus = includeStatus;
            _visibilityOverride = visibilityOverride;
            _securityTransparencyStatus = securityTransparencyStatus;
            _key = key;
            _returnType = returnType;
            _memberType = memberType;
            _name = name;
        }

        public override String Key { get { return _key; } }
        public override IncludeStatus IncludeStatus { get { return _includeStatus; } }
        public override VisibilityOverride VisibilityOverride { get { return _visibilityOverride; } }
        public override SecurityTransparencyStatus SecurityTransparencyStatus { get { return _securityTransparencyStatus; } }
        public override MemberTypes MemberType { get { return _memberType; } }
        public override string ReturnType { get { return _returnType; } }
    }
}
