// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Xml.Linq;
using System.Diagnostics;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks
{
    public class MakeResourcesPriFile : Task
    {
        private string _reswListFile;
        private string _priListFile;
        private string _tempFolder;
        private string _modifiedConfigFile;

        [Required]
        public ITaskItem[] ResWFiles { get; set; }

        [Required]
        public string RunnersPriFilePath { get; set; }

        [Required]
        public string NewPriFilePath { get; set; }

        [Required]
        public string MakePriExecutablePath { get; set; }

        [Required]
        public string ConfigFilePath { get; set; }

        public override bool Execute()
        {
            try
            {
                Initialize();                
                WritePriListFile();
                WriteReswListFile();
                UpdateConfigFile();
                MergePriFiles();
                Cleanup();
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, showStackTrace: true);
                return false; // fail the task
            }

            return !Log.HasLoggedErrors;
        }

        private void Initialize()
        {
            if (!File.Exists(ConfigFilePath))
            {
                throw new ArgumentException($"MakePri config file: {ConfigFilePath} was not found.");
            }

            if (!File.Exists(RunnersPriFilePath))
            {
                throw new ArgumentException($"App runner resources.pri file: {RunnersPriFilePath} was not found.");
            }

            if (!File.Exists(MakePriExecutablePath))
            {
                throw new ArgumentException($"MakePri.exe was not found in path: {MakePriExecutablePath}");
            }

            _tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "makeprifiles");
            _reswListFile = Path.Combine(_tempFolder, "reswlist.RESFILES");
            _priListFile = Path.Combine(_tempFolder, "prilist.RESFILES");
            _modifiedConfigFile = Path.Combine(_tempFolder, "modifiedconfigfile.xml");

            if (!Directory.Exists(_tempFolder))
            {
                Directory.CreateDirectory(_tempFolder);
            }
        }

        private void Cleanup()
        {
            // This files are only used by MakePri.exe so we can safely delete them after we are done merging the resources. 
            try { Directory.Delete(_tempFolder, recursive: true); }
            catch { }
        }

        private void WritePriListFile()
        {
            using (var streamWriter = new StreamWriter(File.Create(_priListFile)))
            {
                streamWriter.WriteLine(RunnersPriFilePath);
            }
        }

        private void WriteReswListFile()
        {
            using (var streamWriter = new StreamWriter(File.Create(_reswListFile)))
            {
                foreach (ITaskItem file in ResWFiles)
                {
                    streamWriter.WriteLine(file.ItemSpec);
                }
            }
        }
        
        private void UpdateConfigFile()
        {
            string text = File.ReadAllText(ConfigFilePath);
            text = text.Replace("{reswfilelist}", _reswListFile);
            text = text.Replace("{prireslist}", _priListFile);
            File.WriteAllText(_modifiedConfigFile, text);
        }

        private void MergePriFiles()
        {
            string projectRoot = Path.GetDirectoryName(RunnersPriFilePath);
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = MakePriExecutablePath,
                Arguments = $"versioned /o /pr {projectRoot} /cf {_modifiedConfigFile} /of {NewPriFilePath} /if {RunnersPriFilePath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
            };

            Process process = new Process()
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.ErrorDataReceived += new DataReceivedEventHandler(ProcessErrorOutputEventHandler);

            process.Start();
            process.BeginErrorReadLine();
            process.WaitForExit();
        }

        void ProcessErrorOutputEventHandler(object sender, DataReceivedEventArgs data)
        {
            if (!string.IsNullOrEmpty(data.Data))
            {
                throw new Exception($"MakePri failed while creating resources.pri with error: {data.Data}");
            }
        }
    }
}