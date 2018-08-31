// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Takes a delay-signed assembly and flips its CLI header "strong-name signed" bit without
    /// adding a correct signature. This creates an assembly that can be loaded in full trust
    /// without registering for verification skipping. The assembly cannot be installed to the
    /// GAC.
    /// </summary>
    public sealed class OpenSourceSign : BuildTask
    {
        /// <summary>
        /// The full path to the assembly to "Open Source Sign". The file will be modified in place.
        /// </summary>
        [Required]
        public string AssemblyPath
        {
            get;
            set;
        }

        /// <summary>
        /// Whether the task should fail in the case the file is already signed. If not, it will re-sign.
        /// By default it does not fail, because this condition can occur simply because the build was 
        /// interrupted at an inopportune moment.
        /// </summary>
        public bool FailIfAlreadySigned
        {
            get;
            set;
        }

        /// <summary>
        /// The number of bytes from the start of the <see cref="CorHeader"/> to its <see cref="CorFlags"/>.
        /// </summary>
        private const int OffsetFromStartOfCorHeaderToFlags =
           sizeof(Int32)  // byte count
         + sizeof(Int16)  // major version
         + sizeof(Int16)  // minor version
         + sizeof(Int64); // metadata directory

        public override bool Execute()
        {
            try
            {
                return ExecuteCore();
            }
            catch (IOException ex)
            {
                LogError("I/O error reading or writing PE file: {0}", ex.Message);
                return false;
            }
            catch (BadImageFormatException ex)
            {
                LogError("Invalid data encountered reading PE file: {0}.", ex.Message);
                return false;
            }
        }

        private bool ExecuteCore()
        {
            using (var stream = OpenFile(this.AssemblyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            using (var reader = new PEReader(stream))
            using (var writer = new BinaryWriter(stream))
            {
                if (!Validate(reader))
                {
                    return false;
                }

                stream.Position = reader.PEHeaders.CorHeaderStartOffset + OffsetFromStartOfCorHeaderToFlags;
                writer.Write((UInt32)(reader.PEHeaders.CorHeader.Flags | CorFlags.StrongNameSigned));
            }

            return true;
        }

        /// <summary>
        /// Returns true if the PE file meets all of the pre-conditions to be Open Source Signed.
        /// Returns false and logs msbuild errors otherwise.
        /// </summary>
        private bool Validate(PEReader peReader)
        {
            if (!peReader.HasMetadata)
            {
                LogError("PE file is not a managed module.");
                return false;
            }

            var mdReader = peReader.GetMetadataReader();
            if (!mdReader.IsAssembly)
            {
                LogError("PE file is not an assembly.");
                return false;
            }

            CorHeader header = peReader.PEHeaders.CorHeader;
            if (FailIfAlreadySigned && ((header.Flags & CorFlags.StrongNameSigned) == CorFlags.StrongNameSigned))
            {
                LogError("PE file is already strong-name signed.");
                return false;
            }

            if ((header.StrongNameSignatureDirectory.Size <= 0) || mdReader.GetAssemblyDefinition().PublicKey.IsNil)
            {
                LogError("PE file is not a delay-signed assembly.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Wraps FileStream constructor to normalize all unpreventable exceptions to IOException.
        /// </summary>
        private static FileStream OpenFile(string path, FileMode mode, FileAccess access, FileShare share)
        {
            try
            {
                return new FileStream(path, mode, access, share);
            }
            catch (ArgumentException ex)
            {
                throw new IOException(ex.Message, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new IOException(ex.Message, ex);
            }
            catch (NotSupportedException ex)
            {
                throw new IOException(ex.Message, ex);
            }
        }

        /// <summary>
        /// Logs an msbuild error with the assembly path being modified prepended.
        /// </summary>
        private void LogError(string format, params object[] args)
        {
            this.Log.LogError(this.AssemblyPath + ": " + String.Format(format, args));
        }
    }
}
