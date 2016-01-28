// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using Microsoft.Tools.Transformer.CodeModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Thinner
{
    //public interface IApiInformationProvider
    //{
    //    bool IsFrameworkInternal(INamedTypeDefinition type);
    //    bool IsFrameworkInternal(ITypeDefinitionMember member);
    //}

    public class ImplementationModel /*: IApiInformationProvider*/
    {
        //TODO: remove all direct references to AssembliesClosure, MembersClosure, MethodsClosure and TypesClosure 
        //      instead we should use public methods on ClosureDepot.
        //TODO: combine MembersClosure and MethodsClosure if we can find an universal way to compare CCI2 objects
        private class ClosureDepot
        {
            private Queue<IReference> _workList;

            private Dictionary<String, IAssembly> _assembliesClosure;
            private Dictionary<uint, INamedTypeDefinition> _typesClosure;
            private Dictionary<String, IAliasForType> _typeForwardersClosure;
            private Dictionary<ITypeDefinitionMember, object> _membersClosure;
            private Dictionary<uint, IMethodDefinition> _methodsClosure;

            public Queue<IReference> WorkList { get { return _workList; } }

            public Dictionary<String, IAssembly> AssembliesClosure { get { return _assembliesClosure; } }
            public Dictionary<uint, INamedTypeDefinition> TypesClosure { get { return _typesClosure; } }
            public Dictionary<String, IAliasForType> TypeForwardersClosure { get { return _typeForwardersClosure; } }
            public Dictionary<ITypeDefinitionMember, object> MembersClosure { get { return _membersClosure; } }
            public Dictionary<uint, IMethodDefinition> MethodsClosure { get { return _methodsClosure; } }

            public ClosureDepot()
            {
                _workList = new Queue<IReference>();

                _typeForwardersClosure = new Dictionary<String, IAliasForType>();
                _assembliesClosure = new Dictionary<String, IAssembly>();
                _typesClosure = new Dictionary<uint, INamedTypeDefinition>();
                _membersClosure = new /*HashSet*/Dictionary<ITypeDefinitionMember, object>();
                _methodsClosure = new Dictionary<uint, IMethodDefinition>();
            }

            public void AddAssemblyReference(IAssemblyReference assembly)
            {
                if (null == assembly)
                    return;

                IAssembly assemblyDef = assembly.ResolvedAssembly;
                if (!AssembliesClosure.ContainsKey(assemblyDef.Name.Value))
                {
                    AssembliesClosure.Add(assemblyDef.Name.Value, assemblyDef);
                    _workList.Enqueue(assemblyDef);
                }
            }

            public bool ContainsAssembly(IAssembly assembly)
            {
                return AssembliesClosure.ContainsValue(assembly);
            }

            public bool AddTypeReference(INamedTypeReference type)
            {
                // TODO: Optionally add all members for interfaces, and add all abstract members for abstract classes
                INamedTypeDefinition typeDef = Util.ResolveTypeThrowing(type);

                IAssembly assembly = TypeHelper.GetDefiningUnit(typeDef) as IAssembly;
                AddAssemblyReference(assembly);

                if (!TypesClosure.ContainsKey(typeDef.InternedKey))
                {
                    TypesClosure.Add(typeDef.InternedKey, typeDef);

                    _workList.Enqueue(typeDef);

                    if (Util.IsDelegateType(typeDef))
                    {
                        foreach (ITypeDefinitionMember member in typeDef.Members)
                        {
                            AddMemberReference(member);
                        }
                    }

                    return true;
                }

                return false;
            }

            public bool AddTypeForwarder(IAliasForType alias)
            {
                String signature = Util.GetTypeForwarderSignature(alias);
                if (!TypeForwardersClosure.ContainsKey(signature))
                {
                    TypeForwardersClosure.Add(signature, alias);
                    _workList.Enqueue(alias);

                    return true;
                }

                return false;
            }

            public bool ContainsType(INamedTypeDefinition typeDef)
            {
                return TypesClosure.ContainsKey(typeDef.InternedKey);
            }

            public void AddMemberReference(ITypeMemberReference member)
            {
                if (null == member)
                    return;

                ITypeDefinitionMember memberDef = Util.CanonicalizeMember(member);

                if (memberDef == Dummy.Method ||
                    memberDef == Dummy.Field ||
                    memberDef == Dummy.Property ||
                    memberDef == Dummy.Event ||
                    memberDef == Dummy.Type)
                    throw new Exception("Cannot add Dummy member");

                // CanonicalizeMember(ITypeDefinitionMember) ensures that member.ContainingType should always be non-null
                AddTypeReference(Util.ContainingTypeDefinition(memberDef));

                IMethodDefinition methodDef = memberDef as IMethodDefinition;

                if (methodDef != null)
                {
                    if (!_methodsClosure.ContainsKey(methodDef.InternedKey))
                    {
                        _methodsClosure.Add(methodDef.InternedKey, methodDef);
                        _workList.Enqueue(methodDef);
                    }
                }
                else
                {
                    // TODO: Always add accessors for events and properties?
                    IEventDefinition eventDef = member as IEventDefinition;
                    IPropertyDefinition propertyDef = member as IPropertyDefinition;
                    if (eventDef != null)
                    {
                        foreach (IMethodReference method in eventDef.Accessors)
                        {
                            AddMemberReference(method);
                        }
                    }
                    if (propertyDef != null)
                    {
                        foreach (IMethodReference method in propertyDef.Accessors)
                        {
                            AddMemberReference(method);
                        }
                    }
                    if (!_membersClosure.ContainsKey(memberDef))
                    {
                        _membersClosure.Add(memberDef, null);
                        _workList.Enqueue(memberDef);
                    }
                }
            }

            public bool ContainsMember(ITypeDefinitionMember member)
            {
                IMethodDefinition method = member as IMethodDefinition;
                if (method != null)
                    return _methodsClosure.ContainsKey(method.InternedKey);
                else
                    return _membersClosure.ContainsKey(member);
            }
        }

        private ClosureDepot _depot;

        private Dictionary<IAssembly, ThinAssembly> _rootAssemblies;
        private Dictionary<INamedTypeDefinition, ThinType> _rootTypes;
        private Dictionary<IAliasForType, ThinTypeForwarder> _rootTypeForwarders;
        private Dictionary<ITypeDefinitionMember, ThinMember> _rootMembers;

        private List<ThinMember> _missingMembers;
        private ThinModel _thinModel;

        //private HostEnvironment m_hostEnvironment;
        public ThinnerOptions Options
        {
            get { return _thinModel.Options; }
        }

        //IClosureVisitor visitor;

        private IncludeStatus _closureStatus;

        public ImplementationModel(ThinModel thinModel)
        {
            _thinModel = thinModel;
            _depot = new ClosureDepot();

            _rootAssemblies = new Dictionary<IAssembly, ThinAssembly>();
            _rootTypes = new Dictionary<INamedTypeDefinition, ThinType>();
            _rootTypeForwarders = new Dictionary<IAliasForType, ThinTypeForwarder>();
            _rootMembers = new Dictionary<ITypeDefinitionMember, ThinMember>();
            _missingMembers = new List<ThinMember>();
        }

        public virtual void AddAssemblyReference(IAssemblyReference assembly)
        {
            Debug.Assert(CanIncludeAssembly(assembly.AssemblyIdentity));
            _depot.AddAssemblyReference(assembly);
        }

        public virtual void AddTypeReference(INamedTypeReference type)
        {
            Debug.Assert(CanInclude(type));
            _depot.AddTypeReference(type);
        }

        public virtual void AddTypeForwarderReference(IAliasForType alias)
        {
            _depot.AddTypeForwarder(alias);
        }

        public virtual void AddMemberReference(ITypeDefinitionMember member)
        {
            Debug.Assert(CanInclude(Util.CanonicalizeTypeReference(member.ContainingType)));
            Debug.Assert(member == Util.CanonicalizeMember(member));
            _depot.AddMemberReference(member);

            //if (!(member is IFieldDefinition && member.Name.Value.Equals("value__")))
            //{
            //    _depot.AddMemberReference(member);
            //}
            //else
            //{
            //    // TODO: "value__" field accesses here. are those "this" pointer accesses?
            //    // For now just ignore them. They could theoretically be used to make classic static when 
            //    // none of their instance methods are used.
            //}
        }

        public virtual bool HasWorkToDo()
        {
            return (_depot.WorkList.Count > 0);
        }

        public virtual IReference Dequeue()
        {
            return _depot.WorkList.Dequeue();
        }

        public void ImportRoots(IncludeStatus rootType)
        {
            ImportWorker(rootType, AddRootAssembly, AddRootType, AddRootTypeForwarder, AddRootMember);
        }

        //@TODO: CLEAN THIS UP NOW THAT WE DON'T NEED THE DELEGATES
        private delegate void AddAssembly(ThinAssembly assembly);
        private delegate void AddType(ThinType type);
        private delegate void AddTypeForwarder(ThinTypeForwarder type);
        private delegate void AddMember(ThinMember member);

        private void AddRootAssembly(ThinAssembly assembly)
        {
            if (!_rootAssemblies.ContainsKey(assembly.Metadata))
                _rootAssemblies.Add(assembly.Metadata, assembly);
        }
        private void AddRootType(ThinType type)
        {
            if (!_rootTypes.ContainsKey(type.Metadata))
                _rootTypes.Add(type.Metadata, type);
        }
        private void AddRootTypeForwarder(ThinTypeForwarder typeForwarder)
        {
            if (!_rootTypeForwarders.ContainsKey(typeForwarder.Metadata))
                _rootTypeForwarders.Add(typeForwarder.Metadata, typeForwarder);
        }
        private void AddRootMember(ThinMember member)
        {
            if (member.Metadata == null)
            {
                if (!_missingMembers.Contains(member))
                    _missingMembers.Add(member);
            }
            else
            {
                if (!_rootMembers.ContainsKey(member.Metadata))
                    _rootMembers.Add(member.Metadata, member);
            }
        }

        private void ImportWorker(IncludeStatus statusToImport, AddAssembly addAssembly, AddType addType, AddTypeForwarder addTypeForwarder, AddMember addMember)
        {
            foreach (ThinAssembly assembly in _thinModel.Assemblies.Values)
            {
                if (assembly.IncludeStatus == statusToImport)
                    addAssembly(assembly);

                foreach (ThinTypeForwarder typeForwarder in assembly.TypeForwarders.Values)
                {
                    if (typeForwarder.IncludeStatus == statusToImport)
                    {
                        // Assembly may not have already been added because they might not have the correct IncludedStatus.
                        addAssembly(assembly);
                        addTypeForwarder(typeForwarder);
                    }
                }

                foreach (ThinType type in assembly.Types.Values)
                {
                    if (type.IncludeStatus == statusToImport)
                    {
                        // Assembly may not have already been added because they might not have the correct IncludedStatus.
                        addAssembly(assembly);
                        addType(type);
                    }

                    foreach (ThinMember member in type.Members.Values)
                    {
                        if (member.IncludeStatus == statusToImport)
                        {
                            // Assembly and Type may not have already been added because they might not have the correct IncludedStatus.
                            addAssembly(assembly);
                            addType(type);
                            addMember(member);
                        }
                    }
                }
            }
        }

        public void PrintStats()
        {
            Console.WriteLine("root assemblies:       {0}", _rootAssemblies.Count);
            Console.WriteLine("root types:            {0}", _rootTypes.Count);
            Console.WriteLine("root type forwarders:  {0}", _rootTypeForwarders.Count);
            Console.WriteLine("root members:          {0}", _rootMembers.Count);
        }

        private void Validate(IClosureVisitor visitor)
        {
            //Queue<IReference> workList = visitor.WorkList;
            //_assembliesClosure = visitor.AssembliesClosure;
            //_typesClosure = visitor.TypesClosure;
            //_membersClosure = visitor.MembersClosure;
            //_methodsClosure = visitor.MethodsClosure;

            //
            // seed the work list and closures with our roots
            //

            // NOTE: the pattern here is that whenever you add a node
            // to the work list, it must also be added to the appropriate
            // closure at the same time.  Adding to the work list will
            // cause the node to be visited, at which time, the visitor
            // is supposed to find all *other* references that that node
            // draws into the closure and adds them to the worklist and 
            // closure at that time.

            foreach (IAssembly assembly in _rootAssemblies.Keys)
            {
                AddAssemblyReference(assembly);
            }

            foreach (INamedTypeDefinition type in _rootTypes.Keys)
            {
                AddTypeReference(type);
            }

            foreach (IAliasForType typeForwarder in _rootTypeForwarders.Keys)
            {
                AddTypeForwarderReference(typeForwarder);
            }

            foreach (ITypeDefinitionMember member in _rootMembers.Keys)
            {
                AddMemberReference(member);
            }

            int loopIterations = 0;
            while (HasWorkToDo())
            {
                //Console.WriteLine("iterations: {0}, queue length: {1}", loopIterations, workList.Count);
                loopIterations++;

                ProcessWorkList(visitor);
                GenerateWorkForVirtuals(visitor);
            }

            // TODO: we should not need this if we have authored model.xml correctly.
            // e.g. we should have included MulticaseDelegate..ctor(object, string) and
            // CodeAccessSecurityAttribute..ctor(SecurityAction)
            GenerateWorkForCtors(visitor);
            while (HasWorkToDo())
            {
                //Console.WriteLine("iterations: {0}, queue length: {1}", loopIterations, workList.Count);
                loopIterations++;

                ProcessWorkList(visitor);
                GenerateWorkForCtors(visitor);
            }
        }

        private void GenerateWorkForVirtuals(IClosureVisitor visitor)
        {
            INamedTypeDefinition[] defs = new INamedTypeDefinition[_depot.TypesClosure.Values.Count];
            _depot.TypesClosure.Values.CopyTo(defs, 0);
            foreach (INamedTypeDefinition type in defs)
            {
                GenerateWorkForVirtuals(type, visitor);
            }
        }

        private void GenerateWorkForCtors(IClosureVisitor visitor)
        {
            foreach (INamedTypeDefinition type in _depot.TypesClosure.Values)
            {
                GenerateWorkForCtors(type, visitor);
            }
        }

        private void GenerateWorkForVirtuals(INamedTypeDefinition type, IClosureVisitor visitor)
        {
            // TODO: can we use INamedTypeDefinition.ExplicitImplementationOverrides instead?
            //foreach (IMethodImplementation methodImpl in type.ExplicitImplementationOverrides)
            //{
            //    ITypeReference declType = methodImpl.ImplementedMethod.ContainingType;
            //    if (_depot.TypesClosure.ContainsKey(declType.ResolvedType.InternedKey))
            //    {
            //        AddMemberReference(methodImpl);
            //    }
            //}


            // TODO: Events?
            foreach (ITypeDefinitionMember member in type.Members)
            {
                IMethodDefinition method = member as IMethodDefinition;
                IPropertyDefinition property = member as IPropertyDefinition;
                IEventDefinition eventDef = member as IEventDefinition;
                if (((method == null) || !method.IsVirtual) &&
                    ((property == null) || !Util.IsPropertyVirtual(property)) &&
                    ((eventDef == null) || !Util.IsEventVirtual(eventDef)))
                {
                    continue;
                }

                //
                // If this or any related member on a base type or interface is in the closure,
                // we must ensure that all related members are also in the closure.
                //

                bool includeRelatedMembers = false;
                List<ITypeDefinitionMember> relatedMembers = Util.FindRelatedMembers(member,
                    delegate (INamedTypeReference myType)
                    {
                        return _depot.TypesClosure.ContainsKey(myType.InternedKey) || !CanInclude(myType);
                    }
                );

                foreach (ITypeDefinitionMember m in relatedMembers)
                {
                    ITypeDefinitionMember specializedMember = Util.CanonicalizeMember(m);
                    if (_depot.ContainsMember(specializedMember) || !CanInclude(Util.CanonicalizeType(specializedMember.ContainingType)))
                        includeRelatedMembers = true;
                }

                if (includeRelatedMembers)
                {
                    foreach (ITypeDefinitionMember m in relatedMembers)
                    {
                        INamedTypeDefinition canonicalDeclaringType = Util.CanonicalizeType(m.ContainingType);

                        if (CanInclude(canonicalDeclaringType))
                        {
                            // TODO: Won't AddMemberReference add the type definition anyway?
                            // Since these members could have resolved to another assembly, check whether we can include them.
                            if (!_depot.TypesClosure.ContainsKey(canonicalDeclaringType.InternedKey))
                                Console.Error.WriteLine("ERROR: declaring type {0} of {1} not present in closure", canonicalDeclaringType, m);
                            AddMemberReference(Util.CanonicalizeMember(m));
                        }
                    }
                }
            }
        }

        private List<IMethodDefinition> GetCtors(INamedTypeDefinition type, bool includedCtorOnly)
        {
            List<IMethodDefinition> ctors = new List<IMethodDefinition>();
            foreach (IMethodDefinition meth in type.Methods)
            {
                if (meth.IsConstructor &&
                     (_depot.MethodsClosure.ContainsKey(meth.InternedKey) ||
                      !includedCtorOnly))
                {
                    ctors.Add(meth);
                }
            }
            return ctors;
        }

        //
        // Some types may have base types with no default ctors
        // and no ctors themselves.  In those cases, the compiler 
        // cannot instantiate the type without an explicit ctor
        // that calls one of the included base type ctors.
        //
        private void GenerateWorkForCtors(INamedTypeDefinition type, IClosureVisitor visitor)
        {
            if (TypeHelper.BaseClass(type) == null)
                return;

            List<IMethodDefinition> ctors = GetCtors(type, true);
            if (ctors.Count != 0)
                return;

            List<IMethodDefinition> baseCtors = GetCtors(Util.CanonicalizeType(TypeHelper.BaseClass(type)), true);

            if (baseCtors.Count == 0)
                return;

            int nDefaultCtors = 0;
            foreach (IMethodDefinition ctor in baseCtors)
            {
                if (Util.ParameterCount(ctor) == 0)
                {
                    nDefaultCtors++;
                }
            }

            if (nDefaultCtors != 0)
                return;

            // TODO: Shouldn't this be part of implclosure?
            ctors = GetCtors(type, false);
            foreach (IMethodDefinition baseCtor in baseCtors)
            {
                foreach (IMethodDefinition ctor in ctors)
                {
                    if (MethodCallsMethod(ctor, baseCtor))
                    {
                        AddMemberReference(ctor);
                        return;  // @TODO: we may need to add more than just the first one we find..
                    }
                }
            }

            // at this point, no included ctor in the base type is 
            // being called by any of the ctors in the derived type
            // so we have to get a little more creative

            if (ctors.Count > 0)
            {
                IMethodDefinition fallback = FindCalledBaseCtor(ctors[0]);
                if (null != fallback)
                {
                    AddMemberReference(ctors[0]);
                    AddMemberReference(fallback);
                }
            }
        }

        private class CalledBaseCtorFinder : MetadataTraverser
        {
            private IMethodDefinition _baseCtor;
            private INamedTypeDefinition _targetType;

            public CalledBaseCtorFinder(IMethodDefinition ctor)
            {
                _targetType = Util.CanonicalizeType(TypeHelper.BaseClass(Util.ContainingTypeDefinition(ctor)));
                _baseCtor = null;
            }

            // This is never called.
            //public override Expression VisitMethodCall(MethodCall call)
            //{
            //    InstanceInitializer ctor = ((MemberBinding)call.Callee).BoundMember as InstanceInitializer;
            //    if ((ctor != null) && (ctor.ContainingType == _targetType))
            //    {
            //        if (_baseCtor != null)
            //            throw new Exception("_baseCtor should be null here!");

            //        _baseCtor = ctor;
            //    }

            //    return base.VisitMethodCall(call);
            //}

            public IMethodDefinition BaseCtor { get { return _baseCtor; } }
        }

        public static IMethodDefinition FindCalledBaseCtor(IMethodDefinition ctor)
        {
            CalledBaseCtorFinder finder = new CalledBaseCtorFinder(ctor);
            finder.TraverseChildren(ctor);
            return finder.BaseCtor;
        }

        private class MethodCallFinder : MetadataTraverser
        {
            private bool _found;
            private IMethodDefinition _callee;

            public MethodCallFinder(IMethodDefinition callee)
            {
                _found = false;
                _callee = callee;
            }

            public bool Found { get { return _found; } }

            public override void TraverseChildren(IMethodDefinition method)
            {
                base.TraverseChildren(method.Body);
            }

            public override void TraverseChildren(IOperation operation)
            {
                IMethodReference methodReference = operation.Value as IMethodReference;
                if (methodReference != null)
                {
                    IMethodDefinition method = methodReference.ResolvedMethod;
                    if (method != null && method.InternedKey == _callee.InternedKey)
                    {
                        _found = true;
                        //return;
                    }
                }

                // TODO: Do we need this?
                // base.Visit(operation);
            }
        }

        //
        // if caller directly calls callee, return true
        //
        public static bool MethodCallsMethod(IMethodDefinition caller, IMethodDefinition callee)
        {
            MethodCallFinder finder = new MethodCallFinder(callee);
            finder.TraverseChildren(caller);
            return finder.Found;
        }

        private void ProcessWorkList(IClosureVisitor visitor)
        {
            //
            // walk the closure of each item in the list
            //
            int loopIterations = 0;
            while (HasWorkToDo())
            {
                IReference node = Dequeue();
                visitor.VisitNode(node);

                loopIterations++;
            }
        }

        public void CalculateImplementationClosure(bool isCSharp, FieldOptions fieldOptions)
        {
            ImplClosureVisitor visitor = new ImplClosureVisitor(this, new ImplClosureVisitorOptions(true, fieldOptions));
            Validate(visitor);
        }

        public void CalculateApiClosure()
        {
            ApiClosureVisitor visitor = new ApiClosureVisitor(this);
            Validate(visitor);
        }

        public void CalculateClosure(ClosureVisitor visitor)
        {
            Validate(visitor);
        }

        private IncludeStatus GetIncludeStatus(IAssembly assembly)
        {
            ThinAssembly modelAssembly;
            if (!_rootAssemblies.TryGetValue(assembly, out modelAssembly))
            {
                if (_depot.ContainsAssembly(assembly))
                    return _closureStatus;

                throw new Exception("could not find IncludeStatus for assembly " + assembly.ToString());
            }
            return modelAssembly.IncludeStatus;
        }

        private IncludeStatus GetIncludeStatus(INamedTypeDefinition type)
        {
            ThinType modelType;
            if (!_rootTypes.TryGetValue(type, out modelType))
            {
                if (_depot.TypesClosure.ContainsKey(type.InternedKey))
                {
                    // Special case ImplRoot
                    // TODO: Visitor should set status instead.
                    if (_closureStatus == IncludeStatus.ApiRoot && !Util.IsTypeExternallyVisible(type))
                    {
                        return IncludeStatus.ImplRoot;
                    }

                    return _closureStatus;
                }

                return IncludeStatus.Exclude;
            }

            return modelType.IncludeStatus;
        }

        private IncludeStatus GetIncludeStatus(ITypeDefinitionMember member)
        {
            ThinMember modelMember;
            if (!_rootMembers.TryGetValue(member, out modelMember))
            {
                if (_depot.ContainsMember(member))
                {
                    // Special case ImplRoot
                    // TODO: Visitor should set status instead.
                    if (_closureStatus == IncludeStatus.ApiRoot && !Util.IsMemberExternallyVisible(member))
                    {
                        return IncludeStatus.ImplRoot;
                    }

                    return _closureStatus;
                }

                throw new Exception("could not find IncludeStatus for member " + member.ToString());
            }

            return modelMember.IncludeStatus;
        }

        private bool IsHiddenTypeCandidate(INamedTypeDefinition type)
        {
            return !Util.IsApi(GetIncludeStatus(type)) && Util.IsTypeExternallyVisible(type);
        }

        private bool IsHiddenMemberCandidate(ITypeDefinitionMember member)
        {
            return !Util.IsApi(GetIncludeStatus(member)) && Util.IsMemberExternallyVisible(member);
        }

        private bool ShouldHideType(INamedTypeDefinition type)
        {
            return IsHiddenTypeCandidate(type);
        }

        private bool WeHidThisType(INamedTypeDefinition type)
        {
            // walk up all the declaring types to see if we hid one of them
            INamedTypeDefinition curType = type;
            while (curType != null && curType != Dummy.Type)
            {
                if (ShouldHideType(curType))
                    return true;

                INestedTypeDefinition nestedType = curType as INestedTypeDefinition;
                if (nestedType != null)
                    curType = Util.CanonicalizeType(nestedType.ContainingType);
                else
                    curType = null;
            }

            return false;
        }

        private bool TypeIsVisibleInApi(INamedTypeDefinition type)
        {
            // either we hid it or its already hidden
            // @TODO: what about private types?
            return !WeHidThisType(type) && Util.IsTypeExternallyVisible(type);
        }

        private bool ShouldHideMember(ITypeDefinitionMember member)
        {
            bool shouldHide = false;

            INamedTypeDefinition type = Util.ContainingTypeDefinition(member);
            if (IsHiddenMemberCandidate(member))
            {
                if (!TypeIsVisibleInApi(type))
                {
                    // Declaring type is hidden, only modify the visibility on a 
                    // member when its corresponding member on a public base type 
                    // was hidden.

                    INamedTypeDefinition baseType = Util.CanonicalizeType(TypeHelper.BaseClass(type));
                    while (baseType != null && baseType != Dummy.Type)
                    {
                        if (TypeIsVisibleInApi(baseType))
                        {
                            ITypeDefinitionMember relatedMember = Util.FindRelatedMember(baseType, member);
                            if (relatedMember != null)
                            {
                                ITypeDefinitionMember canonicalizedRelatedMember = Util.CanonicalizeMember(relatedMember);
                                if (_depot.ContainsMember(canonicalizedRelatedMember) &&
                                    ShouldHideMember(canonicalizedRelatedMember))
                                {
                                    shouldHide = true;
                                    break;
                                }
                            }
                        }
                        baseType = Util.CanonicalizeType(TypeHelper.BaseClass(baseType));
                    }
                }
                else
                {
                    // declaring type is public, we must hide the member.
                    shouldHide = true;
                }
            }

            return shouldHide;
        }


        // Special case: If closureStatus == ApiRoot this will automatically
        // convert it to ImplRoot for internal types
        // TODO: Visitor should set status instead of this.
        public ThinModel ExportModel(IncludeStatus closureStatus)
        {
            _closureStatus = closureStatus;

            int nApiTypes = 0;
            int nApiTypeForwarders = 0;
            int nApiMembers = 0;

            ThinModel thinModel = new ThinModel(_thinModel.Options);
            Dictionary<String, ThinAssembly> assemblies = new Dictionary<String, ThinAssembly>(_depot.AssembliesClosure.Count);
            Dictionary<INamedTypeDefinition, ThinType> types = new Dictionary<INamedTypeDefinition, ThinType>(_depot.TypesClosure.Count);

            foreach (IAssembly assembly in _depot.AssembliesClosure.Values)
            {
                ThinAssembly thinAsm = new ThinAssembly(_thinModel, assembly.Name.Value, GetIncludeStatus(assembly), assembly);
                thinModel.Assemblies.Add(thinAsm.Name, thinAsm);
                assemblies.Add(assembly.Name.Value, thinAsm);
            }

            foreach (INamedTypeDefinition type in _depot.TypesClosure.Values)
            {
                IAssembly asm = TypeHelper.GetDefiningUnit(type) as IAssembly;
                if (asm != null && assemblies.ContainsKey(asm.Name.Value))
                {
                    VisibilityOverride vis = VisibilityOverride.None;
                    if (ShouldHideType(type))
                        vis = VisibilityOverride.Internal;

                    if (closureStatus != IncludeStatus.ApiRoot)
                    {
                        if (TypeIsVisibleInApi(type))
                        {
                            INamedTypeDefinition curType = type;
                            while (curType != null && curType != Dummy.Type && // TODO: Remove dummy check?
                                CanInclude(curType))
                            {
                                if (WeHidThisType(curType))
                                    throw new Exception("API closure error!  Base type " + curType + " was hidden, but " + type + " is in the public API");
                                ITypeReference curTypeRef = TypeHelper.BaseClass(curType);
                                curType = curTypeRef != null ? Util.CanonicalizeType(curTypeRef) : null;
                            }
                        }
                    }

                    ThinAssembly declaringAssembly = assemblies[asm.Name.Value];
                    ThinType thinType = new ThinType(declaringAssembly, Util.FullyQualifiedTypeNameFromType(type), GetIncludeStatus(type), type, vis);
                    declaringAssembly.Types.Add(thinType.Name, thinType);
                    types.Add(type, thinType);

                    if (thinType.IncludeStatus == IncludeStatus.ApiClosure ||
                        thinType.IncludeStatus == IncludeStatus.ApiRoot ||
                        thinType.IncludeStatus == IncludeStatus.ApiFxInternal)
                    {
                        nApiTypes++;
                    }
                }
                else
                {
                    Console.Error.WriteLine("BclRewriter : warning BR5004 : couldn't find declaring module of type {0} in closure", type);
                }
            }

            foreach (IAliasForType typeForwarder in _depot.TypeForwardersClosure.Values)
            {
                // TODO: Why is this getting an immutable copy of the assembly?
                IAssembly asm = Util.GetDefiningAssembly(typeForwarder);
                if (asm != null && assemblies.ContainsKey(asm.Name.Value))
                {
                    ThinAssembly declaringAssembly = assemblies[asm.Name.Value];
                    ITypeReference aliasedType = typeForwarder.AliasedType;
                    ThinTypeForwarder thinTypeForwarder = new ThinTypeForwarder(declaringAssembly,
                                                                                Util.GetDefiningAssembly(aliasedType).Name.Value,
                                                                                Util.GetTypeName(aliasedType),
                                                                                GetIncludeStatus(typeForwarder),
                                                                                typeForwarder);

                    declaringAssembly.TypeForwarders.Add(thinTypeForwarder.Key, thinTypeForwarder);

                    if (thinTypeForwarder.IncludeStatus == IncludeStatus.ApiClosure ||
                        thinTypeForwarder.IncludeStatus == IncludeStatus.ApiRoot ||
                        thinTypeForwarder.IncludeStatus == IncludeStatus.ApiFxInternal)
                    {
                        nApiTypeForwarders++;
                    }
                }
                else
                {
                    Console.Error.WriteLine("BclRewriter : warning BR5001 : couldn't find declaring module of type forwarder {0} in closure", typeForwarder);
                }
            }

            foreach (ITypeDefinitionMember member in _depot.MembersClosure.Keys)
            {
                INamedTypeDefinition type = Util.ContainingTypeDefinition(member);
                if (types.ContainsKey(type))
                {
                    ThinType declaringType = types[type];
                    IncludeStatus status = GetIncludeStatus(member);

                    VisibilityOverride vis = VisibilityOverride.None;
                    if (ShouldHideMember(member))
                        vis = VisibilityOverride.Internal;

                    if ((type.IsInterface) &&
                        TypeIsVisibleInApi(type) &&
                        vis == VisibilityOverride.Internal)
                    {
                        throw new Exception(string.Format("Implementation required non-public member on public interface: {0} on {1}. This usually means you added a property to model.xml without adding the corresponding getter or setter.",
                            member.Name,
                            Util.FullyQualifiedTypeNameFromType(member.ContainingType)));
                    }

                    ThinMember thinMember = new ThinMember(declaringType, member, status, vis);
                    declaringType.Members.Add(thinMember.Key, thinMember);

                    if (thinMember.IncludeStatus == IncludeStatus.ApiClosure ||
                        thinMember.IncludeStatus == IncludeStatus.ApiRoot ||
                        thinMember.IncludeStatus == IncludeStatus.ApiFxInternal)
                    {
                        nApiMembers++;
                    }
                }
                else
                {
                    Console.Error.WriteLine("BclRewriter : warning BR5002 : couldn't find declaring type of member {0} in closure", member);
                }
            }

            foreach (IMethodDefinition method in _depot.MethodsClosure.Values)
            {
                INamedTypeDefinition type = Util.ContainingTypeDefinition(method);
                if (types.ContainsKey(type))
                {
                    ThinType declaringType = types[type];
                    IncludeStatus status = GetIncludeStatus(method);

                    VisibilityOverride vis = VisibilityOverride.None;
                    if (ShouldHideMember(method))
                        vis = VisibilityOverride.Internal;

                    if ((type.IsInterface) &&
                        TypeIsVisibleInApi(type) &&
                        vis == VisibilityOverride.Internal)
                    {
                        //throw new Exception(string.Format("WARNING: implementation required non-public member on public interface: {0} on {1}. This usually means you added a property to model.xml without adding the corresponding getter or setter.",
                        //    method.Name, 
                        //    Util.FullyQualifiedTypeNameFromType(method.ContainingType)));
                    }

                    ThinMember thinMember = new ThinMember(declaringType, method, status, vis);
                    if (declaringType.Members.ContainsKey(thinMember.Key))
                    {
                        throw new Exception(String.Format("Found two members with the same signature: {0}", thinMember.Key));
                    }
                    declaringType.Members.Add(thinMember.Key, thinMember);

                    if (thinMember.IncludeStatus == IncludeStatus.ApiClosure ||
                        thinMember.IncludeStatus == IncludeStatus.ApiRoot ||
                        thinMember.IncludeStatus == IncludeStatus.ApiFxInternal)
                    {
                        nApiMembers++;
                    }
                }
                else
                {
                    Console.Error.WriteLine("BclRewriter : warning BR5003 : couldn't find declaring type of method {0} in closure", method);
                }
            }

            foreach (ThinMember thinMember in _missingMembers)
            {
                ThinType typeToExtend = types[thinMember.DeclaringType.Metadata];
                ThinMember newThinMember = new ThinMember(typeToExtend, thinMember);
                if (!typeToExtend.Members.ContainsKey(newThinMember.Key))
                    typeToExtend.Members.Add(newThinMember.Key, newThinMember);
            }

            return thinModel;
        }

        public IncludeStatus GetIncludeStatus(IAliasForType typeForwarder)
        {
            ThinTypeForwarder modelTypeForwarder;
            if (!_rootTypeForwarders.TryGetValue(typeForwarder, out modelTypeForwarder))
            {
                if (_depot.TypeForwardersClosure.ContainsKey(Util.GetTypeForwarderSignature(typeForwarder)))
                {
                    return _closureStatus;
                }

                return IncludeStatus.Exclude;
            }

            return modelTypeForwarder.IncludeStatus;
        }

        public bool /*IApiInformationProvider.*/IsFrameworkInternal(INamedTypeDefinition type)
        {
            return _rootTypes.ContainsKey(type) && _rootTypes[type].IncludeStatus == IncludeStatus.ApiFxInternal;
        }

        public bool /*IApiInformationProvider.*/IsFrameworkInternal(ITypeDefinitionMember member)
        {
            // if member is a nested type we should be looking for it in _rootTypes
            INamedTypeDefinition type = member as INamedTypeDefinition;
            if (type != null)
                return IsFrameworkInternal(type);
            else
                return _rootMembers.ContainsKey(member) && _rootMembers[member].IncludeStatus == IncludeStatus.ApiFxInternal;
        }

        public bool CanInclude(INamedTypeReference typeRef)
        {
            return CanIncludeUnit(TypeHelper.GetDefiningUnitReference(typeRef));
        }

        public bool CanIncludeUnit(IUnitReference unit)
        {
            IAssemblyReference assembly = unit as IAssemblyReference;
            if (assembly == null)
            {
                assembly = (unit as IModuleReference).ContainingAssembly;
            }

            return CanIncludeAssembly(assembly.AssemblyIdentity);
        }

        public bool CanIncludeAssembly(AssemblyIdentity assemblyIdentity)
        {
            // If no filter set, we include all assemblies
            if (Options.IncludedAssemblies == null)
            {
                return true;
            }

            // Look through the list
            foreach (AssemblyIdentity assemblyId in Options.IncludedAssemblies)
            {
                if (assemblyId.Equals(assemblyIdentity))
                {
                    return true;
                }
            }

            // Assembly was not found in the list.
            return false;
        }
    }
}
