// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
                Log.LogError("FrameworkListsPath argument must be specified");
                return false;
            }

            if (String.IsNullOrEmpty(AssemblyName))
            {
                Log.LogError("AssemblyName argument must be specified");
                return false;
            }

            if (!Directory.Exists(FrameworkListsPath))
            {
                Log.LogError("FrameworkListsPath '{0}' does not exist", FrameworkListsPath);
                return false;
            }

            Log.LogMessage(LogImportance.Low, "Determining inbox frameworks for {0}, {1}", AssemblyName, AssemblyVersion);


            InboxFrameworks = Frameworks.GetInboxFrameworksList(FrameworkListsPath, AssemblyName, AssemblyVersion, Log);

            return !Log.HasLoggedErrors;
        }
    }
}
