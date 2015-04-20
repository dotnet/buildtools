// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.NuGet.Build.Tasks
{
    public sealed class ReadNuGetPackageReferences : Task
    {
        private readonly List<ITaskItem> _nuGetPackageReferences = new List<ITaskItem>();

        /// <summary>
        /// The target framework monikers to use when selecting assets from packages.
        /// </summary>
        [Required]
        public string[] TargetFrameworkMonikers
        {
            get; set;
        }

        [Required]
        public string ProjectLockJsonFile
        {
            get; set;
        }

        [Output]
        public ITaskItem[] NuGetPackageReferences
        {
            get { return _nuGetPackageReferences.ToArray(); }
        }

        public override bool Execute()
        {
            using (var streamReader = new StreamReader(ProjectLockJsonFile))
            {
                var lockFile = JObject.Load(new JsonTextReader(streamReader));
                var libraries = (JObject)lockFile["libraries"];
                foreach (var library in libraries)
                {
                    var nameParts = library.Key.Split('/');
                    var taskItem = new TaskItem(nameParts[0]);
                    taskItem.SetMetadata("VersionRange", nameParts[1]);
                    _nuGetPackageReferences.Add(taskItem);
                }
            }

            return true;
        }
    }
}
