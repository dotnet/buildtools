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
    public class GetNetCoreAppVersionsFromFile : Task
    {
        [Required]
        public string PathToVersionsFile { get; set; }

        [Required]
        public ITaskItem[] VersionKeys { get; set; }

        [Output]
        public ITaskItem[] VersionHashes { get; set; }

        public override bool Execute()
        {
            if (VersionKeys == null || VersionKeys.Length == 0)
                return true;
            if (!File.Exists(PathToVersionsFile))
            {
                Log.LogError($"File {PathToVersionsFile} does not exist.");
                return false;
            }

            List<ITaskItem> hashes = new List<ITaskItem>();
            string versionsFileContents = File.ReadAllText(PathToVersionsFile);
            foreach (var key in VersionKeys)
            {
                ITaskItem itemWithHash;
                if (TryGetVersionHash(key, versionsFileContents, out itemWithHash))
                    hashes.Add(itemWithHash);
                else
                    return false;
            }

            VersionHashes = hashes.ToArray();
            return true;
        }

        public bool TryGetVersionHash(ITaskItem keyItem, string fileContents, out ITaskItem itemWithHash)
        {
            itemWithHash = new TaskItem(keyItem.ItemSpec);
            var match = Regex.Match(fileContents, $"^{keyItem.ItemSpec} (\\S.*)", RegexOptions.Multiline);
            if (match.Success && match.Groups.Count > 1)
            {
                itemWithHash.SetMetadata("VersionHash", match.Groups[1].Value);
                return true;
            }
            else
            {
                Log.LogError($"Failed to parse out the hash for {keyItem.ItemSpec}");
                return false;
            }
        }
    }
}
