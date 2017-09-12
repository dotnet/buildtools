// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public partial class IsRestoreRequired : BuildTask
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
            var needsRestore = new LinkedList<ITaskItem>();
            var lockFileFormat = new LockFileFormat();

            ProjectJsons.AsParallel().ForAll(project =>
            {
                string projectJsonPath = project.GetMetadata("FullPath");
                string projectLockJsonPath = ProjectJsonPathUtilities.GetLockFilePath(projectJsonPath);

                if (!File.Exists(projectLockJsonPath))
                {
                    Log.LogMessage(MessageImportance.Low, $"{projectJsonPath} requires restore because {projectLockJsonPath} is missing.");
                    AddLock(needsRestore, project);
                    return;
                }

                if (File.GetLastWriteTime(projectJsonPath) > File.GetLastWriteTime(projectLockJsonPath))
                {
                    Log.LogMessage(MessageImportance.Low, $"{projectJsonPath} requires restore because {projectLockJsonPath} is older.");
                    AddLock(needsRestore, project);
                    return;
                }

                var packages = ReadPackages(projectLockJsonPath);

                foreach (var package in packages)
                {
                    // Each id/version only needs to be checked once
                    if (AddLock(packagesChecked, package))
                    {
                        // Verify the SHA exists for each package, don't validate the content since we assume our packages are immutable
                        var hashPath = packageResolver.GetHashPath(package.Id, package.Version);

                        if (!File.Exists(hashPath))
                        {
                            Log.LogMessage(MessageImportance.Low, $"{projectJsonPath} requires restore because {package} is missing.");
                            AddLock(needsRestore, project);
                            break;
                        }
                    }
                }
            });

            ProjectJsonsRequiringRestore = needsRestore.ToArray();
            RestoreRequired = ProjectJsonsRequiringRestore.Length > 0;

            return !Log.HasLoggedErrors;
        }

        private bool AddLock<T>(ISet<T> set, T item)
        {
            lock (set)
            {
                return set.Add(item);
            }
        }
        private void AddLock<T>(ICollection<T> collection, T item)
        {
            lock (collection)
            {
                collection.Add(item);
            }
        }

        private string GetProjectName(string projectJsonPath)
        {
            // match nuget behavior
            return Path.GetFileName(Path.GetDirectoryName(projectJsonPath));
        }

        // Lightweight lock file parser that only reads the package identities
        private static IEnumerable<PackageIdentity> ReadPackages(string lockFilePath)
        {
            using (var stream = File.OpenRead(lockFilePath))
            using (var textReader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                while (jsonReader.TokenType != JsonToken.StartObject)
                {
                    EnsureRead(jsonReader);
                }

                ReadToValue(jsonReader, JsonToken.PropertyName, "libraries");
                EnsureRead(jsonReader); // StartObject
                EnsureRead(jsonReader); // Property name

                while (jsonReader.TokenType != JsonToken.EndObject)
                {
                    var packageString = jsonReader.Value as string;

                    if (packageString == null)
                    {
                        throw new InvalidDataException($"Unexpected entry in {lockFilePath} at line {jsonReader.LineNumber}, position {jsonReader.LinePosition}.  Expected property name.");
                    }

                    var parts = packageString.Split('/');
                    if (parts.Length != 2)
                    {
                        throw new InvalidDataException($"Unexpected entry in {lockFilePath} at line {jsonReader.LineNumber}, position {jsonReader.LinePosition}.  Expected string of format id/version.");
                    }

                    EnsureRead(jsonReader); // StartObject
                    ReadToValue(jsonReader, JsonToken.PropertyName, "type");
                    EnsureRead(jsonReader); // value
                    if (jsonReader.Value.Equals("package"))
                    {
                        yield return new PackageIdentity(parts[0], NuGetVersion.Parse(parts[1]));
                    }

                    ReadToToken(jsonReader, JsonToken.EndObject); // Move to End
                    EnsureRead(jsonReader); // next PropertyName
                }
            }
        }

        private static void ReadToValue(JsonTextReader jsonReader, JsonToken tokenType, object value)
        {
            while (jsonReader.Value == null || jsonReader.TokenType != tokenType || !jsonReader.Value.Equals(value))
            {
                EnsureRead(jsonReader);
            }
        }
        private static void ReadToToken(JsonTextReader jsonReader, JsonToken tokenType)
        {
            while (jsonReader.TokenType != tokenType)
            {
                EnsureRead(jsonReader);
            }
        }

        private static void EnsureRead(JsonTextReader reader)
        {
            if (!reader.Read())
            {
                throw new EndOfStreamException();
            }
        }
    }
}
