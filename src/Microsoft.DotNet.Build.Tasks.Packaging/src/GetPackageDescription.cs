// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class GetPackageDescription : ITask
    {
        // avoid parsing the same document multiple times on a single node.
        private static Dictionary<string, Dictionary<string, string>> s_descriptionCache = new Dictionary<string, Dictionary<string, string>>();

        private TaskLoggingHelper _log;

        public GetPackageDescription()
        {
            _log = new TaskLoggingHelper(this);
        }

        public IBuildEngine BuildEngine
        {
            get;
            set;
        }

        public ITaskHost HostObject
        {
            get;
            set;
        }

        [Required]
        public ITaskItem DescriptionFile
        {
            get;
            set;
        }

        [Required]
        public string PackageId
        {
            get;
            set;
        }

        [Output]
        public string Description
        {
            get;
            set;
        }

        public bool Execute()
        {
            if (null == DescriptionFile)
            {
                _log.LogError("DescriptionFile argument must be specified");
                return false;
            }

            if (String.IsNullOrEmpty(PackageId))
            {
                _log.LogError("PackageId argument must be specified");
                return false;
            }

            string descriptionPath = DescriptionFile.GetMetadata("FullPath");

            if (!File.Exists(descriptionPath))
            {
                _log.LogError("DescriptionFile '{0}' does not exist", descriptionPath);
                return false;
            }

            Dictionary<string, string> descriptionTable = null;

            if (!s_descriptionCache.TryGetValue(descriptionPath, out descriptionTable))
            {
                // no cache, load it now.
                descriptionTable = LoadDescriptions(descriptionPath);

                s_descriptionCache[descriptionPath] = descriptionTable;
            }

            string description = null;

            if (descriptionTable != null)
            {
                descriptionTable.TryGetValue(PackageId, out description);
            }

            if (String.IsNullOrEmpty(description))
            {
                _log.LogError("Unable to find description for package {0}", PackageId);
            }

            Description = description;

            return !_log.HasLoggedErrors;
        }

        private Dictionary<string, string> LoadDescriptions(string descriptionPath)
        {
            try
            {
                Dictionary<string, string> descriptions = new Dictionary<string, string>();

                var allMetadata = PackageMetadata.ReadFrom(descriptionPath);

                foreach (PackageMetadata metadata in allMetadata)
                {
                    descriptions[metadata.Name] = FormatDescription(metadata, descriptionPath);
                }

                return descriptions;
            }
            catch (Exception excep)
            {
                if (excep is IOException || excep is UnauthorizedAccessException)
                {
                    _log.LogError("Error loading {0}, {1}", descriptionPath, excep);
                    return null;
                }
                else
                {
                    throw;
                }
            }
        }

        private string FormatDescription(PackageMetadata metadata, string descriptionPath)
        {
            if (String.IsNullOrEmpty(metadata.Description))
            {
                _log.LogError("Package {0} has no Description, please add it to {1}", metadata.Name, descriptionPath);
            }

            StringBuilder description = new StringBuilder(metadata.Description);

            if (metadata.CommonTypes != null && metadata.CommonTypes.Length > 0)
            {
                description.AppendLine();
                description.AppendLine();
                description.AppendLine("Commonly Used Types:");

                foreach (string type in metadata.CommonTypes)
                {
                    description.AppendLine(type);
                }
            }

            return description.ToString();
        }
    }
}
