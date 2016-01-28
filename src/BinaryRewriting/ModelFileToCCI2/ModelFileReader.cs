// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Tools.Transformer.CodeModel;
using System;
using System.Xml;

namespace ModelFileToCCI2
{
    // Predicate of whether or not to include a model.xml entity based on platform/architecture/flavor string
    internal interface IIncludePredicate
    {
        bool Include(string platform, string architecture, string flavor, string condition);
    }

    // Override the XmlImporter reader class to read the model.xml as a bunch of nested dictionaries.
    internal class ModelFileReader : ModelReader
    {
        private IIncludePredicate _pred;

        internal CodeModelElement Model { get; private set; }

        internal ModelFileReader(string fileName, IIncludePredicate pred)
        {
            this.Model = new CodeModelElement();
            _pred = pred;
            ReadIncludeFile(fileName);
        }

        private void ReadIncludeFile(String includeFile)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreComments = true;
            settings.IgnoreProcessingInstructions = true;
            settings.IgnoreWhitespace = true;
            XmlReader xmlReader = XmlReader.Create(includeFile, settings);

            RecursivelyReadBlock(xmlReader, this.Model, true);
        }

        public override AssemblyElement CreateAssemblyElement(ModelElement model, string assemblyName, IncludeStatus includeStatus)
        {
            CodeAssemblyElement assembly = new CodeAssemblyElement(includeStatus, assemblyName);
            (model as CodeModelElement).m_assemblies.Add(assemblyName, assembly);
            return assembly;
        }

        public override TypeElement CreateTypeElement(AssemblyElement assembly, string typeName, IncludeStatus includeStatus, VisibilityOverride visibilityOverride, SecurityTransparencyStatus securityTransparencyStatus)
        {
            CodeTypeElement type = new CodeTypeElement(includeStatus, visibilityOverride, securityTransparencyStatus, typeName);
            (assembly as CodeAssemblyElement).m_types.Add(typeName, type);
            return type;
        }

        public override TypeForwarderElement CreateTypeForwarderElement(AssemblyElement parent, string assemblyName, string typeName, IncludeStatus includeStatus)
        {
            CodeTypeForwarderElement typeForwarder = new CodeTypeForwarderElement(includeStatus, assemblyName, typeName);
            (parent as CodeAssemblyElement).TypeForwarders.Add(typeForwarder.Key, typeForwarder);
            return typeForwarder;
        }

        public override MemberElement CreateMemberElement(TypeElement type, string memberName, string returnType, MemberTypes memberType, IncludeStatus includeStatus, VisibilityOverride visibilityOverride, SecurityTransparencyStatus securityTransparencyStatus)
        {
            // Identify the member by category (method/prop/field/event), name, and return type.  These
            // strings are what matched by ModelSigFormatter

            string memberSig = memberType.ToString() + " : " + memberName;
            if (returnType != null) memberSig = memberSig + " : " + returnType;

            CodeMemberElement member = new CodeMemberElement(includeStatus, visibilityOverride, securityTransparencyStatus, memberSig, returnType, memberType, memberName);
            (type as CodeTypeElement).m_members.Add(memberSig, member);
            return member;
        }

        public override bool IncludeBuild(string platform, string architecture, string flavor, string condition)
        {
            return _pred.Include(platform, architecture, flavor, condition);
        }
    }
}
