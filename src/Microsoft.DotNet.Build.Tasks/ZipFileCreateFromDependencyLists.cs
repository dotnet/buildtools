// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class ZipFileCreateFromDependencyLists : Task
    {
        /// <summary>
        /// List of dependency text files to be unified.
        /// </summary>
        [Required]
        public string DependencyListsFolder { get; set; }

        /// <summary>
        /// The path of the archive to be created.
        /// </summary>
        [Required]
        public string DestinationArchive { get; set; }

        [Required]
        /// <summary>
        /// Base path for paths specified in txt files in DependencyListsFolder
        /// </summary>
        public string RelativePathBaseDirectory { get; set; }

        /// <summary>
        /// Indicates if the destination archive should be overwritten if it already exists.
        /// </summary>
        public bool OverwriteDestination { get; set; } = false;

        public override bool Execute()
        {
            Log.LogMessage($"DependencyListsFolder = {DependencyListsFolder}");
            Log.LogMessage($"DestinationArchive = {DestinationArchive}");
            Log.LogMessage($"RelativePathBaseDirectory = {RelativePathBaseDirectory}");
            Log.LogMessage($"OverwriteDestination = {OverwriteDestination}");

            try
            {
                if (File.Exists(DestinationArchive))
                {
                    if (OverwriteDestination == true)
                    {
                        Log.LogMessage(MessageImportance.Low, $"{DestinationArchive} already existed, deleting before zipping...");
                        File.Delete(DestinationArchive);
                    }
                    else
                    {
                        Log.LogWarning($"'{DestinationArchive}' already exists, no change will occur. Did you forget to set '{nameof(OverwriteDestination)}' to true?");
                    }
                }

                Log.LogMessage(MessageImportance.High, $"Compressing files listed in {DependencyListsFolder} into {DestinationArchive}...");
                if (!Directory.Exists(Path.GetDirectoryName(DestinationArchive)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(DestinationArchive));
                }
                List<string> filesForArchiving = GetUniquePaths(DependencyListsFolder, "*");
                Log.LogMessage($"{DependencyListsFolder} contained files referencing {filesForArchiving.Count} unique file paths.");
                ZipDependencies(filesForArchiving, RelativePathBaseDirectory, DestinationArchive);
            }
            catch (Exception e)
            {
                // We have 2 log calls because we want a nice error message but we also want to capture the callstack in the log.
                Log.LogError($"An exception has occured while trying to compress files listed in '{DependencyListsFolder}' into '{DestinationArchive}'.");
                Log.LogMessage(MessageImportance.Low, e.ToString());
                return false;
            }

            return true;
        }


        private List<string> GetUniquePaths(string folder, string filePattern)
        {
            HashSet<string> everyDependency = new HashSet<string>();
            foreach (string path in Directory.GetFiles(folder, filePattern))
            {
                IEnumerable<string> specificDependencies = File.ReadAllLines(path);
                everyDependency.UnionWith(specificDependencies);
            }
            return everyDependency.Distinct().ToList<string>();
        }

        private void ZipDependencies(List<string> dependencies, string basePath, string outputFileName)
        {
            using (FileStream zipToOpen = new FileStream(outputFileName, FileMode.Create))
            {
                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                {
                    foreach (string dependency in dependencies)
                    {
                        string absolutePath = Path.Combine(basePath, dependency);
                        archive.CreateEntryFromFile(absolutePath, dependency.Replace('\\', '/'));
                    }
                }
            }
        }
    }
}
