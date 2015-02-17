// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using LibGit2Sharp;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GetPackageVersion : Task
    {
        [Required]
        public string BuildNumber { get; set; }

        [Required]
        public string NuSpecFile { get; set; }

        [Output]
        public string PackageVersion { get; set; }

        public override bool Execute()
        {
            Log.LogMessage("Starting GetPackageVersion");

            XDocument nuspecFile = XDocument.Load(NuSpecFile);
            var ns = nuspecFile.Root.GetDefaultNamespace();

            var embeddedVerNode =
                (from el in nuspecFile.Descendants(ns + "version")
                select el).FirstOrDefault();

            Debug.Assert(embeddedVerNode != null, "embeddedVerNode shouldn't be null");
            PackageVersion = string.Format("{0}-{1}", embeddedVerNode.Value, BuildNumber);

            Log.LogMessage(string.Format("GetPackageVersion completed successfully - chose version {0}", PackageVersion));

            return true;
        }
    }
}
