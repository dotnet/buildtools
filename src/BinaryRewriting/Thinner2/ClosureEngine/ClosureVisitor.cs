// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using Microsoft.Tools.Transformer.CodeModel;
using System;

namespace Thinner
{
    public interface IClosureVisitor
    {
        //Queue<IReference> WorkList { get; }
        ///*HashSet*/List<IAssembly> AssembliesClosure { get; }
        //Dictionary<uint, INamedTypeDefinition> TypesClosure { get; }
        ///*HashSet*/List<ITypeDefinitionMember> MembersClosure { get; }
        //Dictionary<uint, IMethodDefinition> MethodsClosure { get; }

        void AddAssemblyReference(IAssemblyReference assembly);
        void AddMemberReference(ITypeDefinitionMember member);
        void AddTypeReference(INamedTypeReference type);
        void VisitNode(IReference node);
    }

#pragma warning disable 618
    // Warning is displayed that we should use MetadataTraverser instead of BaseMetadataTraverser, but due to breaking changes, we supressed the warning.
    public class ClosureVisitor : BaseMetadataTraverser, IClosureVisitor
#pragma warning restore 618
    {
        protected ImplementationModel m_implModel;

        public ClosureVisitor(ImplementationModel implModel)
        {
            m_implModel = implModel;
        }

        public virtual void AddAssemblyReference(IAssemblyReference assembly)
        {
            m_implModel.AddAssemblyReference(assembly);
        }

        public virtual void AddTypeReference(INamedTypeReference type)
        {
            m_implModel.AddTypeReference(type);
        }

        public virtual void AddTypeForwarder(IAliasForType alias)
        {
            m_implModel.AddTypeForwarderReference(alias);
        }

        public virtual void AddMemberReference(ITypeDefinitionMember member)
        {
            m_implModel.AddMemberReference(member);
        }

        public virtual void VisitNode(IReference node)
        {
            IAssembly assembly = node as IAssembly;
            INamedTypeDefinition type = node as INamedTypeDefinition;
            ITypeDefinitionMember member = node as ITypeDefinitionMember;
            IAliasForType alias = node as IAliasForType;

            if (assembly != null)
            {
                if (m_implModel.CanIncludeAssembly(assembly.AssemblyIdentity))
                {
                    Visit(assembly);
                }
            }
            else if (type != null)
            {
                INamedTypeReference typeRef = Util.CanonicalizeTypeReference(type);
                if (m_implModel.CanInclude(typeRef))
                {
                    Visit(type);
                }
            }
            else if (member != null)
            {
                INamedTypeReference typeRef = Util.CanonicalizeTypeReference(member.ContainingType);
                if (m_implModel.CanInclude(typeRef))
                {
                    Visit(member);
                }
            }
            else if (alias != null)
            {
                Visit(alias);
            }
            else
            {
                throw new Exception("Illegal node: " + node.GetType().Name);
            }
        }
    }
}
