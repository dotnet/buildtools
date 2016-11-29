// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks
{
    public class ComputeDestinationsForDependencies : Task
    {
        [Required]
        public ITaskItem[] TestDependencies
        {
            get;
            set;
        }

        [Output]
        public ITaskItem[] TestDependenciesWithDestinations
        {
            get { return TestDependencies; }
        }

        public override bool Execute()
        {
            foreach (ITaskItem dependency in TestDependencies)
            {
                string relativeDestinationPath;

                bool? preserveSubDirectories = dependency.GetMetadata("PreserveSubDirectories")?.Equals("true", StringComparison.OrdinalIgnoreCase);
                if (preserveSubDirectories == true)
                {
                    string normalizedPackageRelativePath = new Uri(dependency.GetMetadata("PackageRelativePath"), UriKind.Relative).ToString();

                    // PackageRelativePath contains (PackageName\VersionNumber\Directories\FileName). This is to remove the first two directories on 
                    // the path to preserve just the directory structure.
                    int indexOfSubDirectories = normalizedPackageRelativePath.IndexOf("\\", normalizedPackageRelativePath.IndexOf("\\") + 1);
                    relativeDestinationPath = normalizedPackageRelativePath.Substring(indexOfSubDirectories);
                }
                else
                {
                    relativeDestinationPath = Path.GetFileName(dependency.ItemSpec);
                }

                dependency.SetMetadata("RelativeDestinationPath", relativeDestinationPath);
            }

            return true;
        }
    }
}
