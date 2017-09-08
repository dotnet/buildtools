// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO.Compression;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class ZipFileInjectFile : BuildTask
    {
        /// <summary>
        /// The path to the zip archive to be injected into.
        /// </summary>
        [Required]
        public string TargetArchive { get; set; }

        /// <summary>
        /// Files to inject into the zip archive. "ArchivePath" metadata is used
        /// as the relative path from the root of the archive to place the file.
        /// </summary>
        public ITaskItem[] InjectFiles { get; set; }

        public override bool Execute()
        {
            try
            {
                Log.LogMessage(
                    MessageImportance.High,
                    $"Injecting files into '{TargetArchive}'...");

                using (ZipArchive archive = ZipFile.Open(TargetArchive, ZipArchiveMode.Update))
                {
                    foreach (ITaskItem file in InjectFiles)
                    {
                        string archivePath = file.GetMetadata("ArchivePath");
                        if (string.IsNullOrEmpty(archivePath))
                        {
                            throw new ArgumentException($"Item '{file.ItemSpec}' does not have a value for required metadata 'ArchivePath'.");
                        }

                        ZipArchiveEntry newEntry = archive.CreateEntryFromFile(
                            file.ItemSpec,
                            archivePath);

                        Log.LogMessage(
                            MessageImportance.Low,
                            $"Injected into '{TargetArchive}': '{file.ItemSpec}' as '{newEntry.FullName}'");
                    }
                }
            }
            catch (Exception e)
            {
                // We have 2 log calls because we want a nice error message but we also want to capture the callstack in the log.
                Log.LogError($"An exception has occurred while trying to inject into '{TargetArchive}'.");
                Log.LogErrorFromException(e, /*show stack=*/ true, /*show detail=*/ true, TargetArchive);
                return false;
            }
            return true;
        }
    }
}
