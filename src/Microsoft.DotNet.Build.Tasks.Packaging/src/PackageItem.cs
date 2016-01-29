// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using NuGet.Frameworks;
using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class PackageItem
    {
        public PackageItem(ITaskItem item)
        {
            OriginalItem = item;
            SourcePath = item.GetMetadata("FullPath");
            string fx = item.GetMetadata("TargetFramework");
            if (!String.IsNullOrWhiteSpace(fx))
            {
                TargetFramework = NuGetFramework.Parse(fx);
            }
            TargetPath = item.GetMetadata("TargetPath");
            Package = item.GetMetadata("PackageId");

            // determine if we need to append filename to TargetPath
            // see https://docs.nuget.org/create/nuspec-reference#specifying-files-to-include-in-the-package
            // SourcePath specifies file and target specifies file - do nothing
            // SourcePath specifies file and Target specifies directory - copy filename
            // SourcePath specifies wildcard files - copy wildcard
            // SourcePath specifies recursive wildcard - do not allow, recursive directory may impact asset selection
            //   we don't want to attempt to expand the wildcard since the build may not yet be complete.

            if (SourcePath.Contains("**"))
            {
                throw new ArgumentException($"Recursive wildcards \"**\" are not permitted in source paths for packages: {SourcePath}.  Recursive directory may impact asset selection and we don't want to attempt to expand the wildcard since the build may not yet be complete.");
            }

            string sourceFile = Path.GetFileName(SourcePath);
            if (!Path.GetExtension(TargetPath).Equals(Path.GetExtension(sourceFile), StringComparison.OrdinalIgnoreCase) ||
                sourceFile.Contains("*"))
            {
                TargetPath = Path.Combine(TargetPath, sourceFile);
            }

            // standardize to /
            TargetPath = TargetPath.Replace('\\', '/');

            int dirLength = TargetPath.LastIndexOf('/');
            TargetDirectory = (dirLength > 0) ? TargetPath.Substring(0, dirLength) : String.Empty;
        }

        private Version _version;
        public Version Version
        {
            get
            {
                if (_version == null)
                {
                    string versionString = OriginalItem.GetMetadata("AssemblyVersion");

                    if (!String.IsNullOrWhiteSpace(versionString))
                    {
                        Version.TryParse(versionString, out _version);
                    }

                    if (_version == null && File.Exists(SourcePath))
                    {
                        using (PEReader peReader = new PEReader(new FileStream(SourcePath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read)))
                        {
                            MetadataReader reader = peReader.GetMetadataReader();
                            _version = reader.GetAssemblyDefinition().Version;
                        }
                    }
                }

                return _version;
            }
        }

        public ITaskItem OriginalItem { get; }
        public string SourcePath { get; }
        public NuGetFramework TargetFramework { get; }
        public string TargetDirectory { get; }
        public string TargetPath { get; }
        public string Package { get; }
    }
}
