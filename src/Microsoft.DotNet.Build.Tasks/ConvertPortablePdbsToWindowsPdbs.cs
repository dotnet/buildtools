// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DiaSymReader.Tools;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;

namespace Microsoft.DotNet.Build.Tasks
{
    public class ConvertPortablePdbsToWindowsPdbs : BuildTask, ICancelableTask
    {
        private const string PdbPathMetadata = "PdbPath";
        private const string TargetPathMetadata = "TargetPath";

        private const string NoDebugDirectoryEntriesMessage =
            "has no Debug directory entries. If this DLL is created by GenFacades " +
            "(Microsoft.Win32.Registry.AccessControl, System.Security.Permissions), this is a " +
            "known issue tracked by 'https://github.com/dotnet/buildtools/issues/1739'.";

        [Required]
        public ITaskItem[] Files { get; set; }

        public bool SuppressSourceLinkConversion { get; set; }

        private bool _cancel;

        public override bool Execute()
        {
            var parsedConversionOptions = new PortablePdbConversionOptions(SuppressSourceLinkConversion);

            var converter = new PdbConverter(
                d => Log.LogError(d.ToString(CultureInfo.InvariantCulture)));

            foreach (ITaskItem file in Files)
            {
                if (_cancel)
                {
                    break;
                }

                try
                {
                    ConvertPortableToWindows(file, converter, parsedConversionOptions);
                }
                catch (Exception e)
                {
                    Log.LogErrorFromException(e, true, true, file.ItemSpec);
                }
            }

            return !Log.HasLoggedErrors;
        }

        public void Cancel()
        {
            _cancel = true;
        }

        private void ConvertPortableToWindows(
            ITaskItem file,
            PdbConverter converter,
            PortablePdbConversionOptions parsedConversionOptions)
        {
            string pdbPath = file.GetMetadata(PdbPathMetadata);

            if (string.IsNullOrEmpty(pdbPath))
            {
                Log.LogError($"No '{PdbPathMetadata}' metadata found for '{file}'.");
                return;
            }

            string targetPath = file.GetMetadata(TargetPathMetadata);

            if (string.IsNullOrEmpty(targetPath))
            {
                Log.LogError($"No '{TargetPathMetadata}' metadata found for '{file}'.");
                return;
            }

            using (var sourcePdbStream = new FileStream(pdbPath, FileMode.Open, FileAccess.Read))
            {
                if (PdbConverter.IsPortable(sourcePdbStream))
                {
                    Log.LogMessage(
                        MessageImportance.Low,
                        $"Converting portable PDB '{file.ItemSpec}'...");

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

                    using (var peStream = new FileStream(file.ItemSpec, FileMode.Open, FileAccess.Read))
                    using (var peReader = new PEReader(peStream, PEStreamOptions.LeaveOpen))
                    {
                        if (peReader.ReadDebugDirectory().Length > 0)
                        {
                            using (var outPdbStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                            {
                                converter.ConvertPortableToWindows(
                                    peReader,
                                    sourcePdbStream,
                                    outPdbStream,
                                    parsedConversionOptions);
                            }

                            Log.LogMessage(
                                MessageImportance.Normal,
                                $"Portable PDB '{file.ItemSpec}' -> '{targetPath}'");
                        }
                        else
                        {
                            Log.LogWarning($"'{file.ItemSpec}' {NoDebugDirectoryEntriesMessage}");
                        }
                    }
                }
                else
                {
                    Log.LogMessage(
                        MessageImportance.Normal,
                        $"PDB is not portable, skipping: '{file.ItemSpec}'");
                }
            }
        }
    }
}
