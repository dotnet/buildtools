// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GatherDirectoriesToRestore : BuildTask
    {
        [Required]
        public string[] RootDirectories { get; set; }

        [Output]
        public string[] DirectoriesToRestore { get; set; }

        private int directoryListLength = 7000;

        public int DirectoryListLength
        {
            get { return directoryListLength; }
            set { directoryListLength = value; }
        }

        public override bool Execute()
        {
            HashSet<string> directoriesToRestore = new HashSet<string>();

            foreach (string rootDirectory in RootDirectories)
            {
                AddDirectoriesToRestore(rootDirectory, rootDirectory, directoriesToRestore);
            }

            List<string> resultBuilder = new List<string>();
            StringBuilder sb = new StringBuilder(directoryListLength);

            foreach (string directoryPath in directoriesToRestore)
            {
                sb.Append('"');
                sb.Append(directoryPath);
                sb.Append("\" ");

                if (sb.Length >= directoryListLength)
                {
                    resultBuilder.Add(sb.ToString());
                    sb.Clear();
                }
            }

            if (sb.Length > 0)
            {
                resultBuilder.Add(sb.ToString());
            }

            DirectoriesToRestore = resultBuilder.ToArray();

            return true;
        }

        public void AddDirectoriesToRestore(string directoryToExplore, string directoryToAdd, HashSet<string> directoriesToRestore)
        {
            bool hasProjectJson = File.Exists(Path.Combine(directoryToExplore, "project.json"));

            if (hasProjectJson)
            {
                directoriesToRestore.Add(directoryToAdd);
            }

            foreach (string childDirectory in Directory.GetDirectories(directoryToExplore))
            {
                AddDirectoriesToRestore(childDirectory, hasProjectJson ? childDirectory : directoryToAdd, directoriesToRestore);
            }
        }
    }
}
