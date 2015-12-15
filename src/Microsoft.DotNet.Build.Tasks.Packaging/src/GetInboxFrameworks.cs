// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class GetInboxFrameworks : PackagingTask
    {
        [Required]
        public string FrameworkListsPath
        {
            get;
            set;
        }

        [Required]
        public string AssemblyName
        {
            get;
            set;
        }

        public string AssemblyVersion
        {
            get;
            set;
        }

        [Output]
        public string[] InboxFrameworks
        {
            get;
            set;
        }

        public override bool Execute()
        {
            if (null == FrameworkListsPath)
            {
                _log.LogError("FrameworkListsPath argument must be specified");
                return false;
            }

            if (String.IsNullOrEmpty(AssemblyName))
            {
                _log.LogError("AssemblyName argument must be specified");
                return false;
            }

            if (!Directory.Exists(FrameworkListsPath))
            {
                _log.LogError("FrameworkListsPath '{0}' does not exist", FrameworkListsPath);
                return false;
            }

            _log.LogMessage(LogImportance.Low, "Determining inbox frameworks for {0}, {1}", AssemblyName, AssemblyVersion);


            InboxFrameworks = Frameworks.GetInboxFrameworksList(FrameworkListsPath, AssemblyName, AssemblyVersion, _log);

            return !_log.HasLoggedErrors;
        }
    }
}
