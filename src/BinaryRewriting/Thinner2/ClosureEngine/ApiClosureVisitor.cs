// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using Microsoft.Tools.Transformer.CodeModel;
using System;
using System.Collections.Generic;

namespace Thinner
{
    public class ApiClosureVisitor : ClosureVisitor
    {
        #region Private members
        //private IApiInformationProvider m_apiInformation;

        // this method takes into account the FrameworkInternal annotation
        private bool IsTypeExternallyVisible2(INamedTypeDefinition type)
        {
            INamespaceTypeDefinition nsType = type as INamespaceTypeDefinition;
            if (nsType != null)
                return nsType.IsPublic || m_implModel.IsFrameworkInternal(type);

            INestedTypeDefinition nestedType = type as INestedTypeDefinition;
            if (nestedType != null)
            {
                return IsMemberExternallyVisible2(nestedType);
            }

            throw new Exception("We shouldn't ask this question on anything else");
        }

        // this method takes into account the FrameworkInternal annotation
        private bool IsMemberExternallyVisible2(ITypeDefinitionMember member)
        {
            return ((member.Visibility == TypeMemberVisibility.Public ||
                      member.Visibility == TypeMemberVisibility.Family ||
                      member.Visibility == TypeMemberVisibility.FamilyOrAssembly ||
                      m_implModel.IsFrameworkInternal(member)) &&
                     IsTypeExternallyVisible2(Util.ContainingTypeDefinition(member)));
        }
        #endregion

        #region Constructor
        public ApiClosureVisitor(ImplementationModel implModel)
            : base(implModel)
        {
        }
        #endregion

        #region ClosureVisitor override
        public override void AddTypeReference(INamedTypeReference type)
        {
            INamedTypeDefinition typeDef = Util.CanonicalizeType(type);

            // For enums, we want to include all their values.
            // TODO: Should this be optional?
            if (typeDef.IsEnum)
            {
                foreach (ITypeDefinitionMember member in typeDef.Members)
                {
                    AddMemberReference(member);
                }
            }

            base.AddTypeReference(type);
        }
        #endregion

        #region BaseMetadataTraverser/IMetadataVisitor override
        public override void Visit(IAssembly assembly)
        {
        }

        public override void Visit(IModule module)
        {
        }

        public override void Visit(ITypeDefinition type)
        {
            INamespaceTypeDefinition nsType = type as INamespaceTypeDefinition;
            INestedTypeDefinition nestedType = type as INestedTypeDefinition;
            if (nsType != null)
                Visit(nsType);
            else if (nestedType != null)
                Visit(nestedType);
            else
                base.Visit(type);
        }

        public override void Visit(IMarshallingInformation marshallingInformation)
        {
            // ignore all marshalling information because is is part of implementation, not the api.
        }

        public override void Visit(IEnumerable<ICustomAttribute> customAttributes)
        {
            // ignore all custom attributes because they are part of implementation, not the api.
        }

        private Dictionary<INamedTypeDefinition, object> _visited = new Dictionary<INamedTypeDefinition, object>();

        // Base case
        public override void Visit(INamespaceTypeDefinition type)
        {
            if (_visited.ContainsKey(type))
                return;
            _visited.Add(type, null);

            AddTypeReference(type);
            AddMembersWithExternalRelatives(type);

            // Recursion
            Visit(type.BaseClasses);
        }

        // Base case
        public override void Visit(INestedTypeDefinition type)
        {
            if (_visited.ContainsKey(type))
                return;
            _visited.Add(type, null);

            AddTypeReference(type);
            AddMembersWithExternalRelatives(type);

            // Recursion
            Visit(type.BaseClasses);
            Visit(type.ContainingType);
        }

        private void AddMembersWithExternalRelatives(INamedTypeDefinition type)
        {
            foreach (ITypeDefinitionMember member in type.Members)
            {
                List<ITypeDefinitionMember> members = Util.FindRelatedExternalMembers(member, m_implModel.CanInclude);

                if (members.Count != 0)
                {
                    // This member is related to an external member, so we need to add it here. 
                    AddMemberReference(member);
                    // TODO: Only add if the related member is abstract?
                }
            }
        }

        public override void Visit(INamespaceTypeReference type)
        {
            INamespaceTypeReference typeRef = (INamespaceTypeReference)Util.CanonicalizeType(type);
            // The type can resolve to an external assembly, so we need to check whether we can include the external assembly.
            if (m_implModel.CanInclude(typeRef))
            {
                Visit(Util.ResolveTypeThrowing(typeRef));
            }
        }

        public override void Visit(INestedTypeReference type)
        {
            INestedTypeReference typeRef = (INestedTypeReference)Util.CanonicalizeTypeReference(type);

            // The type can resolve to an external assembly, so we need to check whether we can include the external assembly.
            if (m_implModel.CanInclude(typeRef))
            {
                Visit(Util.ResolveTypeThrowing(typeRef));
            }
        }

        public override void Visit(ITypeDefinitionMember typeMember)
        {
            m_implModel.AddTypeReference(Util.CanonicalizeTypeReference(typeMember.ContainingType));
            base.Visit(typeMember);
        }

        public override void Visit(IMethodDefinition method)
        {
            if (IsMemberExternallyVisible2(method))
            {
                // Recursion
                Visit(method.Parameters);
                Visit(method.Type);
            }
        }

        public override void Visit(IPropertyDefinition property)
        {
            if (IsMemberExternallyVisible2(property))
            {
                // Recursion
                Visit(property.Parameters);
                Visit(property.Type);
            }
        }

        public override void Visit(IEventDefinition evnt)
        {
            if (IsMemberExternallyVisible2(evnt))
            {
                // Recursion
                Visit(evnt.Type);
            }
        }

        public override void Visit(IFieldDefinition field)
        {
            if (IsMemberExternallyVisible2(field))
            {
                // Recursion
                Visit(field.Type);
            }
        }

        public override void Visit(INestedAliasForType nestedAliasForType)
        {
            AddTypeForwarder(nestedAliasForType.ContainingAlias);
            base.Visit(nestedAliasForType);
        }
        #endregion
    }
}
