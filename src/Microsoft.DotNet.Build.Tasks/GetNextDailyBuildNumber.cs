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
    public class GetNextDailyBuildNumber : Task
    {
        [Required]
        public string VersionPropsFile { get; set; }

        [Output]
        public string DailyBuildNumber { get; set; }

        public override bool Execute()
        {
            Log.LogMessage("Starting GetNextDailyBuildNumber");

            XDocument versionProps = XDocument.Load(VersionPropsFile);
            var defaultNS = versionProps.Root.GetDefaultNamespace();

            var dailyBuildNode =
                (from el in versionProps.Descendants(defaultNS + "DailyBuildNumber")
                 select el).FirstOrDefault();

            Debug.Assert(dailyBuildNode != null, "dailyBuildNode shouldn't be null");
            var incDailyBuildNum = int.Parse(dailyBuildNode.Value) + 1;

            DailyBuildNumber = incDailyBuildNum.ToString();

            dailyBuildNode.Value = DailyBuildNumber;
            versionProps.Save(VersionPropsFile);

            Log.LogMessage(string.Format("GetNextDailyBuildNumber completed successfully - the daily build number is now {0}", DailyBuildNumber));

            return true;
        }
    }
}
