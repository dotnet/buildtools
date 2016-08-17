// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define read
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public partial class IsRestoreRequired : Task
    {
        [Required]
        public ITaskItem[] ProjectJsons { get; set; }

        [Required]
        public string PackagesFolder { get; set; }


        [Output]
        public bool RestoreRequired { get; set; }

        [Output]
        public ITaskItem[] ProjectJsonsRequiringRestore { get; set; }

        public override bool Execute()
        {
            var packagesChecked = new HashSet<PackageIdentity>();
            var packageResolver = new VersionFolderPathResolver(PackagesFolder);
            List<ITaskItem> needRestore = new List<ITaskItem>();
            var lockFileFormat = new LockFileFormat();
            
            ProjectJsons.AsParallel().ForAll(project =>
            {
                string projectJsonPath = project.GetMetadata("FullPath");
                string projectLockJsonPath = ProjectJsonPathUtilities.GetLockFilePath(projectJsonPath);

                if (!File.Exists(projectLockJsonPath))
                {
                    Log.LogMessage(MessageImportance.Low, $"{projectJsonPath} requires restore because {projectLockJsonPath} is missing.");
                    needRestore.Add(project);
                    return;
                }

                if (File.GetLastWriteTime(projectJsonPath) > File.GetLastWriteTime(projectLockJsonPath))
                {
                    Log.LogMessage(MessageImportance.Low, $"{projectJsonPath} requires restore because {projectLockJsonPath} is older.");
                    needRestore.Add(project);
                    return;
                }

                var lockFile = lockFileFormat.Read(projectLockJsonPath);

                //var projectName = GetProjectName(projectJsonPath);
                //var packageSpec = JsonPackageSpecReader.GetPackageSpec(projectName, projectJsonPath);

                //if (!lockFile.IsValidForPackageSpec(packageSpec, LockFileFormat.Version))
                //{
                //    Log.LogMessage(MessageImportance.Low, $"{projectJsonPath} requires restore because {projectLockJsonPath} is out of date.");
                //    needRestore.Add(project);
                //    return;
                //}

                // Verify all libraries are on disk
                var packages = lockFile.Libraries.Where(library => library.Type == LibraryType.Package);

                foreach(var library in packages)
                {
                    var identity = new PackageIdentity(library.Name, library.Version);

                    // Each id/version only needs to be checked once
                    if (AddLock(packagesChecked, identity))
                    {
                        // Verify the SHA for each package
                        var hashPath = packageResolver.GetHashPath(library.Name, library.Version);

                        if (File.Exists(hashPath))
                        {
                            var sha512 = File.ReadAllText(hashPath);

                            if (library.Sha512 != sha512)
                            {
                                Log.LogMessage(MessageImportance.Low, $"{projectJsonPath} requires restore because {library} is different.");
                                needRestore.Add(project);
                                break;
                            }
                        }
                        else
                        {
                            Log.LogMessage(MessageImportance.Low, $"{projectJsonPath} requires restore because {library} is missing.");
                            needRestore.Add(project);
                            break;
                        }
                    }
                }
            });

            ProjectJsonsRequiringRestore = needRestore.ToArray();
            RestoreRequired = ProjectJsonsRequiringRestore.Length > 0;

            return !Log.HasLoggedErrors;
        }

        private bool AddLock<T>(ISet<T> collection, T item)
        {
            lock(collection)
            {
                return collection.Add(item);
            }
        }

        private string GetProjectName(string projectJsonPath)
        {
            // match nuget behavior
            return Path.GetFileName(Path.GetDirectoryName(projectJsonPath));
        }
    }
}
