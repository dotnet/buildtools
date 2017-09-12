// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GetNetCoreAppVersionsFromFile : BuildTask
    {
        [Required]
        public string PathToVersionsFile { get; set; }

        [Output]
        public ITaskItem[] VersionHashes { get; set; }

        public override bool Execute()
        {
            if (!File.Exists(PathToVersionsFile))
            {
                Log.LogError($"File {PathToVersionsFile} does not exist.");
                return false;
            }

            List<ITaskItem> hashes = new List<ITaskItem>();
            var versionFileLines = File.ReadAllLines(PathToVersionsFile);
            foreach (var line in versionFileLines)
            {
                var match = Regex.Match(line, @"(\S.*) (\S.*)");
                if (match.Success && match.Groups.Count > 2)
                {
                    var itemWithHash = new TaskItem(match.Groups[1].Value);
                    itemWithHash.SetMetadata("VersionHash", match.Groups[2].Value);
                    hashes.Add(itemWithHash);
                }
            }
            VersionHashes = hashes.ToArray();
            return true;
        }
    }
}
