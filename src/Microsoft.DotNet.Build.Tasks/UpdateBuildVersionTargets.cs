// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public class UpdateBuildVersionTargets : Task
    {
        [Required]
        public string BuildVersionTargetsFile { get; set; }

        [Required]
        public string RevisionNumber { get; set; }

        public override bool Execute()
        {
            Log.LogMessage("Starting UpdateBuildVersionTargets");

            XDocument versionProps = XDocument.Load(BuildVersionTargetsFile);
            var defaultNS = versionProps.Root.GetDefaultNamespace();

            var buildNode =
                (from el in versionProps.Descendants(defaultNS + "BuildToolsRevisionNumber")
                 select el).FirstOrDefault();

            buildNode.Value = RevisionNumber;
            versionProps.Save(BuildVersionTargetsFile);

            Log.LogMessage(string.Format("UpdateBuildVersionTargets completed successfully - the revision number is now {0}", RevisionNumber));

            return true;
        }
    }
}
