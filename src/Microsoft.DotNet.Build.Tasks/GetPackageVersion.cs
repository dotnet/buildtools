// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GetPackageVersion : Task
    {
        [Required]
        public string RevisionNumber { get; set; }

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

            // if this is a prerelease version then append the build number to the end
            Regex preReleaseMatch = new Regex("-[0-9A-Za-z]+$");
            if (preReleaseMatch.Match(embeddedVerNode.Value).Success)
                PackageVersion = string.Format("{0}-{1}", embeddedVerNode.Value, RevisionNumber);
            else
                PackageVersion = embeddedVerNode.Value;

            Log.LogMessage(string.Format("GetPackageVersion completed successfully - chose version {0}", PackageVersion));

            return true;
        }
    }
}
