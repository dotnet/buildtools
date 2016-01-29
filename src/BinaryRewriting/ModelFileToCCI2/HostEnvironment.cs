// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Tools.Transformer.CodeModel
{
    public class HostEnvironment : MetadataReaderHost
    {
        private IEnumerable<String> _assemblyPaths;
        private IEnumerable<String> _referencedAssemblies;
        private PeReader _peReader;
        public HostEnvironment(INameTable table, IEnumerable<String> assemblyPaths, IEnumerable<String> referencedAssemblies)
            : base(table, new InternFactory(), 4, assemblyPaths, true)
        {
            _peReader = new PeReader(this);
            _assemblyPaths = assemblyPaths;
            _referencedAssemblies = referencedAssemblies;
        }
        public HostEnvironment(INameTable table)
            : this(table, new String[0], new String[0])
        {
        }

        public IAssembly FindWeaklyNamedAssembly(String assemblyName)
        {
            IName name = this.NameTable.GetNameFor(assemblyName);
            foreach (IUnit unit in this.LoadedUnits)
            {
                IAssembly assembly = unit as IAssembly;
                if (assembly != null)
                {
                    if (assembly.Name == name)
                    {
                        return assembly;
                    }
                }
            }
            return null;
        }

        public override IAssembly LoadAssembly(AssemblyIdentity assemblyIdentity)
        {
            return base.LoadAssembly(assemblyIdentity);
        }

        public override AssemblyIdentity UnifyAssembly(AssemblyIdentity assemblyIdentity)
        {
            Dictionary<String, Byte[]> assembliesToUnify = new Dictionary<string, Byte[]>{
                {"System", new Byte[]{0xb7,0x7a,0x5c,0x56,0x19,0x34,0xe0,0x89}},
                {"System.Drawing", new Byte[]{0xb0,0x3f,0x5f,0x7f,0x11,0xd5,0x0a,0x3a}},
                {"System.Windows.Forms", new Byte[]{0xb7,0x7a,0x5c,0x56,0x19,0x34,0xe0,0x89}},
            };
            Byte[] publicKeyToken;

            if (assembliesToUnify.TryGetValue(assemblyIdentity.Name.Value, out publicKeyToken) && IteratorHelper.EnumerablesAreEqual<Byte>(publicKeyToken, assemblyIdentity.PublicKeyToken))
            {
                assemblyIdentity = new AssemblyIdentity(
                    assemblyIdentity.Name,
                    assemblyIdentity.Culture,
                    CoreAssemblySymbolicIdentity.Version, // roll forward to the version of mscorlib
                    assemblyIdentity.PublicKeyToken,
                    assemblyIdentity.Location);
            }

            return base.UnifyAssembly(assemblyIdentity);
        }

        // TODO: Something similar for ResolvingModuleReference?
        public override void ResolvingAssemblyReference(IUnit referringUnit, AssemblyIdentity referencedAssembly)
        {
            List<String> paths = new List<string>();
            paths.Add(Path.GetDirectoryName(referringUnit.Location));
            paths.AddRange(_assemblyPaths);

            foreach (String assemblyFile in _referencedAssemblies)
            {
                if (Path.GetFileNameWithoutExtension(assemblyFile).Equals(referencedAssembly.Name.Value, StringComparison.OrdinalIgnoreCase))
                {
                    if (TryLoadAssembly(referringUnit, referencedAssembly, assemblyFile))
                    {
                        return;
                    }
                }
            }

            foreach (String path in paths)
            {
                String file = Path.Combine(path, referencedAssembly.Name.Value + ".dll");
                if (TryLoadAssembly(referringUnit, referencedAssembly, file))
                {
                    return;
                }

                file = Path.Combine(path, referencedAssembly.Name.Value + ".exe");
                if (TryLoadAssembly(referringUnit, referencedAssembly, file))
                {
                    return;
                }

                file = Path.Combine(path, referencedAssembly.Name.Value + ".winmd");
                if (TryLoadAssembly(referringUnit, referencedAssembly, file))
                {
                    return;
                }
            }

            throw new Exception(String.Format("Cannot find: {0}. Check assembly dependency paths.", referencedAssembly.ToString()));
        }

        private bool TryLoadAssembly(IUnit referringUnit, AssemblyIdentity referencedAssembly, String file)
        {
            bool result = false;

            if (File.Exists(file))
            {
                IUnit unit = this.LoadUnitFrom(file);
                IModule module = unit as IModule;
                if (module != null)
                {
                    IAssembly aRef = module.ContainingAssembly;
                    if (aRef != null)
                    {
                        if (aRef.AssemblyIdentity.Equals(referencedAssembly))
                        {
                            base.ResolvingAssemblyReference(referringUnit, referencedAssembly);
                            result = true;
                        }
                    }
                }
            }

            return result;
        }

        public IAssembly LoadWeaklyNamedAssembly(String assemblyName, IEnumerable<String> referencePaths)
        {
            List<String> paths = new List<string>();
            paths.AddRange(referencePaths);
            paths.AddRange(_assemblyPaths);
            foreach (String path in paths)
            {
                // TODO: .exe
                String file = Path.Combine(path, assemblyName + ".dll");

                if (File.Exists(file))
                {
                    IUnit unit = this.LoadUnitFrom(file);
                    IAssembly aRef = (unit as IModule).ContainingAssembly;
                    if (aRef.AssemblyIdentity.Name.Value.Equals(assemblyName))
                    {
                        return aRef;
                    }
                }
            }
            throw new Exception(String.Format("Cannot find assembly {0}.dll. Check assembly dependency paths.", assemblyName));
        }


        public override IUnit LoadUnitFrom(string location)
        {
            IUnit result = _peReader.OpenModule(BinaryDocument.GetBinaryDocumentForFile(location, this));
            this.RegisterAsLatest(result);
            return result;
        }

        public override IBinaryDocumentMemoryBlock OpenBinaryDocument(IBinaryDocument sourceDocument)
        {
            try
            {
                return base.OpenBinaryDocument(sourceDocument);
            }
            catch (TypeLoadException)
            {
                return ManagedBinaryMemoryBlock.CreateManagedBinaryMemoryBlock(sourceDocument.Location, sourceDocument);
            }
        }

        // Name doesn't containt the namespace, only the type name.
        private INamedTypeDefinition LoadTypeDef(string name, IScope<INamespaceMember> parent)
        {
            INamedTypeDefinition type = null;
            string firstName = null;
            string remainingName = null;

            int plusIdx = name.IndexOf('+');
            if (plusIdx > 0)
            {
                firstName = name.Substring(0, plusIdx);
                remainingName = name.Substring(plusIdx + 1);
            }
            else if (plusIdx < 0)
            {
                firstName = name;
            }
            else /* plusIdx = 0 */
            {
                throw new Exception("Invalid type def name");
            }

            int numGenArgs;
            string shortName;
            Util.ParseGenName(firstName, out shortName, out numGenArgs);

            IName firstIName = NameTable.GetNameFor(shortName);
            foreach (INamespaceMember member in parent.GetMembersNamed(firstIName, false))
            {
                type = member as INamedTypeDefinition;
                if (type != null && type.GenericParameterCount == numGenArgs)
                    break;
            }

            // HACK to handle class names like "gcroot<System::String ^>"
            // TODO: Does this result in false matches?
            if (type == null)
            {
                foreach (INamespaceMember member in parent.GetMembersNamed(NameTable.GetNameFor(firstName), false))
                {
                    type = member as INamedTypeDefinition;
                    if (type != null)
                        break;
                }
            }

            if (type != null)
            {
                if (remainingName == null)
                    return type;
                else
                {
                    return LoadNestedTypeDef(remainingName, type);
                }
            }
            else
            {
                throw new Exception("Cannot find the type: " + name);
            }
        }

        //name doesn't containt the namespace, only the type name
        private INamedTypeDefinition LoadNestedTypeDef(string name, IScope<ITypeDefinitionMember> parent)
        {
            INamedTypeDefinition type = null;
            string firstName = null;
            string remainingName = null;

            int plusIdx = name.IndexOf('+');
            if (plusIdx > 0)
            {
                firstName = name.Substring(0, plusIdx);
                remainingName = name.Substring(plusIdx + 1);
            }
            else if (plusIdx < 0)
            {
                firstName = name;
            }
            else /* plusIdx = 0 */
            {
                throw new Exception("Invalid type def name");
            }

            int numGenArgs;
            string shortName;
            Util.ParseGenName(firstName, out shortName, out numGenArgs);

            IName firstIName = NameTable.GetNameFor(shortName);
            foreach (ITypeDefinitionMember member in parent.GetMembersNamed(firstIName, false))
            {
                type = member as INamedTypeDefinition;
                if (type != null && type.GenericParameterCount == numGenArgs)
                    break;
            }

            if (type != null)
            {
                if (remainingName == null)
                    return type;
                else
                {
                    return LoadNestedTypeDef(remainingName, type);
                }
            }
            else
                throw new Exception("Cannot find the type: " + name);
        }

        private INamedTypeDefinition LoadFullyQualifiedTypeDef(string name, INamespaceDefinition parentNs)
        {
            int dotIdx = name.IndexOf('.');
            if (dotIdx > 0)
            {
                string firstNs = name.Substring(0, dotIdx);
                string remainingName = name.Substring(dotIdx + 1);
                INamespaceDefinition ns = null;

                IName firstINs = NameTable.GetNameFor(firstNs);
                foreach (INamespaceMember member in parentNs.GetMembersNamed(firstINs, false))
                {
                    ns = member as INamespaceDefinition;
                    if (ns != null) break;
                }

                if (ns != null)
                {
                    return LoadFullyQualifiedTypeDef(remainingName, ns);
                }
                else
                    throw new Exception("Cannot find the namespace: " + firstNs);
            }
            else if (dotIdx < 0)
            {
                return LoadTypeDef(name, parentNs);
            }
            else /* dotIdx == 0 */
            {
                throw new Exception("Invalid type def name");
            }
        }

        // handles nested types
        // Only resolves type definitions, i.e. no arrays, refs, ptrs, or instantiated generic types
        public INamedTypeDefinition LoadFullyQualifiedTypeDef(string fqtn, IAssembly containingAssembly)
        {
            if (fqtn.Equals("<Module>")) // Special case "<Module>" class since it isn't visible in the UnitNamespaceRoot
            {
                foreach (INamedTypeDefinition typeDef in ((IModule)containingAssembly).GetAllTypes())
                {
                    return typeDef; // The "<Module>" class is always the first value in IModule.GetAllTypes()
                }
            }
            INamespaceDefinition rootNS = containingAssembly.UnitNamespaceRoot;
            return LoadFullyQualifiedTypeDef(fqtn, rootNS);
        }

        public unsafe sealed class ManagedBinaryMemoryBlock : IBinaryDocumentMemoryBlock
        {
            private IBinaryDocument _binaryDocument;
            private IntPtr _pointer;
            private GCHandle _gcHandle;

            internal ManagedBinaryMemoryBlock(IBinaryDocument binaryDocument)
            {
                _binaryDocument = binaryDocument;
                byte[] b = new byte[(int)binaryDocument.Length];
                _gcHandle = GCHandle.Alloc(b, GCHandleType.Pinned);
                _pointer = _gcHandle.AddrOfPinnedObject();
            }

            /// <summary>
            /// Destructor for ManagedBinaryMemoryBlock
            /// </summary>
            ~ManagedBinaryMemoryBlock()
            {
                _gcHandle.Free();
                _pointer = IntPtr.Zero;
            }

            #region IBinaryDocumentMemoryBlock Members

            IBinaryDocument IBinaryDocumentMemoryBlock.BinaryDocument
            {
                get { return _binaryDocument; }
            }

            byte* IBinaryDocumentMemoryBlock.Pointer
            {
                get { return (byte*)_pointer; }
            }

            uint IBinaryDocumentMemoryBlock.Length
            {
                get { return _binaryDocument.Length; }
            }

            #endregion

            /// <summary>
            /// Factory method for opening the memory mapped file. The content of the map is assumed to come from localFileName.
            /// This can throw FileLoadException in case of error.
            /// </summary>
            /// <param name="localFileName"></param>
            /// <param name="binaryDocument"></param>
            /// <returns></returns>
            public static ManagedBinaryMemoryBlock CreateManagedBinaryMemoryBlock(
                string localFileName,
                IBinaryDocument binaryDocument
            )
            {
                using (FileStream stream = new FileStream(localFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length != binaryDocument.Length)
                        throw new IOException("File size difference: " + localFileName);
                    if (stream.Length > Int32.MaxValue)
                        throw new IOException("File too Big: " + localFileName);
                    ManagedBinaryMemoryBlock managedBinaryMemoryBlock = new ManagedBinaryMemoryBlock(binaryDocument);
                    byte* pMainBuffer = (byte*)managedBinaryMemoryBlock._pointer;

                    //Read a fixed length block at a time, so that the GC does not come under pressure from lots of large byte arrays.
                    int fileRemainingLen = (int)binaryDocument.Length;
                    int copyBufferLen = 8096;
                    byte[] tempBuffer = new byte[copyBufferLen];
                    fixed (byte* tempBufferPtr = tempBuffer)
                    {
                        while (fileRemainingLen > 0)
                        {
                            if (fileRemainingLen < copyBufferLen)
                            {
                                copyBufferLen = fileRemainingLen;
                            }
                            stream.Read(tempBuffer, 0, copyBufferLen);
                            byte* iterBuffer = tempBufferPtr;
                            byte* endBuffer = tempBufferPtr + copyBufferLen;
                            while (iterBuffer < endBuffer)
                            {
                                *pMainBuffer++ = *iterBuffer++;
                            }
                            fileRemainingLen -= copyBufferLen;
                        }
                    }
                    return managedBinaryMemoryBlock;
                }
            }
        }
    }
}
