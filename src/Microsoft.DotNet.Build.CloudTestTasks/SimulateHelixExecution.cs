// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    /// <summary>
    /// Simulates Helix Job execution using real Helix Client scripts, on the local machine.
    /// Currently this includes downloading the URLs listed in the helix job list, since it's a fair amount of extra
    /// work to make a job list with local file paths, plus this can be a source of problems to debug.
    /// </summary>
    public sealed class SimulateHelixExecution : Task
    {
        /// <summary>
        /// Job-list JSON file describing Helix Work Item(s)
        /// </summary>
        [Required]
        public ITaskItem JobList { get; set; }

        /// <summary>
        /// For Job lists with greater than one work item, an optional string to filter execution down to a single test.
        /// </summary>
        public string WorkItemExecutionFilter { get; set; }

        /// <summary>
        /// Base folder path for executing jobs.  Will be created if it does not exist.
        /// </summary>
        public string ExecutionFolder { get; set; }

        /// <summary>
        /// Python executable for runner scripts. Will attempt to find it in default places if not provided, but cannot continue without it.
        /// </summary>
        public string PythonExecutable { get; set; }

        /// <summary>
        /// Optional CorrelationId value for run.  Set to a constant value to prevent repeated unzipping, but may leave stale files. 
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// Input items with metadata stripped out.
        /// </summary>
        [Output]
        public ITaskItem[] ExecutionLogs { get; set; }

        // Private constants
        private const string DefaultPythonPathWindows = @"c:\Python27\python.exe";
        private const string DefaultPythonPathUnix = "/usr/bin/python";
        private const string HelixScriptsUri = "http://dotnetbuildscripts.blob.core.windows.net/scripts/reproscripts.zip";
        private const string HelixConfigUri = "http://dotnetbuildscripts.blob.core.windows.net/scripts/reproconfig.zip";

        // Private variables
        private string configFolder;
        private string correlationReproFolder;
        private string pythonScriptsFolder;
        private bool runningOnWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private string logFolder;
        private List<HelixJobListEntry> jobs;
        private List<TaskItem> logs = new List<TaskItem>();

        public override bool Execute()
        {
            Setup();

            // Quit early in the case of Setup() failure
            if (Log.HasLoggedErrors)
            {
                return false;
            }

            foreach (HelixJobListEntry currentWorkItem in jobs)
            {
                UnpackPayloads(currentWorkItem);
                ExecuteWorkItem(currentWorkItem);
            }

            ExecutionLogs = logs.ToArray();
            return !Log.HasLoggedErrors;
        }
        private void UnpackPayloads(HelixJobListEntry currentWorkItem)
        {
            foreach (string correlationPayloadFile in currentWorkItem.CorrelationPayloadUris)
            {
                string resolvedPayload = correlationPayloadFile;
                // We're handling the job list that has HTTP Uris, so need to download these first
                // We could eventually just use the local files but this would involve producing a 
                // separate, and not-real-for-Helix Job list JSON.
                if (correlationPayloadFile.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = correlationPayloadFile.Substring(correlationPayloadFile.LastIndexOf('/') + 1);
                    fileName = fileName.Substring(0, fileName.IndexOf('?'));
                    resolvedPayload = Path.Combine(ExecutionFolder, fileName);
                    if (!File.Exists(resolvedPayload))
                    {
                        DownloadTo(correlationPayloadFile, resolvedPayload);
                    }
                }
                if (File.Exists(resolvedPayload))
                {
                    Unzip(resolvedPayload, correlationReproFolder, false);
                }
                else
                {
                    Log.LogError($"Could not find payload at {correlationPayloadFile}");
                }
            }
            string workitemPayload = currentWorkItem.PayloadUri;
            if (!string.IsNullOrEmpty(workitemPayload))
            {
                if (workitemPayload.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = workitemPayload.Substring(workitemPayload.LastIndexOf('/') + 1);
                    if (fileName.IndexOf('?') > 0)
                    {
                        fileName = fileName.Substring(0, fileName.IndexOf('?'));
                    }
                    workitemPayload = Path.Combine(ExecutionFolder, fileName);
                    if (!File.Exists(workitemPayload))
                    {
                        DownloadTo(currentWorkItem.PayloadUri, workitemPayload);
                    }
                }
                if (File.Exists(workitemPayload))
                {
                    Unzip(workitemPayload, Path.Combine(ExecutionFolder, "work", CorrelationId, currentWorkItem.WorkItemId), true);
                }
                else
                {
                    Log.LogWarning($"Could not obtain work item payload specified by {currentWorkItem.PayloadUri}.  This will likely break execution.");
                }
            }
        }

        private void ExecuteWorkItem(HelixJobListEntry currentWorkItem)
        {
            Log.LogMessage($"Executing {currentWorkItem.WorkItemId}");
            Log.LogMessage($"Command: '{currentWorkItem.Command}'\nTimeout: {currentWorkItem.TimeoutInSeconds} seconds. ");

            Dictionary<string, string> environment = GenerateHelixEnvironmentVariables(currentWorkItem.WorkItemId);
            string generatedLogRoot = environment["HELIX_LOG_ROOT"];
            Directory.CreateDirectory(generatedLogRoot);
            string runnerScript = GenerateRunnerScript(environment, currentWorkItem.Command, Path.Combine(ExecutionFolder, "runnerscripts", currentWorkItem.WorkItemId));

            ProcessStartInfo pStartInfo = new ProcessStartInfo()
            {
                FileName = runnerScript,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                WorkingDirectory = Path.Combine(ExecutionFolder, "work", CorrelationId, currentWorkItem.WorkItemId),
                UseShellExecute = false
            };

            DateTime startTime = DateTime.Now;
            Process workItem = Process.Start(pStartInfo);
            string output = workItem.StandardOutput.ReadToEnd();

            workItem.Refresh();

            if (workItem.WaitForExit((int)TimeSpan.FromSeconds(currentWorkItem.TimeoutInSeconds).TotalMilliseconds))
            {
                Log.LogMessage($"Executed for {(workItem.ExitTime - startTime).Seconds}.{(workItem.ExitTime - startTime).Milliseconds} seconds. (Exit code: {workItem.ExitCode})");
                Log.LogMessage(MessageImportance.Low, $"Log:\n{output}");
            }
            // Timed out
            else
            {
                Log.LogError($"Timed out during execution.");
                TryKillProcess(workItem);
            }
            // Crashed, errors, or failed tests
            if (workItem.ExitCode != 0)
            {
                Log.LogError($"Non-0 exit code, please review logs.");
            }

            if (!string.IsNullOrEmpty(output))
            {
                string logPath = Path.Combine(generatedLogRoot, "stdout.log");
                File.WriteAllText(logPath, output);
            }
            Log.LogMessage(MessageImportance.Low, $"Logs for execution stored in {generatedLogRoot}");

            // HELIX_LOG_ROOT is customized per-workitem here, so we can associate it specially with the run.
            foreach (string file in Directory.GetFiles(generatedLogRoot))
            {
                TaskItem loggedFile = new TaskItem(file);
                loggedFile.SetMetadata("WorkItemFriendlyName", currentWorkItem.WorkItemId);
                loggedFile.SetMetadata("LogDirectory", generatedLogRoot);
                logs.Add(loggedFile);
            }
        }

        private string GenerateRunnerScript(Dictionary<string, string> environment, string command, string folder)
        {
            string filename = string.Empty;
            Directory.CreateDirectory(folder);
            if (runningOnWindows)
            {
                filename = Path.Combine(folder, $"{environment["HELIX_WORKITEM_FRIENDLYNAME"]}.cmd");

                using (StreamWriter sw = new StreamWriter(new FileStream(filename, FileMode.Create)))
                {
                    sw.WriteLine("SETLOCAL");
                    sw.WriteLine($"pushd {environment["HELIX_WORKITEM_PAYLOAD"]}");
                    foreach (string entry in environment.Keys)
                    {
                        sw.WriteLine($"set {entry}={environment[entry]}");
                    }
                    sw.WriteLine(command);
                    sw.WriteLine("popd");
                    sw.WriteLine("ENDLOCAL");
                }
            }
            else
            {
                filename = Path.Combine(folder, $"{environment["HELIX_WORKITEM_FRIENDLYNAME"]}.sh");

                using (StreamWriter sw = new StreamWriter(new FileStream(filename, FileMode.Create)))
                {
                    sw.NewLine = "\n";
                    sw.WriteLine("#!/usr/bin/env bash");
                    sw.WriteLine($"pushd {environment["HELIX_WORKITEM_PAYLOAD"]}");
                    foreach (string entry in environment.Keys)
                    {
                        sw.WriteLine($"export {entry}={environment[entry]}");
                    }
                    sw.WriteLine(command);
                    sw.WriteLine("popd");
                }
            }
            return filename;
        }

        private void TryKillProcess(Process p)
        {
            try
            {
                p.Kill();
            }
            catch (System.InvalidOperationException)
            {
                // Do nothing, has already exited.
            }
        }        

        private void Setup()
        {
            if (string.IsNullOrEmpty(CorrelationId))
            {
                CorrelationId = Guid.NewGuid().ToString();
            }
            if (!FindPythonExecutable())
            {
                return;
            }
            if (string.IsNullOrEmpty(ExecutionFolder))
            {
                ExecutionFolder = Path.Combine(Path.GetTempPath(), "HelixExecution");
            }
            else
            {
                ExecutionFolder = Path.GetFullPath(ExecutionFolder);
            }
            Directory.CreateDirectory(ExecutionFolder);
            correlationReproFolder = Path.Combine(ExecutionFolder, "work", CorrelationId, "Payload");

            // Get a copy of the Helix client scripts, and a config file:
            Log.LogMessage(MessageImportance.Low, $"Scripts.zip Source: {HelixScriptsUri}");
            DownloadTo(HelixScriptsUri, Path.Combine(ExecutionFolder, "scripts.zip"));
            pythonScriptsFolder = Path.Combine(ExecutionFolder, "scripts");
            Unzip(Path.Combine(ExecutionFolder, "scripts.zip"), pythonScriptsFolder);

            Log.LogMessage(MessageImportance.Low, "Config.zip Source: {0}", HelixConfigUri);
            DownloadTo(HelixConfigUri, Path.Combine(ExecutionFolder, "config.zip"));
            configFolder = Path.Combine(ExecutionFolder, "config");
            Unzip(Path.Combine(ExecutionFolder, "config.zip"), configFolder);

            logFolder = Path.Combine(ExecutionFolder, "logs");
            Directory.CreateDirectory(logFolder);

            // Could support http here too, but not sure this is interesting.
            jobs = JsonConvert.DeserializeObject<List<HelixJobListEntry>>(File.ReadAllText(JobList.ItemSpec));
            if (!string.IsNullOrEmpty(WorkItemExecutionFilter))
            {
                Log.LogMessage($"Filtering to items which start with {WorkItemExecutionFilter}");
                jobs = (from j in jobs
                        where j.WorkItemId.StartsWith(WorkItemExecutionFilter, StringComparison.OrdinalIgnoreCase)
                        select j)
                        .ToList();
            }
            Log.LogMessage($"Found {jobs.Count} work items to execute.");
        }

        private bool FindPythonExecutable()
        {
            // Short circuit in the case where a value for the PythonExecutable parameter is provided.
            if (!string.IsNullOrEmpty(PythonExecutable) && File.Exists(PythonExecutable))
            {
                Log.LogMessage($"Found python executable at {PythonExecutable}");
                return true;
            }

            string pythonExeName = runningOnWindows ? "python.exe" : "python";

            string pythonPath = Environment.GetEnvironmentVariable("PYTHONPATH");
            if (!string.IsNullOrEmpty(pythonPath))
            {
                string[] candidatePaths = pythonPath.Split(';', ':');
                foreach (string path in candidatePaths)
                {
                    if (File.Exists(Path.Combine(path, pythonExeName)))
                    {
                        PythonExecutable = Path.Combine(path, pythonExeName);
                    }
                }
                Log.LogMessage("Using PYTHONPATH environment variable for python executable.");
            }
            else
            {
                PythonExecutable = runningOnWindows ? DefaultPythonPathWindows : DefaultPythonPathUnix;
            }

            if (File.Exists(PythonExecutable))
            {
                Log.LogMessage($"Found python executable at {PythonExecutable}");
                return true;
            }
            else
            {
                Log.LogError("Could not find a python executable.");
                return false;
            }
        }

        private Dictionary<string, string> GenerateHelixEnvironmentVariables(string workItemFriendlyName)
        {
            Dictionary<string, string> environment = new Dictionary<string, string>();
            // There's a different path env separator on *NIX 
            if (runningOnWindows)
            {
                environment.Add("PYTHONPATH", $"{Path.GetDirectoryName(PythonExecutable)};{Path.Combine(ExecutionFolder, "scripts")}");
            }
            else
            {
                environment.Add("PYTHONPATH", $"{Path.GetDirectoryName(PythonExecutable)}:{Path.Combine(ExecutionFolder, "scripts")}");
            }
            environment.Add("HELIX_PYTHONPATH", PythonExecutable);
            environment.Add("HELIX_SCRIPT_ROOT", pythonScriptsFolder);
            environment.Add("HELIX_WORK_ROOT", Path.Combine(ExecutionFolder, "work"));
            environment.Add("HELIX_LOG_ROOT", Path.Combine(logFolder, CorrelationId, workItemFriendlyName));
            environment.Add("HELIX_CONFIG_ROOT", configFolder);
            environment.Add("HELIX_EVENT_URI", string.Empty);
            environment.Add("HELIX_EVENT_TIMEOUT", "10");
            environment.Add("HELIX_EVENT_RETRY_COUNT", "10");
            environment.Add("HELIX_CLIENT_INSTANCE_ID", "Repro");
            environment.Add("HELIX_CORRELATION_ID", CorrelationId);
            environment.Add("HELIX_WORKITEM_ID", workItemFriendlyName);
            environment.Add("HELIX_WORKITEM_FRIENDLYNAME", workItemFriendlyName);
            environment.Add("HELIX_CORRELATION_PAYLOAD", correlationReproFolder);
            environment.Add("HELIX_WORKITEM_ROOT", Path.Combine(ExecutionFolder, "work", CorrelationId, workItemFriendlyName));
            environment.Add("HELIX_WORKITEM_PAYLOAD", Path.Combine(ExecutionFolder, "work", CorrelationId, workItemFriendlyName));
            environment.Add("HELIX_RESULTS_CONTAINER_URI", "http://localhost");
            environment.Add("HELIX_RESULTS_CONTAINER_WSAS", "?ignored");
            environment.Add("HELIX_RESULTS_CONTAINER_RSAS", "?ignored");
            return environment;
        }

        private static void Unzip(string zipFile, string destinationPath, bool overwriteExistingFiles = false)
        {
            // Private implementation of unzip to allow for overwriting or not throwing when the file is already there.
            using (ZipArchive zip = ZipFile.OpenRead(zipFile))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    string zipDestinationPath = Path.Combine(destinationPath, entry.FullName);
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        zipDestinationPath = zipDestinationPath.Replace('\\', '/');
                    }

                    if (!File.Exists(zipDestinationPath) || overwriteExistingFiles)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(zipDestinationPath));
                        // Some malformed zips have empty folders... don't try to extract if this is a folder
                        if (!(zipDestinationPath.EndsWith("\\") || zipDestinationPath.EndsWith("/")))
                        {
                            entry.ExtractToFile(zipDestinationPath, true);
                        }
                    }
                }
            }
        }

        private void DownloadTo(string uri, string destinationPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            int BufferSize = 4096;
            using (var httpClient = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                {
                    using (
                        Stream contentStream = (httpClient.SendAsync(request).GetAwaiter().GetResult()).Content.ReadAsStreamAsync().GetAwaiter().GetResult(),
                        stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true))
                    {
                        contentStream.CopyToAsync(stream).GetAwaiter().GetResult();
                    }
                }
            }
        }
    }
    // Common Job list item.  Does not change, but could eventually go into a common model class.
    public class HelixJobListEntry
    {
        public string Command { get; set; }
        public List<string> CorrelationPayloadUris { get; set; }
        public string PayloadUri { get; set; }
        public string WorkItemId { get; set; }
        public int TimeoutInSeconds { get; set; }
    }
}

