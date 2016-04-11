// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Metedata and information for a package listed in the lock file.
    /// </summary>
    internal sealed class NuGetPackageObject
    {
        /// <summary>
        /// A Lazy to get the full path to the package. In some cases we want to represent packages where we don't actually have
        /// a path to it, but we don't care about that fact until we try getting assets to it. Thus we won't force us to have
        /// a package path until we actually need it.
        /// </summary>
        private readonly Lazy<string> _fullPackagePath;

        public NuGetPackageObject(string id, string version, bool isProject, Func<string> fullPackagePathGenerator, JObject targetObject, JObject libraryObject)
        {
            Id = id;
            Version = version;
            IsProject = isProject;
            _fullPackagePath = new Lazy<string>(fullPackagePathGenerator);
            TargetObject = targetObject;
            LibraryObject = libraryObject;
        }

        public string Id { get; }
        public string Version { get; }
        public bool IsProject { get; }

        /// <summary>
        /// The JSON object from the "targets" section in the project.lock.json for this package.
        /// </summary>
        public JObject TargetObject { get; }

        /// <summary>
        /// The JSON object from the "libraries" section in the project.lock.json for this package.
        /// </summary>
        public JObject LibraryObject { get; }

        public string GetFullPathToFile(string relativePath)
        {
            relativePath = relativePath.Replace('/', '\\');
            return Path.Combine(_fullPackagePath.Value, relativePath);
        }
    }
}