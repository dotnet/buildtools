// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using ModelFileToCCI2;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Tools.Transformer.CodeModel
{
    public static class Util
    {
        public static INamedTypeDefinition ResolveTypeThrowing(INamedTypeReference typeRef)
        {
            INamedTypeDefinition result = typeRef.ResolvedType;
            if (result == Dummy.Type ||
                result == Dummy.NamespaceTypeDefinition ||
                result == Dummy.NestedType)
            {
                throw new Exception(String.Format("Cannot resolve type '{0}'. Are all dependent assemblies loaded?", typeRef.ToString()));
            }
            if (result == Dummy.GenericTypeParameter)
            {
                throw new InvalidOperationException("Why is a generic parameter being resolved?");
            }

            Debug.Assert(!result.GetType().Name.Contains("Dummy"));
            return result;
        }

        public static ITypeDefinitionMember ResolveMemberThrowing(ITypeMemberReference memberRef)
        {
            ITypeDefinitionMember result = memberRef.ResolvedTypeDefinitionMember;
            if (result == Dummy.Method ||
                result == Dummy.Field ||
                result == Dummy.Event ||
                result == Dummy.Property ||
                result == null)
            {
                throw new Exception(String.Format("Cannot resolve member '{0}'. Are all dependent assemblies loaded?", memberRef.ToString()));
            }

            Debug.Assert(!result.GetType().Name.Contains("Dummy"));
            return result;
        }
        public static IMethodDefinition ResolveMethodThrowing(IMethodReference method)
        {
            IMethodDefinition result = method.ResolvedMethod;
            if (result == Dummy.Method ||
                result == null)
            {
                throw new Exception(String.Format("Cannot resolve member '{0}'. Are all dependent assemblies loaded?", method.ToString()));
            }

            Debug.Assert(!result.GetType().Name.Contains("Dummy"));
            return result;
        }

        public static bool HasAttribute(IEnumerable<ICustomAttribute> attributes, ITypeReference attributeType)
        {
            foreach (ICustomAttribute attribute in attributes)
            {
                if (TypeHelper.TypesAreEquivalent(attribute.Type, attributeType))
                {
                    return true;
                }
            }

            return false;
        }

        public static IAssembly GetDefiningAssembly(ITypeReference type)
        {
            IUnit unit = TypeHelper.GetDefiningUnit(CanonicalizeType(type));
            // TODO: Does this work?
            IModule module = unit as IModule;
            if (module != null) unit = module.ContainingAssembly;

            return unit as IAssembly;
        }

        //TODO: this doesn't work on nested types, e.g. List<Dictionary<Key, Value>>
        // Or types that begin with <> e.g. System.Threading.Tasks.Future<T>+<>c__DisplayClasse<T,U>
        public static void ParseGenName(string longName, out string shortName, out int numGenArgs)
        {
            int startIndex = 0;
            if (longName.StartsWith("<>"))
            {
                startIndex = 2;
            }
            //1. Removing signature for methods
            int sigStart = longName.IndexOf('(', startIndex);
            string name = sigStart > 0 ? longName.Substring(startIndex, sigStart - startIndex) : longName;

            //2. Parse type parameters
            numGenArgs = 0;
            int bra = name.IndexOf('<', startIndex);
            int ket = name.LastIndexOf('>');
            //Debug.Assert(ket == longName.Length-1 || ket == -1);

            if (bra > 0 && ket > bra)
            {
                ++numGenArgs;
                int start = bra;
                int comma;
                do
                {
                    comma = name.IndexOf(',', start, ket - start);
                    if (comma > 0 && comma < ket)
                    {
                        ++numGenArgs;
                        start = comma + 1;
                    }
                } while (comma > 0 && comma < ket);

                shortName = name.Substring(0, bra);
            }
            else
            {
                shortName = name;
            }
        }

        public static bool IsSystemBoolean(ITypeReference type)
        {
            return (type.TypeCode == PrimitiveTypeCode.Boolean);
        }

        public static bool IsSystemObject(ITypeReference type)
        {
            return TypeHelper.TypesAreEquivalent(type, type.PlatformType.SystemObject);
        }

        public static bool IsPropertyVirtual(IPropertyDefinition prop)
        {
            if (Util.ContainingTypeDefinition(prop).IsInterface)
                return true;

            IMethodReference getter = prop.Getter;
            if (getter != null)
                return ResolveMethodThrowing(getter).IsVirtual;
            IMethodReference setter = prop.Setter;
            if (setter != null)
                return ResolveMethodThrowing(setter).IsVirtual;

            throw new Exception(String.Format("Property {0} has no accessors. Cannot determine whether it is virtual or not", prop.ToString()));
        }

        public static bool IsEventVirtual(IEventDefinition eventDef)
        {
            if (Util.ContainingTypeDefinition(eventDef).IsInterface)
                return true;

            foreach (IMethodDefinition method in eventDef.Accessors)
            {
                return ResolveMethodThrowing(method).IsVirtual;
            }

            throw new Exception(String.Format("Event {0} has no accessors. Cannot determine whether it is virtual or not", eventDef.ToString()));
        }

        public static IPropertyDefinition GetProperty(INamedTypeDefinition type, IName propName)
        {
            foreach (ITypeDefinitionMember member in type.GetMembersNamed(propName, false))
            {
                IPropertyDefinition prop = member as IPropertyDefinition;
                if (prop != null) return prop;
            }
            return null;
        }

        public static IPropertyDefinition GetProperty(INamedTypeDefinition type, IPropertyDefinition property)
        {
            foreach (ITypeDefinitionMember member in type.GetMembersNamed(property.Name, false))
            {
                IPropertyDefinition prop = member as IPropertyDefinition;
                if (prop != null && ParametersMatch(property.Parameters, prop.Parameters))
                    return prop;
            }
            return null;
        }

        public static IMethodDefinition GetMethod(INamedTypeDefinition type, IMethodReference methodRef)
        {
            foreach (ITypeDefinitionMember member in type.GetMembersNamed(methodRef.Name, false))
            {
                IMethodDefinition methodDef = member as IMethodDefinition;
                if (methodDef != null && ParametersMatch(methodRef.Parameters, methodDef.Parameters))
                    return methodDef;
            }
            return null;
        }

        public static IFieldDefinition GetField(INamedTypeDefinition type, IName fieldName)
        {
            foreach (ITypeDefinitionMember member in type.GetMembersNamed(fieldName, false))
            {
                IFieldDefinition fieldDef = member as IFieldDefinition;
                if (fieldDef != null)
                    return fieldDef;
            }
            return null;
        }

        public static IFieldDefinition GetField(INamedTypeDefinition type, IFieldReference fieldRef)
        {
            return Util.GetField(type, fieldRef.Name);
        }

        public static IEventDefinition GetEvent(INamedTypeDefinition type, IEventDefinition evnt)
        {
            foreach (ITypeDefinitionMember member in type.GetMembersNamed(evnt.Name, false))
            {
                IEventDefinition evntDef = member as IEventDefinition;
                if (evntDef != null)
                    return evntDef;
            }
            return null;
        }

        public static uint ParameterCount(IMethodDefinition method)
        {
            uint i = 0;
            foreach (IParameterDefinition param in method.Parameters)
                ++i;

            return i;
        }

        public static ITypeDefinitionMember OwnerPropertyOrEvent(IMethodDefinition method)
        {
            // TODO: Don't use names for this. Use prop.Accessors and evnt.Accessors instead
            if (method.Name.Value.StartsWith("get_") ||
                method.Name.Value.StartsWith("set_"))
            {
                string propName = method.Name.Value.Substring(4);
                foreach (IPropertyDefinition prop in Util.ContainingTypeDefinition(method).Properties)
                {
                    if (prop.Name.Value.Equals(propName))
                    {
                        return prop;
                    }
                }
            }

            if (method.Name.Value.StartsWith("add_") ||
                method.Name.Value.StartsWith("remove_"))
            {
                string eventName = method.Name.Value.Substring(method.Name.Value.IndexOf('_') + 1);
                foreach (IEventDefinition evnt in Util.ContainingTypeDefinition(method).Events)
                {
                    if (evnt.Name.Value.Equals(eventName))
                    {
                        return evnt;
                    }
                }
            }

            return null;
        }

        // this method doesn't take into account the FrameworkInternal annotation
        public static bool IsTypeExternallyVisible(INamedTypeDefinition type)
        {
            INamespaceTypeDefinition nsType = type as INamespaceTypeDefinition;
            if (nsType != null)
                return nsType.IsPublic;

            INestedTypeDefinition nestedType = type as INestedTypeDefinition;
            if (nestedType != null)
            {
                return IsMemberExternallyVisible(nestedType);
            }

            throw new Exception("We shouldn't ask this question on anything else");
        }

        // this method doesn't take into account the FrameworkInternal annotation
        public static bool IsMemberExternallyVisible(ITypeDefinitionMember member)
        {
            INamedTypeDefinition containingTypeDef = ContainingTypeDefinition(member);
            // TODO: Review
            switch (member.Visibility)
            {
                case TypeMemberVisibility.Public:
                    return IsTypeExternallyVisible(containingTypeDef);
                case TypeMemberVisibility.Family:
                case TypeMemberVisibility.FamilyOrAssembly:
                    return IsTypeExternallyVisible(containingTypeDef) && !containingTypeDef.IsSealed;
                default:
                    return false;
            }
        }

        public static bool ParametersMatch(IEnumerable<IParameterDefinition> params1, IEnumerable<IParameterDefinition> params2)
        {
            //return TypeHelper.ParameterListsAreEquivalent(params1, params2);
            List<ITypeDefinition> lhs = new List<ITypeDefinition>();
            List<ITypeDefinition> rhs = new List<ITypeDefinition>();

            foreach (IParameterDefinition param in params1)
            {
                lhs.Add(param.Type.ResolvedType);
            }

            foreach (IParameterDefinition param in params2)
            {
                rhs.Add(param.Type.ResolvedType);
            }

            if (lhs.Count != rhs.Count)
                return false;

            for (int i = 0; i < lhs.Count; ++i)
            {
                if (!TypeHelper.TypesAreEquivalent(lhs[i], rhs[i]))
                    return false;
            }

            return true;
        }

        public static bool ParametersMatch(IEnumerable<IParameterTypeInformation> params1, IEnumerable<IParameterDefinition> params2)
        {
            //List<ITypeDefinition> lhs = new List<ITypeDefinition>();
            //List<ITypeDefinition> rhs = new List<ITypeDefinition>();
            List<ITypeReference> lhs = new List<ITypeReference>();
            List<ITypeReference> rhs = new List<ITypeReference>();

            foreach (IParameterTypeInformation param in params1)
            {
                lhs.Add(param.Type);
            }

            foreach (IParameterDefinition param in params2)
            {
                rhs.Add(param.Type);
            }

            if (lhs.Count != rhs.Count)
                return false;

            for (int i = 0; i < lhs.Count; ++i)
            {
                if (!TypeHelper.TypesAreEquivalent(CanonicalizeType(lhs[i]), CanonicalizeType(rhs[i])))
                    return false;
            }

            return true;
        }

        public static MemberTypes TypeMemberType(ITypeMemberReference member)
        {
            if (member is IMethodReference)
                return MemberTypes.Method;
            else if (member is IFieldReference)
                return MemberTypes.Field;
            else if (member is IPropertyDefinition)
                return MemberTypes.Property;
            else if (member is IEventDefinition)
                return MemberTypes.Event;
            else
                return MemberTypes.Unknown;
        }

        public static bool IsRoot(IncludeStatus status)
        {
            return ((status == IncludeStatus.ApiRoot) || (status == IncludeStatus.ImplRoot) || (status == IncludeStatus.ApiFxInternal));
        }

        public static bool IsApi(IncludeStatus status)
        {
            return ((status == IncludeStatus.ApiRoot) || (status == IncludeStatus.ApiClosure) || (status == IncludeStatus.ApiFxInternal));
        }

        public static string GetTypeName(ITypeReference type)
        {
            return (new ModelSigFormatter()).GetTypeSignature(type);
        }

        public static string FullyQualifiedTypeNameFromType(ITypeReference type)
        {
            return (new ModelSigFormatter()).GetTypeSignature(type);
        }

        public static string GetMemberSignature(ITypeMemberReference member, bool MemberTypePrefix)
        {
            return (new ModelSigFormatter()).GetMemberSignature(member, MemberTypePrefix);
        }

        // This returns null for everything other than op_Explicit and op_Implicit
        public static string GetConversionOperatorReturnType(ITypeDefinitionMember member)
        {
            IMethodDefinition method = member as IMethodDefinition;
            if (method != null && (method.Name.Value.Equals("op_Explicit") || method.Name.Value.Equals("op_Implicit")))
            {
                return Util.FullyQualifiedTypeNameFromType(method.Type);
            }

            return null;
        }

        public static bool IsDelegateType(ITypeDefinition typeDef)
        {
            foreach (ITypeReference typeRef in typeDef.BaseClasses)
            {
                // TODO: Better way of doing this that doesn't require strings even if the type is out of assembly?
                if (typeRef.ToString().Equals(typeDef.PlatformType.SystemMulticastDelegate.ToString()))
                {
                    return true;
                }
            }
            return false;
        }

        public static ITypeMemberReference CanonicalizeMemberReference(ITypeMemberReference member)
        {
            if (null == member)
                return null;

            if (member == null || member == Dummy.Method)
                throw new Exception("Can't resolve member");

            // function pointers don't have declaring types and they don't 
            // really fit our model, so we ignore them.
            if (null == member.ContainingType)
                return null;

            //
            // first canonicalize the method...
            //
            IGenericMethodInstanceReference genMeth = member as IGenericMethodInstanceReference;
            if ((genMeth != null))
            {
                member = genMeth.GenericMethod;
            }

            ISpecializedMethodReference specializedMethodRef = member as ISpecializedMethodReference;
            ISpecializedFieldReference specializedFieldRef = member as ISpecializedFieldReference;
            ISpecializedPropertyDefinition specializedPropertyDef = member as ISpecializedPropertyDefinition;
            ISpecializedEventDefinition specializedEventDef = member as ISpecializedEventDefinition;
            ISpecializedMethodDefinition specializedMethodDef = member as ISpecializedMethodDefinition;
            ISpecializedFieldDefinition specializedFieldDef = member as ISpecializedFieldDefinition;
            if (specializedMethodRef != null)
                member = specializedMethodRef.UnspecializedVersion;
            else if (specializedFieldRef != null)
                member = specializedFieldRef.UnspecializedVersion;
            else if (specializedPropertyDef != null)
                member = specializedPropertyDef.UnspecializedVersion;
            else if (specializedEventDef != null)
                member = specializedEventDef.UnspecializedVersion;
            else if (specializedMethodDef != null)
                member = specializedMethodDef.UnspecializedVersion;
            else if (specializedFieldDef != null)
                member = specializedFieldDef.UnspecializedVersion;

            if (member == null)
                throw new Exception("Can't canonicalize some member.");
            return member;
        }

        public static IMethodReference CanonicalizeMethodReference(IMethodReference method)
        {
            // function pointers don't have declaring types and they don't 
            // really fit our model, so we ignore them.
            if (null == method.ContainingType)
                return null;

            //
            // first canonicalize the method...
            //
            IGenericMethodInstanceReference genMeth = method as IGenericMethodInstanceReference;
            if ((genMeth != null))
            {
                method = genMeth.GenericMethod;
            }

            //ITypeDefinitionMember memberDef;

            ISpecializedMethodReference specializedMethodRef = method as ISpecializedMethodReference;
            if (specializedMethodRef != null)
                method = specializedMethodRef.UnspecializedVersion;
            //else
            //member = member as ITypeDefinitionMember;

            return method;
        }

        public static INamedTypeReference CanonicalizeTypeReference(ITypeReference type)
        {
            while (type != null)
            {
                IModifiedTypeReference modifiedType = type as IModifiedTypeReference;
                IPointerTypeReference ptrType = type as IPointerTypeReference;
                IManagedPointerType refType = type as IManagedPointerType;
                IArrayTypeReference arrType = type as IArrayTypeReference;
                IGenericTypeInstanceReference genType = type as IGenericTypeInstanceReference;
                ISpecializedNestedTypeReference nestedType = type as ISpecializedNestedTypeReference;
                // TODO: Why doesn't ISpecializedNestedTypeDefinition derive from ISpecializedNestedTypeReference?
                ISpecializedNestedTypeDefinition nestedTypeDef = type as ISpecializedNestedTypeDefinition;

                if (modifiedType != null)
                    type = modifiedType.UnmodifiedType;
                else if (ptrType != null)
                    type = ptrType.TargetType;
                else if (refType != null)
                    type = refType.TargetType;
                else if (arrType != null)
                    type = arrType.ElementType;
                else if (genType != null)
                    type = genType.GenericType;
                else if (nestedType != null)
                    type = nestedType.UnspecializedVersion;
                else if (nestedTypeDef != null)
                    type = nestedTypeDef.UnspecializedVersion;
                else /* ITypeDefinition */
                    break;
            }

            return type as INamedTypeReference;
        }

        public static MemberTypes GetMemberTypeFromMember(IDefinition member)
        {
            if (member is IMethodDefinition)
                return MemberTypes.Method;
            else if (member is IFieldDefinition)
                return MemberTypes.Field;
            else if (member is IPropertyDefinition)
                return MemberTypes.Property;
            else if (member is IEventDefinition)
                return MemberTypes.Event;
            else if (member is ITypeDefinition)
                return MemberTypes.Type;
            else
                return MemberTypes.Unknown;
        }

        public static MemberTypes GetMemberTypeFromMember(ITypeMemberReference member)
        {
            if (member is IMethodReference)
                return MemberTypes.Method;
            else if (member is IFieldReference)
                return MemberTypes.Field;
            else if (member is IPropertyDefinition)
                return MemberTypes.Property;
            else if (member is IEventDefinition)
                return MemberTypes.Event;
            else if (member is ITypeReference)
                return MemberTypes.Type;
            else
                return MemberTypes.Unknown;
        }

        public static string GetMemberSignature(ITypeMemberReference member)
        {
            return (new ModelSigFormatter()).GetMemberSignature(member, true);
        }

        public static string MemberKeyFromMember(ITypeMemberReference member)
        {
            return MemberNameFromMember(member);//nameBuilder.ToString();
        }

        public static string MemberNameFromMember(ITypeMemberReference member)
        {
            return GetMemberSignature(member);
        }

        public static bool HasRelatedInterfaceMembers(IMethodDefinition method)
        {
            var explicitOverrides = MemberHelper.GetExplicitlyOverriddenMethods(method);
            var implicitOverrides = MemberHelper.GetImplicitlyImplementedInterfaceMethods(method);

            if (IteratorHelper.EnumerableIsEmpty(explicitOverrides) && IteratorHelper.EnumerableIsEmpty(implicitOverrides))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public static List<ITypeDefinitionMember> FindRelatedInterfaceMembers(ITypeDefinitionMember member)
        {
            List<ITypeDefinitionMember> relatedMembers = new List<ITypeDefinitionMember>();
            Dictionary<uint, ITypeReference> participatingTypes = new Dictionary<uint, ITypeReference>();

            ITypeDefinition currentType = member.ContainingTypeDefinition;

            foreach (ITypeReference iface in currentType.Interfaces)
            {
                // check the closure against the template type, but add 
                // the specialized type to participatingTypes so that
                // argument matching works
                if (!participatingTypes.ContainsKey(iface.InternedKey))
                {
                    participatingTypes.Add(iface.InternedKey, iface);
                }
            }

            foreach (ITypeReference type in participatingTypes.Values)
            {
                ITypeDefinitionMember relatedMember = FindRelatedMember(type, member);
                if (null != relatedMember)
                {
                    relatedMembers.Add(relatedMember);
                }
            }

            return relatedMembers;
        }

        public static ITypeDefinitionMember FindRelatedMember(ITypeReference type, ITypeDefinitionMember member)
        {
            ITypeDefinition typeDef = type.ResolvedType;

            IMethodDefinition method = member as IMethodDefinition;
            IPropertyDefinition property = member as IPropertyDefinition;

            bool findProperty = (property != null);
            bool findMethod = (method != null);
            bool findEvent = (member is IEventDefinition);

            string prefix = "";
            if (typeDef.IsInterface)
            {
                prefix = Util.FullyQualifiedTypeNameFromType(type) + ".";
            }

            foreach (ITypeDefinitionMember curMember in typeDef.Members)
            {
                IMethodDefinition curMethod = curMember as IMethodDefinition;
                if (findMethod && curMethod == null)
                    continue;

                IPropertyDefinition curProperty = curMember as IPropertyDefinition;
                if (findProperty && curProperty == null)
                    continue;

                if (findEvent && !(curMember is IEventDefinition))
                    continue;

                //string currentMemberName = Util.CCI2StyleMemberNameFromMember(curMember);
                string currentMemberName = curMember.Name.Value;

                //
                // handle explicit member overrides
                //
                string memberName = member.Name.Value;
                // TODO: hack here because member.Name doesn't have spaces between generic type arguments.
                //string memberName = Util.CCI2StyleMemberNameFromMember(member);
                if (findProperty || findEvent)
                {
                    if (typeDef.IsInterface && memberName.StartsWith(prefix))
                    {
                        string simpleName = memberName.Substring(prefix.Length);
                        if (simpleName == currentMemberName)
                        {
                            return curMember;
                        }
                    }
                }

                if (memberName == currentMemberName)
                {
                    if (findEvent ||
                        findProperty ||
                        (findMethod && Util.ParametersMatch(method.Parameters, curMethod.Parameters) && method.GenericParameterCount == curMethod.GenericParameterCount))
                    {
                        return curMember;
                    }
                }
            }

            return null;
        }

        public static INamedTypeDefinition ContainingTypeDefinition(ITypeDefinitionMember member)
        {
            return CanonicalizeType(CanonicalizeMemberReference(member).ContainingType);
        }

        public static bool IsInternal(TypeMemberVisibility typeMemberVisibility)
        {
            return typeMemberVisibility == TypeMemberVisibility.FamilyAndAssembly || typeMemberVisibility == TypeMemberVisibility.Assembly;
        }

        public delegate bool TypeIncluded(INamedTypeReference type);
        public static List<ITypeDefinitionMember> FindRelatedMembers(ITypeDefinitionMember member, TypeIncluded includeType)
        {
            List<ITypeDefinitionMember> relatedMembers = new List<ITypeDefinitionMember>();
            Dictionary<uint, ITypeReference> participatingTypes = new Dictionary<uint, ITypeReference>();

            ITypeDefinition currentType = member.ContainingTypeDefinition;
            do
            {
                //
                // add the type
                //
                participatingTypes.Add(currentType.InternedKey, currentType);

                //
                // add any interfaces it implements that are part of the closure
                //
                foreach (ITypeReference iface in currentType.Interfaces)
                {
                    INamedTypeReference ifaceTemplate = Util.CanonicalizeTypeReference(iface);
                    // check the closure against the template type, but add 
                    // the specialized type to participatingTypes so that
                    // argument matching works
                    if (includeType(ifaceTemplate) &&
                        !participatingTypes.ContainsKey(iface.InternedKey))
                    {
                        // Should we add ifaceTemplate or iface?
                        participatingTypes.Add(iface.InternedKey, iface);
                    }
                }

                //
                // go up to the base type
                //
                currentType = TypeHelper.BaseClass(currentType);
            }
            while (currentType != null);

            foreach (ITypeReference type in participatingTypes.Values)
            {
                ITypeDefinitionMember relatedMember = FindRelatedMember(type, member);
                if (null != relatedMember)
                {
                    relatedMembers.Add(relatedMember);
                }
                // TODO: Review
                foreach (IMethodImplementation methodImpl in Util.CanonicalizeType(type).ExplicitImplementationOverrides)
                {
                    ITypeDefinitionMember implementingMethod = Util.CanonicalizeMember(methodImpl.ImplementingMethod);
                    ITypeDefinitionMember implementedMethod = Util.CanonicalizeMember(methodImpl.ImplementedMethod);
                    bool implementedTypeIncluded = includeType(Util.CanonicalizeType(implementedMethod.ContainingType));

                    if ((implementedMethod == member) ||
                        (implementingMethod == member && implementedTypeIncluded))
                    {
                        if (!relatedMembers.Contains(implementingMethod)) { relatedMembers.Add(implementingMethod); }
                        if (!relatedMembers.Contains(implementedMethod)) { relatedMembers.Add(implementedMethod); }
                    }
                }
            }

            return relatedMembers;
        }
        public delegate bool CanIncludeCheck(INamedTypeReference type);
        public static List<ITypeDefinitionMember> FindRelatedExternalMembers(ITypeDefinitionMember member, CanIncludeCheck canInclude)
        {
            List<ITypeDefinitionMember> relatedMembers = new List<ITypeDefinitionMember>();
            Dictionary<uint, ITypeReference> participatingTypes = new Dictionary<uint, ITypeReference>();

            ITypeDefinition currentType = member.ContainingTypeDefinition;
            do
            {
                //
                // add the type
                //
                if (!canInclude(Util.CanonicalizeTypeReference(currentType)))
                {
                    participatingTypes.Add(currentType.InternedKey, currentType);
                }

                //
                // add any interfaces it implements that are part of the closure
                //

                foreach (ITypeReference iface in currentType.Interfaces)
                {
                    INamedTypeReference ifaceTemplate = Util.CanonicalizeTypeReference(iface);
                    // check the closure against the template type, but add 
                    // the specialized type to participatingTypes so that
                    // argument matching works
                    if (!canInclude(ifaceTemplate) &&
                        !participatingTypes.ContainsKey(iface.InternedKey))
                    {
                        // Should we add ifaceTemplate or iface?
                        participatingTypes.Add(iface.InternedKey, iface);
                    }
                }

                //
                // go up to the base type
                //
                currentType = TypeHelper.BaseClass(currentType);
            }
            while (currentType != null);

            foreach (ITypeReference type in participatingTypes.Values)
            {
                ITypeDefinitionMember relatedMember = FindRelatedMember(type, member);
                if (null != relatedMember)
                {
                    relatedMembers.Add(relatedMember);
                }
            }

            return relatedMembers;
        }

        public static bool HasAttributeNamed(IEnumerable<ICustomAttribute> attributes, string attributeTypeName)
        {
            foreach (ICustomAttribute attribute in attributes)
            {
                if (GetTypeName(attribute.Type).Equals(attributeTypeName))
                {
                    return true;
                }
            }

            return false;
        }

        public static INamedTypeDefinition CanonicalizeType(ITypeReference type)
        {
            if (type == null) return null;
            return ResolveTypeThrowing(CanonicalizeTypeReference(type));
        }

        public static ITypeDefinitionMember CanonicalizeMember(ITypeMemberReference member)
        {
            return ResolveMemberThrowing(CanonicalizeMemberReference(member));
        }

        public static String GetTypeForwarderSignature(string assemblyName, string typeName)
        {
            return assemblyName + " " + typeName;
        }

        public static String GetTypeForwarderSignature(IAliasForType alias)
        {
            ITypeReference type = alias.AliasedType;
            return GetTypeForwarderSignature(Util.GetDefiningAssembly(type).Name.Value, Util.GetTypeName(type));
        }

        // Walk up the path of defining namespaces and units until we get to the assembly
        public static IAssembly GetDefiningAssembly(IAliasForType aliasForType)
        {
            INamespaceAliasForType namespaceAliasForType = GetAliasForType(aliasForType);
            if (namespaceAliasForType == null) return null;
            INamespaceDefinition containingNamespace = namespaceAliasForType.ContainingNamespace;

            IUnitNamespace unitNamespace = GetUnitNamespace(containingNamespace);
            if (unitNamespace == null) return null;

            IUnit unit = unitNamespace.Unit;

            IAssembly assembly = GetAssembly(unit);
            return assembly;
        }

        private static IAssembly GetAssembly(IUnit unit)
        {
            IAssembly assembly = unit as IAssembly;
            if (assembly == null)
            {
                IModule module = unit as IModule;
                if (module != null)
                    assembly = module.ContainingAssembly;
            }
            return assembly;
        }

        private static IUnitNamespace GetUnitNamespace(INamespaceDefinition containingNamespace)
        {
            INestedUnitNamespace nestedUnitNamespace = containingNamespace as INestedUnitNamespace;
            if (nestedUnitNamespace != null)
            {
                containingNamespace = nestedUnitNamespace.ContainingUnitNamespace;
            }
            IUnitNamespace unitNamespace = containingNamespace as IUnitNamespace;
            return unitNamespace;
        }

        private static INamespaceAliasForType GetAliasForType(IAliasForType aliasForType)
        {
            INestedAliasForType nestedAliasForType = aliasForType as INestedAliasForType;
            while (nestedAliasForType != null)
            {
                aliasForType = nestedAliasForType.ContainingAlias;
                nestedAliasForType = aliasForType as INestedAliasForType;
            }
            INamespaceAliasForType namespaceAliasForType = aliasForType as INamespaceAliasForType;
            return namespaceAliasForType;
        }
    }
}
