// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class GetPackageVersion : PackagingTask
    {
        [Required]
        public ITaskItem[] Files
        {
            get;
            set;
        }

        [Output]
        public string Version
        {
            get;
            private set;
        }

        public override bool Execute()
        {
            if (Files == null || Files.Length == 0)
            {
                Log.LogError("Files argument must be specified");
                return false;
            }

            // remove files that should be skipped or don't have a version
            var filesToConsider = Files.Where(f => !f.GetMetadata("SkipVersionCheck").Equals("true", StringComparison.OrdinalIgnoreCase) &&
                                                   !String.IsNullOrEmpty(f.GetMetadata("AssemblyVersion")))
                                       .Select(f => new
                                       {
                                           File = f.ItemSpec,
                                           TargetPath = f.GetMetadata("TargetPath"),
                                           Version = new Version(f.GetMetadata("AssemblyVersion"))
                                       });

            var refFiles = filesToConsider.Where(f => f.TargetPath.StartsWith("ref", StringComparison.OrdinalIgnoreCase));

            HashSet<Version> permittedVersions = new HashSet<System.Version>();

            if (refFiles.Any())
            {
                foreach (var refFile in refFiles)
                {
                    permittedVersions.Add(refFile.Version);
                }

                // use the version of the higest reference assembly;
                Version = refFiles.Max(f => f.Version).ToString();
            }
            else
            {
                // no reference assemblies, permit any version
                foreach (var file in filesToConsider)
                {
                    permittedVersions.Add(file.Version);
                }

                Version = permittedVersions.Max().ToString();
            }

            foreach (var file in filesToConsider)
            {
                if (!permittedVersions.Contains(file.Version))
                {
                    Log.LogError("File {0} has version {1} which is inconsistent with other libs and doesn't match any reference assembly", file.File, file.Version);
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
