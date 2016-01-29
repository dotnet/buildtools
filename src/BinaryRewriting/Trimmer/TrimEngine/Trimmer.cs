// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using Microsoft.Cci.MutableCodeModel;
using Microsoft.Tools.Transformer.CodeModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

// TODO: let entry point be removable?

namespace TrimBin
{
    public class Trimmer : MetadataRewriter
    {
        public Trimmer(IncludeSet includeSet, bool changeVisibility, bool applyAnnotations, bool removeDesktopSecurity, HostEnvironment hostEnvironment, bool removeSerializabilityInfo)
            : this(includeSet, changeVisibility, applyAnnotations, removeDesktopSecurity, hostEnvironment, removeSerializabilityInfo, false, false)
        { }

        public Trimmer(IncludeSet includeSet, bool changeVisibility, bool applyAnnotations, bool removeDesktopSecurity, HostEnvironment hostEnvironment, bool removeSerializabilityInfo, bool ensureConstructorsPresent)
            : this(includeSet, changeVisibility, applyAnnotations, removeDesktopSecurity, hostEnvironment, removeSerializabilityInfo, false, false)
        { }

        public Trimmer(IncludeSet includeSet, bool changeVisibility, bool applyAnnotations, bool removeDesktopSecurity, HostEnvironment hostEnvironment, bool removeSerializabilityInfo, bool ensureConstructorsPresent, bool removeManifestResources)
            : base(hostEnvironment)
        {
            _includeSet = includeSet;
            _changeVisibility = changeVisibility;
            _applyAnnotations = applyAnnotations;
            _removeDesktopSecurity = removeDesktopSecurity;
            _removeSerializabilityInfo = removeSerializabilityInfo;
            _ensureConstructorsPresent = ensureConstructorsPresent;
            _removeManifestResources = removeManifestResources;

            _trimElements = new Stack<Element>();

            _systemTypes = new List<string>{
                "<Module>",
            };  // No need to include build-related types like AssemblyRef, FXAssembly, nor ThisAssembly
        }

        protected List<INamedTypeDefinition> _allTypesList = new List<INamedTypeDefinition>();

        public bool TrimBinaries(string sourceDir, string outputDir)
        {
            bool fSuccess = true;

            foreach (TrimAssembly trimAssembly in _includeSet.GetAllAssemblies())
            {
                _currentTrimAssembly = trimAssembly;

                try
                {
                    string sourceFile = Path.Combine(sourceDir, trimAssembly.Name + ".dll");
                    string outputFile = Path.Combine(outputDir, trimAssembly.Name + ".dll");

                    Console.WriteLine("loading assembly '" + sourceFile + "'");
                    IModule module = host.LoadUnitFrom(sourceFile) as IModule;

                    if (module == null || module == Dummy.Module || module == Dummy.Assembly)
                    {
                        throw new Exception(sourceFile + " is not a PE file containing a CLR module or assembly, or an error occurred when loading it.");
                    }

                    // Working around bug 
                    DummyTraverser dummyTraverser = new DummyTraverser();

                    PdbReader pdbReader = null;
                    PdbWriter pdbWriter = null;
                    string pdbSourceFile = Path.ChangeExtension(sourceFile, "pdb");
                    string pdbOutputFile = Path.ChangeExtension(outputFile, "pdb");
                    if (File.Exists(pdbSourceFile))
                    {
                        Stream pdbStream = File.OpenRead(pdbSourceFile);
                        pdbReader = new PdbReader(pdbStream, host);
                        pdbWriter = new PdbWriter(Path.GetFullPath(pdbOutputFile), pdbReader);
                    }

                    IAssembly/*?*/ assembly = module as IAssembly;
                    if (assembly != null)
                    {
                        dummyTraverser.Visit(assembly);
                        module = this.Rewrite(assembly);
                    }
                    else
                    {
                        dummyTraverser.Visit(module);
                        module = this.Rewrite(module);
                    }
                    PeWriter.WritePeToStream(module, host, File.Create(outputFile), pdbReader, pdbReader, pdbWriter);
                }
                catch (Exception e)
                {
                    Console.WriteLine(trimAssembly.Key + ": " + e.Message);
                    throw;
                }
            }

            if (!fSuccess)
                Console.Error.WriteLine(String.Format("At least one of the assemblies could not be processed!"));

            return fSuccess;
        }

        /// <summary>
        /// Update references to the given assembly identity to reference the new identity.
        /// </summary>
        /// <param name="assemblyIdentity"></param>
        /// <param name="newAssemblyIdentity"></param>
        public void UpdateAssemblyReferences(AssemblyIdentity assemblyIdentity, AssemblyIdentity newAssemblyIdentity)
        {
            foreach (object reference in referenceRewrites)
            {
                if (reference is Microsoft.Cci.MutableCodeModel.AssemblyReference)
                {
                    var assemblyRef = reference as Microsoft.Cci.MutableCodeModel.AssemblyReference;
                    if (assemblyRef.AssemblyIdentity.Equals(assemblyIdentity))
                    {
                        // Set the reference identity. This updates related properties like name and culture as well. 
                        assemblyRef.AssemblyIdentity = newAssemblyIdentity;
                    }
                }
            }
        }

        public override List<ICustomAttribute> Rewrite(List<ICustomAttribute> customAttributes)
        {
            List<ICustomAttribute> newList = new List<ICustomAttribute>();

            if (customAttributes == null)
            {
                return newList;
            }

            foreach (ICustomAttribute attribute in customAttributes)
            {
                if ((_removeSerializabilityInfo && Util.FullyQualifiedTypeNameFromType(attribute.Type) == "System.Runtime.Serialization.OptionalFieldAttribute") ||
                    (_removeSerializabilityInfo && Util.FullyQualifiedTypeNameFromType(attribute.Type) == "System.NonSerializedAttribute") ||
                    (_removeDesktopSecurity && Util.FullyQualifiedTypeNameFromType(attribute.Type) == "System.Security.SuppressUnmanagedCodeSecurityAttribute"))
                    continue;
                newList.Add(Rewrite(attribute));
            }

            return newList;
        }

        public override List<ISecurityAttribute> Rewrite(List<ISecurityAttribute> securityAttributes)
        {
            if (!_removeDesktopSecurity)
                return securityAttributes;

            List<ISecurityAttribute> newList = new List<ISecurityAttribute>();

            if (securityAttributes == null)
            {
                return newList;
            }
            foreach (ISecurityAttribute sa in securityAttributes)
            {
                bool removeSA = false;
                if (sa.Action == SecurityAction.LinkDemand || sa.Action == SecurityAction.InheritanceDemand)
                {
                    removeSA = true;
                    foreach (ICustomAttribute attribute in sa.Attributes)
                    {
                        if (Util.GetTypeName(attribute.Type) == "System.Security.Permissions.HostProtectionAttribute")
                        {
                            removeSA = false;
                        }
                    }
                }

                if (!removeSA)
                {
                    ISecurityAttribute newSA = sa;
                    newList.Add(Rewrite(newSA));
                }
            }
            return newList;
        }

        public override void RewriteChildren(Assembly assembly)
        {
            _currentTrimAssembly = (TrimAssembly)_includeSet.Assemblies[assembly.Name.Value];
            // This should never happen.
            if (assembly.Name.Value != _currentTrimAssembly.Name)
                throw new Exception("Assembly name mismatch.");

            _trimElements.Push(_currentTrimAssembly);
            base.RewriteChildren(assembly);

            assembly.AllTypes = _allTypesList;

            _trimElements.Pop();
        }

        public override void RewriteChildren(Module module)
        {
            // Special case of calling the type <Module> since that one won't get called as part of the traversal and is required.
            // This was done before in CCI, but it's not anymore, which is why we are adding that special case here.
            if (module.AllTypes.Count > 0)
                this.Rewrite((INamespaceTypeDefinition)module.AllTypes[0]);

            base.RewriteChildren(module);
            // Cci doesn't trim any module references or user strings if they were read in from an assembly.
            // Clear them so that trimmed versions are built during PE writing.

            if (module.ModuleReferences != null)
                module.ModuleReferences.Clear();
            if (module.Strings != null)
                module.Strings.Clear();
        }

        public override void RewriteChildren(NamespaceTypeDefinition namespaceTypeDefinition)
        {
            string typeName = Util.FullyQualifiedTypeNameFromType(namespaceTypeDefinition);
            Element currentElement = null;

            // Unlike all other types that visited from either a namespace or a parent, 
            // the module type is visited from the containing module
            if (_systemTypes.Contains(typeName))
            {
                currentElement = new SpecialTrimType(typeName);
                _trimElements.Push(currentElement);
            }

            this.RewriteChildren((NamedTypeDefinition)namespaceTypeDefinition);
            //namespaceTypeDefinition.ContainingUnitNamespace = this.GetCurrentNamespace();

            if (!_systemTypes.Contains(typeName))
            {
                TypeElement type = _currentTrimAssembly.GetTypeElement(typeName);
                MutateType(namespaceTypeDefinition, type);
            }
            if (currentElement != null)
            {
                _trimElements.Pop();
            }
        }

        public override List<INamespaceMember> Rewrite(List<INamespaceMember> namespaceMembers)
        {
            List<INamespaceMember> newList = new List<INamespaceMember>();

            if (namespaceMembers == null)
            {
                return newList;
            }
            foreach (INamespaceMember member in namespaceMembers)
            {
                Element currentElement = null;

                MemberTypes memberType = Util.GetMemberTypeFromMember(member);

                if (memberType == MemberTypes.Type)
                {
                    INamedTypeDefinition typeDef = member as INamedTypeDefinition;
                    string typeName = Util.FullyQualifiedTypeNameFromType(typeDef);
                    currentElement = _currentTrimAssembly.GetTypeElement(typeName);
                    // special case for the module class. In mscorlib we don't have any global functions, etc.
                    // TODO: process its members too.
                    if (currentElement == null && IsSpecialType(typeName))
                    {
                        currentElement = new SpecialTrimType(typeName);
                        if (!_systemTypes.Contains(typeName))
                        {
                            _systemTypes.Add(typeName);
                        }
                    }
                }
                else
                {
                    currentElement = new SpecialTrimMember(null, member.Name.Value, null, memberType);
                }

                if (currentElement != null)
                {
                    _trimElements.Push(currentElement);

                    // Visit(INamespaceMember) will create the Mutable copy
                    INamespaceMember newMember = this.Rewrite(member);
                    newList.Add(newMember);

                    _trimElements.Pop();
                }
            }

            return newList;
        }

        public override List<IAliasForType> Rewrite(List<IAliasForType> aliasesForTypes)
        {
            List<IAliasForType> newList = new List<IAliasForType>();
            if (aliasesForTypes == null)
            {
                return newList;
            }
            foreach (IAliasForType alias in aliasesForTypes)
            {
                ITypeReference aliasedType = alias.AliasedType;
                String forwardedToAssemblyName = Util.GetDefiningAssembly(aliasedType).Name.Value;
                String forwardedToTypeName = Util.GetTypeName(aliasedType);
                Element currentElement = _currentTrimAssembly.GetTypeForwarderElement(forwardedToAssemblyName, forwardedToTypeName);
                if (currentElement != null)
                {
                    newList.Add(alias);
                }
            }

            return base.Rewrite(newList);
        }

        public override List<INestedTypeDefinition> Rewrite(List<INestedTypeDefinition> nestedTypeDefinitions)
        {
            List<INestedTypeDefinition> newList = new List<INestedTypeDefinition>();

            bool inParentSpecialType = CurrentTrimElement is SpecialTrimType;

            if (nestedTypeDefinitions == null)
            {
                return newList;
            }
            foreach (INestedTypeDefinition nestedType in nestedTypeDefinitions)
            {
                string typeName = Util.FullyQualifiedTypeNameFromType(nestedType);
                Element currentElement = null;

                if (inParentSpecialType)
                {
                    currentElement = new SpecialTrimType(typeName);
                }
                else
                {
                    currentElement = _currentTrimAssembly.GetTypeElement(typeName);
                }

                // special case for the module class. In mscorlib we don't have any global functions, etc.
                // TODO: process its members too.
                if (currentElement != null)
                {
                    _trimElements.Push(currentElement);

                    // Need to create the Mutable copy here
                    INestedTypeDefinition newType = Rewrite(nestedType);
                    newList.Add(newType);
                    MutateType(newType, currentElement);
                    _trimElements.Pop();
                }
            }

            return newList;
        }


        public override List<IEventDefinition> Rewrite(List<IEventDefinition> eventDefinitions)
        {
            TrimType currentType = CurrentTrimElement as TrimType;

            List<IEventDefinition> newList = new List<IEventDefinition>();

            if (eventDefinitions == null)
            {
                return newList;
            }
            foreach (IEventDefinition evnt in eventDefinitions)
            {
                MemberElement currentElement = currentType.GetMemberElementFromMember(evnt);

                if (currentElement != null)
                {
                    _trimElements.Push(currentElement);

                    IEventDefinition newEvnt = Rewrite(evnt);
                    newList.Add(newEvnt);

                    _trimElements.Pop();
                }
            }

            return newList;
        }

        public override List<IFieldDefinition> Rewrite(List<IFieldDefinition> fieldDefinitions)
        {
            TrimType currentType = CurrentTrimElement as TrimType;

            List<IFieldDefinition> newList = new List<IFieldDefinition>();

            if (fieldDefinitions == null)
            {
                return newList;
            }
            foreach (IFieldDefinition field in fieldDefinitions)
            {
                MemberElement currentElement = currentType.GetMemberElementFromMember(field);

                if (currentElement != null)
                {
                    _trimElements.Push(currentElement);

                    IFieldDefinition newField = Rewrite(field);
                    newList.Add(newField);

                    _trimElements.Pop();
                }
            }

            return newList;
        }

        public override List<IMethodDefinition> Rewrite(List<IMethodDefinition> methodDefinitions)
        {
            TrimType currentType = CurrentTrimElement as TrimType;

            List<IMethodDefinition> newList = new List<IMethodDefinition>();

            if (methodDefinitions == null)
            {
                return newList;
            }
            foreach (IMethodDefinition method in methodDefinitions)
            {
                MemberElement currentElement = currentType.GetMemberElementFromMember(method);

                if (currentElement != null)
                {
                    _trimElements.Push(currentElement);

                    IMethodDefinition newMethod = Rewrite(method);
                    newList.Add(newMethod);

                    _trimElements.Pop();
                }
            }

            return newList;
        }

        public override List<IPropertyDefinition> Rewrite(List<IPropertyDefinition> propertyDefinitions)
        {
            TrimType currentType = CurrentTrimElement as TrimType;

            List<IPropertyDefinition> newList = new List<IPropertyDefinition>();

            if (propertyDefinitions == null)
            {
                return newList;
            }
            foreach (IPropertyDefinition property in propertyDefinitions)
            {
                MemberElement currentElement = currentType.GetMemberElementFromMember(property);

                if (currentElement != null)
                {
                    _trimElements.Push(currentElement);

                    IPropertyDefinition newProperty = Rewrite(property);
                    newList.Add(newProperty);

                    _trimElements.Pop();
                }
            }

            return newList;
        }

        public override List<IMethodImplementation> Rewrite(List<IMethodImplementation> methodImplementations)
        {
            TrimType currentType = CurrentTrimElement as TrimType;

            List<IMethodImplementation> newList = new List<IMethodImplementation>();

            if (methodImplementations == null)
            {
                return newList;
            }
            foreach (IMethodImplementation methodImpl in methodImplementations)
            {
                IMethodReference implementingMethod = methodImpl.ImplementingMethod;
                IMethodReference implementedMethod = methodImpl.ImplementedMethod;
                TrimMember implementingMemberElement = currentType.GetMemberElementFromMember(implementingMethod);
                TrimMember implementedMemberElement = null;

                if (implementingMemberElement != null)
                {
                    ITypeReference implementedType = Util.CanonicalizeTypeReference(implementedMethod.ContainingType);
                    TrimType implementedTypeElement = (TrimType)_currentTrimAssembly.GetTypeElement(Util.GetTypeName(implementedType));
                    if (implementedTypeElement != null)
                        implementedMemberElement = implementedTypeElement.GetMemberElementFromMember(Util.CanonicalizeMethodReference(implementedMethod));
                }
                else
                { }

                if (implementingMemberElement != null && (implementedMemberElement != null ||
                        !_includeSet.Assemblies.ContainsKey(Util.GetDefiningAssembly(implementedMethod.ContainingType).Name.Value)))
                {
                    IMethodImplementation newMethodImpl = Rewrite(methodImpl);
                    newList.Add(newMethodImpl);
                }
                //else
                //{ Console.WriteLine("Removing {0}'s impl of {1}", implementingMethod.ToString(), implementedMethod.ToString()); }
            }

            return newList;
        }

        public override void RewriteChildren(NamedTypeDefinition typeDefinition)
        {
            // Remove interfaces
            List<ITypeReference> newList = new List<ITypeReference>();
            if (typeDefinition.Interfaces != null)
            {
                foreach (ITypeReference iface in typeDefinition.Interfaces)
                {
                    INamedTypeDefinition canonicalInterface = Util.CanonicalizeType(iface);
                    // TODO: m_currentTrimAssembly is bad if we want to trim more than one assembly in one pass.

                    // Keep implemented interfaces that are present in the include set, or defined in a different assembly.
                    TypeElement element = _currentTrimAssembly.GetTypeElement(Util.FullyQualifiedTypeNameFromType(canonicalInterface));
                    if (element != null ||
                        !_includeSet.Assemblies.ContainsKey(Util.GetDefiningAssembly(canonicalInterface).Name.Value))
                    {
                        // No need to visit iface here because base.Visit will do that
                        newList.Add(iface);
                    }
                }
            }
            typeDefinition.Interfaces = newList;

            // Remove SerializableAttribute
            if (_removeSerializabilityInfo)
            {
                typeDefinition.IsSerializable = false;
            }

            // Ensure we visit all children
            base.RewriteChildren(typeDefinition);

            // Adding this type to the flat list of types for this assembly. This is required because the MetadataRewriter doesn't update the AllTypes property of the assembly.
            if (_currentTrimAssembly.GetTypeElement(Util.GetTypeName(typeDefinition)) != null || _systemTypes.Contains(Util.GetTypeName(typeDefinition)))
                _allTypesList.Add(typeDefinition);

            if (typeDefinition.HasDeclarativeSecurity && typeDefinition.SecurityAttributes.Count == 0)
            {
                typeDefinition.HasDeclarativeSecurity = false;
            }

            // Add an empty constructor to constructor-less types
            if (_ensureConstructorsPresent)
            {
                bool hasConstructors = false;
                foreach (IMethodDefinition method in typeDefinition.Methods)
                {
                    if (method.IsConstructor && !method.IsStatic)
                    {
                        hasConstructors = true;
                    }
                }

                if (!hasConstructors && !_systemTypes.Contains(typeDefinition.Name.Value) && typeDefinition.IsClass && !typeDefinition.IsStatic)
                {
                    MethodDefinition method = new MethodDefinition();
                    method.IsSpecialName = true;
                    method.Name = this.host.NameTable.Ctor;
                    method.CallingConvention = CallingConvention.HasThis;
                    method.ContainingTypeDefinition = typeDefinition;
                    method.Parameters = new List<IParameterDefinition>();
                    MethodBody methodBody = new MethodBody();
                    method.Body = methodBody;
                    method.Type = typeDefinition.PlatformType.SystemVoid;
                    method.Visibility = TypeMemberVisibility.Private;

                    // To avoid warnings when round-tripping these through ildasm & ilasm, put 1 return instruction
                    // in the method body.  Unfortunately this doesn't seem to work.
                    List<IOperation> instrs = new List<IOperation>(1);
                    Operation o = new Operation();
                    o.OperationCode = OperationCode.Ret;
                    o.Offset = 0;
                    instrs.Add(o);
                    methodBody.Operations = instrs;
                    methodBody.MethodDefinition = method;

                    //this.path.Push(typeDefinition);
                    typeDefinition.Methods.Add(this.Rewrite(method));
                    //this.path.Pop();
                }
            }
        }

        public override void RewriteChildren(EventDefinition eventDefinition)
        {
            base.RewriteChildren(eventDefinition);
            MutateMember(eventDefinition, CurrentTrimElement);
        }

        public override void RewriteChildren(FieldDefinition fieldDefinition)
        {
            base.RewriteChildren(fieldDefinition);

            MutateMember(fieldDefinition, CurrentTrimElement);
            if (_removeSerializabilityInfo)
            {
                (fieldDefinition).IsNotSerialized = false;
            }
        }

        public override void RewriteChildren(MethodDefinition methodDefinition)
        {
            base.RewriteChildren(methodDefinition);
            MutateMember(methodDefinition, CurrentTrimElement);

            if (methodDefinition.HasDeclarativeSecurity && methodDefinition.SecurityAttributes.Count == 0)
            {
                methodDefinition.HasDeclarativeSecurity = false;
            }
        }

        public override IFieldReference Rewrite(IFieldReference fieldReference)
        {
            IFieldDefinition fieldDefinition = fieldReference as IFieldDefinition;
            if (fieldDefinition != null)
                return fieldDefinition; // If the reference already is a definition, then we don't need to traverse again.
            return base.Rewrite(fieldReference);
        }

        public override void RewriteChildren(PropertyDefinition propertyDefinition)
        {
            base.RewriteChildren(propertyDefinition);
            MutateMember(propertyDefinition, CurrentTrimElement);
        }

        private void MutateMember(TypeDefinitionMember member, Element element)
        {
            // Set transparency attributes.
            if (_applyAnnotations)
            {
                SecurityTransparencyStatus currentStatus = GetMarkedSecurityAnnotation(member.Attributes, member.ContainingTypeDefinition);
                if (element.SecurityTransparencyStatus != SecurityTransparencyStatus.Undefined && currentStatus != element.SecurityTransparencyStatus)
                {
                    RemoveSecurityTransparencyAttributes(member.Attributes, member.ContainingTypeDefinition);
                    AddSecurityTransparencyAttribute(member.Attributes, element.SecurityTransparencyStatus, member.ContainingTypeDefinition);
                }
            }

            // Add FriendAccessAllowed attribute.
            if (!(member is PropertyDefinition)) // FAA isn't needed on properties; it's only needed on getters and setters
            {
                List<ICustomAttribute> attributes = member.Attributes;
                AddFaaAttributeIfNeeded(element, attributes, member.ContainingTypeDefinition);
            }

            // Make internal if needed.
            if (_changeVisibility && element.ShouldMakeInternal && !Util.IsInternal(member.ResolvedTypeDefinitionMember.Visibility))
            {
                // Make the member internal
                MakeInternal(member);
                // Interface method implementations don't need Explicit Interface Method Implementations (EIMI's) if the implementing 
                // method is public and has the same signature as the interface method. Since we're making the method internal, 
                // we need to add the EIMI's.
                CreateExplicitInterfaceImplementations(member);
            }
        }

        private void AddFaaAttributeIfNeeded(Element element, List<ICustomAttribute> attributes, ITypeReference containingType)
        {
            if (element != null && element.IsFriendAccessAllowed)//typeHasFaaAttribute(attributes, containingType))
            {
                List<INamedTypeDefinition> types = new List<INamedTypeDefinition>(this.host.FindAssembly(this.host.CoreAssemblySymbolicIdentity).GetAllTypes());

                if (types == null || !types.Any())
                    return; //Couldn't find any type on the assembly

                IEnumerable<INamedTypeDefinition> faaAttributeType = types.Where(t => t.Name.Value == "FriendAccessAllowedAttribute");
                if (!faaAttributeType.Any())
                    return; //Unable to find the FriendAccessAllowedAttribute
                var faaAttributeResult = faaAttributeType.FirstOrDefault();
                if (!Util.HasAttribute(attributes, faaAttributeResult))
                {
                    Microsoft.Cci.MethodReference faaCtor = new Microsoft.Cci.MethodReference(this.host, faaAttributeResult,
                      CallingConvention.HasThis, this.host.PlatformType.SystemVoid, this.host.NameTable.Ctor, 0);
                    CustomAttribute faaAttribute = new CustomAttribute();
                    faaAttribute.Constructor = faaCtor;
                    attributes.Add(Rewrite(faaAttribute));
                }
            }
        }

        private void CreateExplicitInterfaceImplementations(TypeDefinitionMember member)
        {
            List<ITypeDefinitionMember> interfaceMembers = Util.FindRelatedInterfaceMembers(member);

            foreach (ITypeDefinitionMember interfaceMember in interfaceMembers)
            {
                IMethodDefinition methodDef = interfaceMember.ResolvedTypeDefinitionMember as IMethodDefinition;
                if (methodDef != null)
                {
                    List<IMethodImplementation> methodImpls = null;
                    methodImpls = GetExplicitImplementationOverrides(member, methodImpls);

                    if (methodImpls != null)
                    {
                        // Make sure implementedmethod is in the closure
                        TrimType trimType = (TrimType)_currentTrimAssembly.GetTypeElement(Util.GetTypeName(Util.ContainingTypeDefinition(interfaceMember)));
                        if (trimType != null)
                        {
                            TrimMember trimMember = trimType.GetMemberElementFromMember(interfaceMember);
                            if (trimMember != null)
                            {
                                MethodImplementation methodImpl = new MethodImplementation();
                                methodImpl.ImplementedMethod = interfaceMember.ResolvedTypeDefinitionMember as IMethodReference;
                                methodImpl.ImplementingMethod = member as IMethodReference;

                                methodImpl.ContainingType = member.ContainingTypeDefinition;
                                methodImpls.Add(Rewrite(methodImpl));
                            }
                        }
                    }
                }
            }
        }

        private void MutateType(INamedTypeDefinition iTypeDef, Element element)
        {
            NamedTypeDefinition typeDef = iTypeDef as NamedTypeDefinition;
            if (typeDef == null)
                throw new Exception("Invalid namedType definition.");
            if (_applyAnnotations)
            {
                SecurityTransparencyStatus currentStatus = GetMarkedSecurityAnnotation(typeDef.Attributes, typeDef);
                if (element.SecurityTransparencyStatus != SecurityTransparencyStatus.Undefined && element.SecurityTransparencyStatus != SecurityTransparencyStatus.Transparent && currentStatus != element.SecurityTransparencyStatus)
                {
                    RemoveSecurityTransparencyAttributes(typeDef.Attributes, typeDef);
                    AddSecurityTransparencyAttribute(typeDef.Attributes, element.SecurityTransparencyStatus, typeDef);
                }
            }

            AddFaaAttributeIfNeeded(element, typeDef.Attributes, typeDef);

            if (_changeVisibility && element.ShouldMakeInternal)
            {
                MakeInternal(typeDef);
            }
        }

        private void MakeInternal(TypeDefinitionMember member)
        {
            member.Visibility = GetInternalVisibility(member.Visibility);
        }

        private TypeMemberVisibility GetInternalVisibility(TypeMemberVisibility vis)
        {
            if (vis == TypeMemberVisibility.Public || vis == TypeMemberVisibility.FamilyOrAssembly)
                return TypeMemberVisibility.Assembly;
            else if (vis == TypeMemberVisibility.Family)
                return TypeMemberVisibility.FamilyAndAssembly;
            else { return vis; }
        }


        private void MakeInternal(NamedTypeDefinition nsType)
        {
            NestedTypeDefinition nestedTypeDef = nsType as NestedTypeDefinition;
            NamespaceTypeDefinition namespaceTypeDef = nsType as NamespaceTypeDefinition;
            if (namespaceTypeDef != null)
            {
                namespaceTypeDef.IsPublic = false;
            }
            else if (nestedTypeDef != null)
            {
                nestedTypeDef.Visibility = GetInternalVisibility(nestedTypeDef.Visibility);
            }
        }

        private bool _changeVisibility = true;
        private bool _applyAnnotations = false;
        private bool _removeDesktopSecurity = false;
        private bool _removeSerializabilityInfo = false;
        private bool _ensureConstructorsPresent = false;
        private bool _removeManifestResources = false;

        private bool IsCurrentElementFAA
        {
            get
            {
                Element element = CurrentTrimElement;
                if (element != null && element.IsFriendAccessAllowed)
                    return true;
                else
                    return false;
            }
        }

        // TODO: Find a better (traversal order-agnostic) mapping between trim elements and cci elements
        private Element CurrentTrimElement
        {
            get
            {
                //if (m_trimElements.Count == 0)
                //    return null;

                return _trimElements.Peek();
            }
        }

        private bool IsSpecialType(string typeName)
        {
            if (_systemTypes.Contains(typeName) ||
                 typeName.StartsWith("<PrivateImplementationDetails>"))
            {
                return true;
            }

            return false;
        }

        private IncludeSet _includeSet;
        private IList<string> _systemTypes;
        private Stack<Element> _trimElements;
        //// We need this because types are organized in a flat tructure instead of hierarchically in model.xml
        // so type lookups are always done at the assembly level, even for nested types.
        private TrimAssembly _currentTrimAssembly;

        private Dictionary<String, INamedTypeDefinition> _typeCache = new Dictionary<string, INamedTypeDefinition>();

        private String _securityTreatAsSafeAttributeName = "System.Security.SecurityTreatAsSafe";
        internal SecurityTransparencyStatus GetMarkedSecurityAnnotation(List<ICustomAttribute> list, ITypeReference containingType)
        {
            ITypeReference securityCriticalAttributeType = containingType.PlatformType.SystemSecuritySecurityCriticalAttribute;
            ITypeReference securitySafeCriticalAttributeType = containingType.PlatformType.SystemSecuritySecuritySafeCriticalAttribute;
            bool containsSecurityCriticalAttribute = false;
            bool containsSecuritySafeCriticalAttribute = false;
            bool containsSecurityTreatAsSafeAttribute = false;
            // TODO: Handle SecurityTreatAsSafe?
            foreach (ICustomAttribute attribute in list)
            {
                if (TypeHelper.TypesAreEquivalent(attribute.Type, securityCriticalAttributeType))
                {
                    containsSecurityCriticalAttribute = true;
                }
                else if (Util.GetTypeName(attribute.Type).Equals(_securityTreatAsSafeAttributeName))
                {
                    containsSecurityTreatAsSafeAttribute = true;
                }
                else if (TypeHelper.TypesAreEquivalent(attribute.Type, securitySafeCriticalAttributeType))
                {
                    containsSecuritySafeCriticalAttribute = true;
                }
            }

            if (containsSecuritySafeCriticalAttribute)
            {
                return SecurityTransparencyStatus.SafeCritical;
            }
            else if (containsSecurityCriticalAttribute)
            {
                if (containsSecurityTreatAsSafeAttribute) // Critical + TreatAsSafe == SafeCritical
                {
                    return SecurityTransparencyStatus.SafeCritical;
                }
                else
                {
                    return SecurityTransparencyStatus.Critical;
                }
            }
            else // No annotation
            {
                return SecurityTransparencyStatus.Transparent;
            }
        }

        internal void RemoveSecurityTransparencyAttributes(List<ICustomAttribute> list, ITypeReference containingType)
        {
            ITypeReference securityCriticalAttributeType = containingType.PlatformType.SystemSecuritySecurityCriticalAttribute;
            ITypeReference securitySafeCriticalAttributeType = containingType.PlatformType.SystemSecuritySecuritySafeCriticalAttribute;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                ICustomAttribute attribute = list[i];
                if (TypeHelper.TypesAreEquivalent(attribute.Type, securityCriticalAttributeType) || TypeHelper.TypesAreEquivalent(attribute.Type, securitySafeCriticalAttributeType) || Util.GetTypeName(attribute.Type).Equals(_securityTreatAsSafeAttributeName))
                {
                    list.RemoveAt(i);
                }
            }
        }

        internal void AddSecurityTransparencyAttribute(List<ICustomAttribute> list, SecurityTransparencyStatus securityTransparencyStatus, ITypeReference containingType)
        {
            Microsoft.Cci.MethodReference ctor = null;
            switch (securityTransparencyStatus)
            {
                case SecurityTransparencyStatus.Critical:
                    ctor = new Microsoft.Cci.MethodReference(this.host, containingType.PlatformType.SystemSecuritySecurityCriticalAttribute,//FindType("System.Security.SecurityCriticalAttribute"),
                    CallingConvention.HasThis, this.host.PlatformType.SystemVoid, this.host.NameTable.Ctor, 0);
                    break;
                case SecurityTransparencyStatus.SafeCritical:
                    ctor = new Microsoft.Cci.MethodReference(this.host, containingType.PlatformType.SystemSecuritySecuritySafeCriticalAttribute,//FindType("System.Security.SecuritySafeCriticalAttribute"),
                    CallingConvention.HasThis, this.host.PlatformType.SystemVoid, this.host.NameTable.Ctor, 0);
                    break;
                case SecurityTransparencyStatus.Transparent:
                    break;
            }

            if (ctor != null)
            {
                CustomAttribute securityAttribute = new CustomAttribute();
                securityAttribute.Constructor = ctor;
                list.Add(Rewrite(securityAttribute));
            }
        }

        private List<IMethodImplementation> GetExplicitImplementationOverrides(TypeDefinitionMember member, List<IMethodImplementation> methodImpls)
        {
            INamespaceTypeDefinition namespaceTypeDef = member.ContainingTypeDefinition as INamespaceTypeDefinition;
            INestedTypeDefinition nestedTypeDef = member.ContainingTypeDefinition as INestedTypeDefinition;
            if (namespaceTypeDef != null)
            {
                methodImpls = ((NamespaceTypeDefinition)namespaceTypeDef).ExplicitImplementationOverrides;
            }
            else if (nestedTypeDef != null)
            {
                methodImpls = ((NamespaceTypeDefinition)namespaceTypeDef).ExplicitImplementationOverrides;
            }
            else
            {
                throw new InvalidOperationException("ExplicitImplementationOverrides can only be accessed on a NamespaceTypeDefinition or a NestedTypeDefinition object");
            }
            return methodImpls;
        }

        public override List<IResourceReference> Rewrite(List<IResourceReference> resourceReferences)
        {
            if (_removeManifestResources)
            {
                if (resourceReferences.Count > 0)
                {
                    return new List<IResourceReference>();
                }
            }
            return resourceReferences;
        }
    }

#pragma warning disable 618
    // Warning is displayed that we should use MetadataTraverser instead of BaseMetadataTraverser, but due to breaking changes, we supressed the warning.
    // This class exists to work around a bug in Cci.
    internal class DummyTraverser : BaseMetadataTraverser
#pragma warning restore 618
    {
        public DummyTraverser()
            : base()
        { }

        public override void Visit(ITypeReference typeReference)
        {
            return;
        }
    }
}
