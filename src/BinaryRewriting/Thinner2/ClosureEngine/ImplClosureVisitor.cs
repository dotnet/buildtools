// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using Microsoft.Tools.Transformer.CodeModel;
using System.Collections.Generic;

namespace Thinner
{
    public enum FieldOptions
    {
        Normal,
        KeepAll,
        KeepAllValueTypeFields
    }

    public struct ImplClosureVisitorOptions
    {
        public bool UseCscRules;
        public FieldOptions FieldOptions;
        public ImplClosureVisitorOptions(bool useCscRules, FieldOptions fieldOptions)
        {
            UseCscRules = useCscRules; FieldOptions = fieldOptions;
        }
    }

    public class ImplClosureVisitor : ClosureVisitor
    {
        private ImplClosureVisitorOptions _options;
        public ImplClosureVisitor(ImplementationModel implModel, ImplClosureVisitorOptions options)
            : base(implModel)
        {
            _options = options;
        }

        #region IMetadataVisitor Members

        public override void Visit(IAssembly assembly)
        {
            // Visit assembly attributes
            Visit(assembly.AssemblyAttributes);

            // Visit module attributes
            Visit(((IModule)assembly).ModuleAttributes);
        }

        public override void Visit(IPropertyDefinition property)
        {
            IMethodReference getter = property.Getter;
            IMethodReference setter = property.Setter;

            if (getter != null)
                AddMemberReference(getter.ResolvedMethod);
            if (setter != null)
                AddMemberReference(setter.ResolvedMethod);

            base.Visit(property);
        }

        public override void Visit(IEventDefinition evnt)
        {
            AddMemberReference(evnt.Adder.ResolvedMethod);
            AddMemberReference(evnt.Remover.ResolvedMethod);

            base.Visit(evnt);
        }

        public override void Visit(IMetadataConstant literal)
        {
            ITypeReference type = literal.Type;
            Visit(type);

            base.Visit(literal);
        }

        public override void Visit(IMethodDefinition method)
        {
            AddTypeReference(Util.ContainingTypeDefinition(method));

            string targetName = null;
            if (method.Name.Value.Equals("op_Equality"))
            {
                targetName = "op_Inequality";
            }
            else if (method.Name.Value.Equals("op_Inequality"))
            {
                targetName = "op_Equality";
            }

            // enque the owner property or event
            if (method.IsSpecialName)
            {
                ITypeDefinitionMember parentMember = Util.OwnerPropertyOrEvent(method);
                if (parentMember != null)
                {
                    AddMemberReference(parentMember);
                }
            }

            if (targetName != null && _options.UseCscRules /* always true in implclosure */)
            {
                IMethodDefinition opInequality = null;
                IMethodDefinition getHashCode = null;
                IMethodDefinition equals = null;

                foreach (IMethodDefinition candidateMethod in Util.ContainingTypeDefinition(method).Methods)
                {
                    if (candidateMethod.IsSpecialName)
                    {
                        if ((candidateMethod.Name.Value.Equals(targetName)))
                        {
                            if (MemberHelper.SignaturesAreEqual(method, candidateMethod))
                            {
                                opInequality = candidateMethod;
                            }
                        }
                    }
                    else
                    {
                        //TODO: why do we need to do this? Why not ToString and Finalize?
                        //We should probalby replace this with post transformation analysis
                        switch (candidateMethod.Name.Value)
                        {
                            case "GetHashCode":
                                if (Util.ParameterCount(candidateMethod) == 0)
                                {
                                    getHashCode = candidateMethod;
                                }
                                break;
                            case "Equals":
                                if (Util.ParameterCount(candidateMethod) == 1)
                                {
                                    foreach (IParameterDefinition param in candidateMethod.Parameters)
                                    {
                                        // There is only one parameter so this will be executed only once
                                        if (Util.IsSystemObject(param.Type))
                                            equals = candidateMethod;
                                    }
                                }
                                break;
                            default:
                                break;
                        }
                    }

                    if (opInequality != null && getHashCode != null && equals != null)
                        break;
                }

                if (opInequality != null)
                {
                    AddMemberReference(opInequality);
                }
                if (getHashCode != null)
                {
                    AddMemberReference(getHashCode);
                }
                if (equals != null)
                {
                    AddMemberReference(equals);
                }
            }


            //
            // the StandardVisitor class doesn't visit these!
            //
            //foreach (IOperationExceptionInformation ehClause in method.Body.OperationExceptionInformation)
            //{
            //    // ExceptionType could be dummy type. In that case we can safely ignore it.

            //    ITypeReference excepType = ehClause.ExceptionType;
            //    if (excepType != Dummy.TypeReference)
            //        AddTypeReference(ehClause.ExceptionType);
            //}

            Visit(method.Body);

            base.Visit(method);
        }

        public override void Visit(IOperationExceptionInformation operationExceptionInformation)
        {
            ITypeReference excepType = operationExceptionInformation.ExceptionType;
            if (excepType == Dummy.TypeReference)
                return;

            base.Visit(operationExceptionInformation);
        }

        public override void Visit(IFieldDefinition field)
        {
            AddTypeReference(Util.CanonicalizeTypeReference(field.ContainingType));
            base.Visit(field);
        }

        public override void Visit(INamespaceTypeReference type)
        {
            INamespaceTypeReference typeRef = (INamespaceTypeReference)Util.CanonicalizeTypeReference(type);
            // The type can resolve to an external assembly, so we need to check whether we can include the external assembly.
            if (m_implModel.CanInclude(typeRef))
            {
                Visit(Util.CanonicalizeType(typeRef));
            }
            base.Visit(type);
        }

        public override void Visit(INestedTypeReference type)
        {
            INestedTypeReference typeRef = (INestedTypeReference)Util.CanonicalizeTypeReference(type);

            // The type can resolve to an external assembly, so we need to check whether we can include the external assembly.
            if (m_implModel.CanInclude(typeRef))
            {
                Visit(Util.CanonicalizeType(type));
            }
            base.Visit(type);
        }


        public override void Visit(IGenericMethodInstanceReference method)
        {
            // TODO: check
            Visit(method.GenericArguments);
            Visit(method.GenericMethod);
        }

        public override void Visit(ITypeMemberReference member)
        {
            // The member can resolve to an external assembly, so we need to check whether we can include the external assembly.
            INamedTypeReference containingType = Util.CanonicalizeType(Util.CanonicalizeMemberReference(member).ContainingType);
            if (m_implModel.CanInclude(containingType))
            {
                AddTypeReference(containingType);
                if (member != null)
                {
                    AddMemberReference(Util.CanonicalizeMember(member));
                    return;
                }
            }
        }

        private Dictionary<uint, object> _visitedTypes = new Dictionary<uint, object>();
        public override void Visit(ITypeDefinition type)
        {
            // prevent cycles
            if (_visitedTypes.ContainsKey(type.InternedKey))
            {
                return;
            }
            _visitedTypes.Add(type.InternedKey, null);

            AddTypeReference(Util.CanonicalizeTypeReference(type));

            INestedTypeDefinition nestedType = type as INestedTypeDefinition;
            if (nestedType != null)
                Visit(nestedType.ContainingTypeDefinition);

            Visit(type.Attributes);

            if (type.HasDeclarativeSecurity)
                Visit(type.SecurityAttributes);

            Visit(type.BaseClasses);

            // Don't visit type.Interfaces here, since we might not need to include all those interfaces in the closure.
            // We will include any interfaces used elsewhere in the api/implementation

            if (type.IsGeneric)
                Visit(type.GenericParameters);

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

            // Instead of visiting all the members, let's just add the things that are implied
            foreach (IMethodDefinition meth in type.Methods)
            {
                // visit .cctor for the type
                if (meth.IsStaticConstructor)
                {
                    AddMemberReference(meth);
                }

                // @TODO: this is lame--a base type of this type may not have
                // a default constructor and we may not ever encounter code
                // that actually creates *this* type, so we may end up in a
                // situation where we cannot compile this type because we
                // don't have a ctor that satisfies the base type's ctor(s).
                // 
                // for now, just add any default .ctor and hope that's good enough
                if (meth.IsConstructor && TypeHelper.BaseClass(type) != null && Util.ParameterCount(meth) == 0)
                {
                    AddMemberReference(meth);
                }
            }

            // TODO: _options.FieldOptions == FieldOptions.KeepAll needs to be checked for api closure as well?
            if (_options.FieldOptions == FieldOptions.KeepAll ||
                (_options.FieldOptions == FieldOptions.KeepAllValueTypeFields && type.IsValueType))
            {
                foreach (IFieldDefinition field in type.Fields)
                {
                    // Include all static literal fields.
                    if (!field.IsStatic || field.IsCompileTimeConstant)
                    {
                        AddMemberReference(field);
                    }
                }
            }

            //Visit(type.TemplateArguments);
            //Visit(type.TemplateParameters);
        }
        #endregion
    }
}
