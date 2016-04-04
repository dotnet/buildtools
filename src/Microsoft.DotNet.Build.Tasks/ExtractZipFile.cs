// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using System.IO.Compression;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class ExtractZipFile : Task
    {
        /// <summary>
        /// The path to the directory to be archived.
        /// </summary>
        [Required]
        public string SourceArchive { get; set; }

        /// <summary>
        /// The path of the archive to be created.
        /// </summary>
        [Required]
        public string DestinationDirectory { get; set; }

        /// <summary>
        /// Indicates if the destination archive should be overwritten if it already exists.
        /// </summary>
        public bool OverwriteDestination { get; set; }

        public override bool Execute()
        {
            if (Directory.Exists(DestinationDirectory) && OverwriteDestination == true)
            {
                Log.LogMessage(MessageImportance.Low, "'{0}' already exists, trying to delete before unzipping...", DestinationDirectory);
                Directory.Delete(DestinationDirectory, recursive: true);
            }

            Log.LogMessage(MessageImportance.High, "Decompressing '{0}' into '{1}'...", SourceArchive, DestinationDirectory);
            if (!Directory.Exists(Path.GetDirectoryName(DestinationDirectory)))
                Directory.CreateDirectory(Path.GetDirectoryName(DestinationDirectory));

            ZipFile.ExtractToDirectory(SourceArchive, DestinationDirectory);

            return true;
        }
    }
}
