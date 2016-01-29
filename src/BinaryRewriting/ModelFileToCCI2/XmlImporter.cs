// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Tools.Transformer.CodeModel;
using System;
using System.Xml;

namespace ModelFileToCCI2
{
    public abstract class ModelReader
    {
        public void RecursivelyReadBlock(XmlReader reader, object parentObj, bool include)
        {
            while (reader.Read() && (reader.NodeType != XmlNodeType.EndElement))
            {
                XmlParseAssert(reader.IsStartElement());

                switch (reader.Name)
                {
                    case XmlKeywords.Elements.Xml:
                        {
                            XmlParseAssert(include);
                            XmlParseAssert(parentObj is ModelElement);
                            RecursivelyReadBlock(reader, parentObj, true);
                            break;
                        }
                    case XmlKeywords.Elements.ThinModel:
                        {
                            XmlParseAssert(include);
                            XmlParseAssert(parentObj is ModelElement);
                            RecursivelyReadBlock(reader, parentObj, true);
                            break;
                        }
                    case XmlKeywords.Elements.Assembly:
                        {
                            XmlParseAssert(include);
                            XmlParseAssert(parentObj is ModelElement);
                            XmlParseAssert(reader.AttributeCount > 0);
                            string assemblyName = reader.GetAttribute(XmlKeywords.Attributes.Name);
                            XmlParseAssert(assemblyName != null);

                            string includeStatusString = reader.GetAttribute(XmlKeywords.Attributes.IncludeStatus);

                            IncludeStatus includeStatus = IncludeStatus.Inherit;
                            if (includeStatusString != null)
                                includeStatus = XmlKeywords.ParseIncludeStatus(includeStatusString);

                            string platform = reader.GetAttribute(XmlKeywords.Attributes.Platform);
                            string architecture = reader.GetAttribute(XmlKeywords.Attributes.Architecture);
                            string flavor = reader.GetAttribute(XmlKeywords.Attributes.Flavor);
                            string condition = reader.GetAttribute(XmlKeywords.Attributes.Condition);
                            bool included = IncludeBuild(platform, architecture, flavor, condition);

                            AssemblyElement assembly = null;
                            if (included) assembly = CreateAssemblyElement((ModelElement)parentObj, assemblyName, includeStatus);

                            if (!reader.IsEmptyElement) RecursivelyReadBlock(reader, assembly, included);
                            break;
                        }
                    case XmlKeywords.Elements.Type:
                        {
                            TypeElement type = null;
                            bool included = false;

                            if (include)
                            {
                                XmlParseAssert(parentObj is AssemblyElement);
                                XmlParseAssert(reader.AttributeCount > 0);
                                string fullyQualifiedTypeName = reader.GetAttribute(XmlKeywords.Attributes.Name);
                                XmlParseAssert(fullyQualifiedTypeName != null);

                                AssemblyElement declaringAssembly = (AssemblyElement)parentObj;

                                IncludeStatus includeStatus = declaringAssembly.IncludeStatus;
                                string includeStatusString = reader.GetAttribute(XmlKeywords.Attributes.IncludeStatus);
                                if (includeStatusString != null)
                                    includeStatus = XmlKeywords.ParseIncludeStatus(includeStatusString);

                                SecurityTransparencyStatus securityTransparencyStatus = SecurityTransparencyStatus.Transparent;
                                string securityTransparencyStatusString = reader.GetAttribute(XmlKeywords.Attributes.SecurityTransparencyStatus);
                                if (securityTransparencyStatusString != null)
                                    securityTransparencyStatus = XmlKeywords.ParseSecurityTransparencyStatus(securityTransparencyStatusString);

                                string visibilityOverrideString = reader.GetAttribute(XmlKeywords.Attributes.VisibilityOverride);
                                VisibilityOverride visibilityOverride = XmlKeywords.ParseVisibilityOverride(visibilityOverrideString);

                                string platform = reader.GetAttribute(XmlKeywords.Attributes.Platform);
                                string architecture = reader.GetAttribute(XmlKeywords.Attributes.Architecture);
                                string flavor = reader.GetAttribute(XmlKeywords.Attributes.Flavor);
                                string condition = reader.GetAttribute(XmlKeywords.Attributes.Condition);
                                included = IncludeBuild(platform, architecture, flavor, condition);

                                if (included) type = CreateTypeElement(declaringAssembly, fullyQualifiedTypeName, includeStatus, visibilityOverride, securityTransparencyStatus);
                            }

                            if (!reader.IsEmptyElement) RecursivelyReadBlock(reader, type, included);
                            break;
                        }
                    case XmlKeywords.Elements.TypeForwarder:
                        {
                            TypeForwarderElement type = null;
                            bool included = false;

                            if (include)
                            {
                                XmlParseAssert(parentObj is AssemblyElement);
                                XmlParseAssert(reader.AttributeCount > 1);
                                string assemblyName = reader.GetAttribute(XmlKeywords.Attributes.AssemblyName);
                                string typeName = reader.GetAttribute(XmlKeywords.Attributes.TypeName);

                                XmlParseAssert(assemblyName != null);
                                XmlParseAssert(typeName != null);

                                AssemblyElement declaringAssembly = (AssemblyElement)parentObj;

                                IncludeStatus includeStatus = declaringAssembly.IncludeStatus;
                                string includeStatusString = reader.GetAttribute(XmlKeywords.Attributes.IncludeStatus);
                                if (includeStatusString != null)
                                    includeStatus = XmlKeywords.ParseIncludeStatus(includeStatusString);

                                string platform = reader.GetAttribute(XmlKeywords.Attributes.Platform);
                                string architecture = reader.GetAttribute(XmlKeywords.Attributes.Architecture);
                                string flavor = reader.GetAttribute(XmlKeywords.Attributes.Flavor);
                                string condition = reader.GetAttribute(XmlKeywords.Attributes.Condition);
                                included = IncludeBuild(platform, architecture, flavor, condition);

                                if (included) type = CreateTypeForwarderElement(declaringAssembly, assemblyName, typeName, includeStatus);
                            }

                            if (!reader.IsEmptyElement) RecursivelyReadBlock(reader, type, included);
                            break;
                        }
                    case XmlKeywords.Elements.Member:
                        {
                            if (include)
                            {
                                XmlParseAssert(parentObj is TypeElement);
                                XmlParseAssert(reader.AttributeCount > 0);
                                string memberName = reader.GetAttribute(XmlKeywords.Attributes.Name);
                                XmlParseAssert(memberName != null);

                                TypeElement declaringType = (TypeElement)parentObj;

                                string returnType = reader.GetAttribute(XmlKeywords.Attributes.ReturnType);

                                string memberTypeString = reader.GetAttribute(XmlKeywords.Attributes.MemberType);
                                MemberTypes memberType = XmlKeywords.ParseMemberType(memberTypeString);
                                XmlParseAssert(memberType != MemberTypes.Unknown);

                                IncludeStatus includeStatus = declaringType.IncludeStatus;
                                string includeStatusString = reader.GetAttribute(XmlKeywords.Attributes.IncludeStatus);
                                if (includeStatusString != null)
                                    includeStatus = XmlKeywords.ParseIncludeStatus(includeStatusString);
                                if (includeStatus == IncludeStatus.Inherit)
                                {
                                    throw new FormatException(String.Format("Specify include status for Member \"{0}\" in Type \"{1}\"", memberName, declaringType.Key));
                                }
                                XmlParseAssert(includeStatus != IncludeStatus.Inherit);

                                // Inherited virtual members "sometimes" don't inherit transparency status (when following certain rulesets) and we don't know 
                                // inheritance or rule status here. Therefore we can't say what the transparency status is here unless it's explicitly defined.
                                SecurityTransparencyStatus securityTransparencyStatus = SecurityTransparencyStatus.Undefined;
                                string securityTransparencyStatusString = reader.GetAttribute(XmlKeywords.Attributes.SecurityTransparencyStatus);
                                if (securityTransparencyStatusString != null)
                                {
                                    securityTransparencyStatus = XmlKeywords.ParseSecurityTransparencyStatus(securityTransparencyStatusString);
                                }

                                string visibilityOverrideString = reader.GetAttribute(XmlKeywords.Attributes.VisibilityOverride);
                                VisibilityOverride visibilityOverride = XmlKeywords.ParseVisibilityOverride(visibilityOverrideString);

                                string platform = reader.GetAttribute(XmlKeywords.Attributes.Platform);
                                string architecture = reader.GetAttribute(XmlKeywords.Attributes.Architecture);
                                string flavor = reader.GetAttribute(XmlKeywords.Attributes.Flavor);
                                string condition = reader.GetAttribute(XmlKeywords.Attributes.Condition);
                                bool included = IncludeBuild(platform, architecture, flavor, condition);

                                if (included)
                                    CreateMemberElement(declaringType, memberName, returnType, memberType, includeStatus, visibilityOverride, securityTransparencyStatus);
                            }

                            break;
                        }
                }
            }
        }

        private static void XmlParseAssert(bool expressionResult)
        {
            if (!expressionResult)
                throw new Exception("XML parse error");
        }

        private static void InvalidXml(string message)
        {
            throw new Exception("InvalidXml: " + message);
        }

        abstract public AssemblyElement CreateAssemblyElement(ModelElement model, string assemblyName, IncludeStatus includeStatus);
        abstract public TypeElement CreateTypeElement(AssemblyElement assembly, string typeName, IncludeStatus includeStatus, VisibilityOverride visibilityOverride, SecurityTransparencyStatus securityTransparencyStatus);
        abstract public TypeForwarderElement CreateTypeForwarderElement(AssemblyElement parent, string assemblyName, string typeName, IncludeStatus includeStatus);
        abstract public MemberElement CreateMemberElement(TypeElement type, string memberName, string returnType, MemberTypes memberType, IncludeStatus includeStatus, VisibilityOverride visibilityOverride, SecurityTransparencyStatus securityTransparencyStatus);

        abstract public bool IncludeBuild(string platform, string architecture, string flavor, string condition);
    }
}
