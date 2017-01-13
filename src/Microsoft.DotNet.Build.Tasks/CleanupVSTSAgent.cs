using Microsoft.Build.Framework;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Microsoft.DotNet.Build.Tasks
{
    public class CleanupVSTSAgent : Microsoft.Build.Utilities.Task
    {
        public bool Clean { get; set; }

        public bool Report { get; set; }

        [Required]
        public string AgentDirectory { get; set; }

        [Required]
        public double RetentionDays { get; set; }

        public int? Retries { get; set; }

        public int? SleepTimeInMilliseconds { get; set; }
        public ITaskItem[] ProcessNamesToKill { get; set; }

        private static readonly int s_DefaultRetries = 3;
        private static readonly int s_DefaultSleepTime = 2000;

        public override bool Execute()
        {
            KillStaleProcesses();
            if (!Directory.Exists(AgentDirectory))
            {
                Log.LogMessage($"Agent directory specified: '{AgentDirectory}' does not exist.");
                return false;
            }
            if (!Retries.HasValue)
            {
                Retries = s_DefaultRetries;
            }
            if (!SleepTimeInMilliseconds.HasValue)
            {
                SleepTimeInMilliseconds = s_DefaultSleepTime;
            }

            bool returnValue = true;

            if (Report)
            {
                ReportDiskUsage();
            }
            if (Clean)
            {
                returnValue &= CleanupAgentsAsync().Result;
                // If report and clean are both 'true', then report disk usage both before and after cleanup.
                if (Report)
                {
                    Log.LogMessage("Disk usage after 'Clean'.");
                    ReportDiskUsage();
                }
            }

            return returnValue;
        }

        private void KillStaleProcesses()
        {
            foreach (string imageName in ProcessNamesToKill.Select(t => t.ItemSpec))
            {
                Process[] allInstances = Process.GetProcessesByName(imageName);
                foreach (Process proc in allInstances)
                {
                    try
                    {
                        if (!proc.HasExited)
                        {
                            proc.Kill();
                            Log.LogMessage($"Killed process {imageName} ({proc.Id})");
                        }
                    }
                    catch (Exception e)
                    {
                        Log.LogMessage($"Hit {e.GetType().ToString()} trying to kill process {imageName} ({proc.Id})");
                    }
                }
            }
        }

        private void ReportDiskUsage()
        {
            string lastDirectoryChecked = AgentDirectory;
            try
            {
                string drive = Path.GetPathRoot(AgentDirectory);
                DriveInfo driveInfo = new DriveInfo(drive);
                Log.LogMessage("Disk Usage Report");
                Log.LogMessage($"  Agent directory: {AgentDirectory}");
                Log.LogMessage($"  Drive letter: {drive}");
                Log.LogMessage($"  Total disk size: {string.Format("{0:N0}", driveInfo.TotalSize)} bytes");
                Log.LogMessage($"  Total disk free space: {string.Format("{0:N0}", driveInfo.TotalFreeSpace)} bytes");

                var workingDirectories = Directory.GetDirectories(Path.Combine(AgentDirectory, "_work"));
                var totalWorkingDirectories = workingDirectories != null ? workingDirectories.Length : 0;

                Log.LogMessage("  Agent info");
                Log.LogMessage($"    Total size of agent directory: {string.Format("{0:N0}", GetDirectoryAttributes(AgentDirectory).Item1)} bytes");
                Log.LogMessage($"    Total agent working directories: {totalWorkingDirectories}");

                if (totalWorkingDirectories > 0)
                {
                    int nameLength = 0;
                    foreach (string directoryName in workingDirectories)
                    {
                        nameLength = directoryName.Length > nameLength ? directoryName.Length : nameLength;
                    }
                    int sizeLength = string.Format("{0:N0}", driveInfo.TotalSize).Length;
                    string columnFormat = "      {0,-" + nameLength.ToString() + "}  {1," + sizeLength.ToString() + ":N0}  {2}";
                    Log.LogMessage(string.Format(columnFormat, "Folder name", "Size (bytes)", "Last Modified DateTime"));
                    foreach (var workingDirectory in workingDirectories)
                    {
                        lastDirectoryChecked = workingDirectory;
                        Tuple<long, DateTime> directoryAttributes = GetDirectoryAttributes(workingDirectory);
                        Log.LogMessage(string.Format(columnFormat, workingDirectory, directoryAttributes.Item1, directoryAttributes.Item2));
                    }
                }
            }
            catch (PathTooLongException)
            {
                Log.LogWarning("Hit PathTooLongException attempting to list info about agent directory.  There are likely files which cannot be cleaned up on the agent.");
                if (!string.IsNullOrEmpty(lastDirectoryChecked))
                {
                    Log.LogWarning($"Last directory checked : {lastDirectoryChecked} (likely the first inaccessible directory, alphabetically) ");
                }
            }
            catch (UnauthorizedAccessException)
            {
                Log.LogWarning("Hit UnauthorizedAccessException attempting to list info about agent directory.  There are likely files which cannot be cleaned up on the agent.");
                if (!string.IsNullOrEmpty(lastDirectoryChecked))
                {
                    Log.LogWarning($"Last directory checked : {lastDirectoryChecked} (likely the first inaccessible directory, alphabetically) ");
                }
            }
        }

        private Tuple<long, DateTime> GetDirectoryAttributes(string directory)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(directory);
            FileInfo[] fileInfos = directoryInfo.GetFiles();
            long totalSize = 0;
            DateTime lastModifiedDateTime = directoryInfo.LastWriteTime;
            foreach (FileInfo fileInfo in fileInfos)
            {
                totalSize += fileInfo.Length;
                lastModifiedDateTime = fileInfo.LastWriteTime > lastModifiedDateTime ? fileInfo.LastWriteTime : lastModifiedDateTime;
            }
            string[] directories = Directory.GetDirectories(directory);

            foreach (string dir in directories)
            {
                Tuple<long, DateTime> directoryAttributes = GetDirectoryAttributes(dir);
                totalSize += directoryAttributes.Item1;
                lastModifiedDateTime = directoryAttributes.Item2 > lastModifiedDateTime ? directoryAttributes.Item2 : lastModifiedDateTime;
            }
            return Tuple.Create(totalSize, lastModifiedDateTime);
        }

        private async System.Threading.Tasks.Task<bool> CleanupAgentsAsync()
        {
            bool returnStatus = true;
            DateTime now = DateTime.Now;

            // Cleanup the agents that the VSTS agent is tracking
            string[] sourceFolderJsons = Directory.GetFiles(Path.Combine(AgentDirectory, "_work", "SourceRootMapping"), "SourceFolder.json", SearchOption.AllDirectories);
            HashSet<string> knownDirectories = new HashSet<string>();
            List<System.Threading.Tasks.Task<bool>> cleanupTasks = new List<System.Threading.Tasks.Task<bool>>();

            Log.LogMessage($"Found {sourceFolderJsons.Length} known agent working directories. ");

            foreach (var sourceFolderJson in sourceFolderJsons)
            {
                Log.LogMessage($"Examining {sourceFolderJson} ...");

                Tuple<string, string, DateTime> agentInfo = await GetAgentInfoAsync(sourceFolderJson);
                string workDirectory = Path.Combine(AgentDirectory, "_work", agentInfo.Item2);
                knownDirectories.Add(workDirectory);

                TimeSpan span = new TimeSpan(now.Ticks - agentInfo.Item3.Ticks);

                if (span.TotalDays > RetentionDays)
                {
                    cleanupTasks.Add(CleanupAgentAsync(workDirectory, Path.GetDirectoryName(agentInfo.Item1)));
                }
                else
                {
                    Log.LogMessage($"Skipping cleanup for {sourceFolderJson}, it is newer than {RetentionDays} days old, last run date is '{agentInfo.Item3.ToString()}'");
                }
            }

            System.Threading.Tasks.Task.WaitAll(cleanupTasks.ToArray());
            foreach (var cleanupTask in cleanupTasks)
            {
                returnStatus &= cleanupTask.Result;
            }

            // Attempt to cleanup any working folders which the VSTS agent doesn't know about.
            Log.LogMessage("Looking for additional '_work' directories which are unknown to the agent.");
            cleanupTasks.Clear();
            Regex workingDirectoryRegex = new Regex(@"\\\d+$");
            var workingDirectories = Directory.GetDirectories(Path.Combine(AgentDirectory, "_work"), "*", SearchOption.TopDirectoryOnly).Where(w => workingDirectoryRegex.IsMatch(w));
            foreach (var workingDirectory in workingDirectories)
            {
                if (!knownDirectories.Contains(workingDirectory))
                {
                    cleanupTasks.Add(CleanupAgentDirectoryAsync(workingDirectory));
                }
            }
            System.Threading.Tasks.Task.WaitAll(cleanupTasks.ToArray());
            foreach (var cleanupTask in cleanupTasks)
            {
                returnStatus &= cleanupTask.Result;
            }
            return returnStatus;
        }

        private async System.Threading.Tasks.Task<bool> CleanupAgentAsync(string workDirectory, string sourceFolderJson)
        {
            bool returnStatus = await CleanupAgentDirectoryAsync(workDirectory);
            returnStatus &= await CleanupAgentDirectoryAsync(sourceFolderJson).ConfigureAwait(false);
            return returnStatus;
        }

        private async System.Threading.Tasks.Task<bool> CleanupAgentDirectoryAsync(string directory, int attempts = 0)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    bool tryAlternateDelete = false;
                    Console.Write($"Attempting to cleanup {directory} ... ");
                    try
                    {
                        Directory.Delete(directory, true);
                        Log.LogMessage("Success");
                    }
                    catch (PathTooLongException)
                    {
                        tryAlternateDelete = true;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // This may be being deleted by another thread, OR be > 260 chars
                        tryAlternateDelete = true;
                    }
                    if (tryAlternateDelete && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        Log.LogMessage($"Attempting to work around PathTooLongException by using robocopy and windows shell to delete {directory} ");
                        Process.Start("robocopy.exe", $"/mir {GetUniqueEmptyFolder()} {directory}").WaitForExit();
                    }
                }
                else
                {
                    Log.LogMessage($"Specified directory, {directory}, does not exist");
                }
                return true;
            }
            catch (Exception e)
            {
                attempts++;
                Log.LogMessage($"Failed in cleanup attempt... {Retries - attempts} retries left.");
                Log.LogMessage($"{e.GetType().ToString()} - {e.Message}");
                Log.LogMessage(e.StackTrace);
                if (attempts < Retries)
                {
                    Log.LogMessage($"Will retry again in {SleepTimeInMilliseconds} ms");
                    await System.Threading.Tasks.Task.Delay(SleepTimeInMilliseconds.Value);
                    return await CleanupAgentDirectoryAsync(directory, attempts).ConfigureAwait(false);
                }
            }
            Log.LogMessage("Failed to cleanup agent");
            return false;
        }

        private static string GetUniqueEmptyFolder()
        {
            string uniquePath;
            do
            {
                Guid guid = Guid.NewGuid();
                string uniqueSubFolderName = guid.ToString();
                uniquePath = Path.GetTempPath() + uniqueSubFolderName;
            }
            while (Directory.Exists(uniquePath));
            Directory.CreateDirectory(uniquePath);
            return uniquePath;
        }

        private async System.Threading.Tasks.Task<Tuple<string, string, DateTime>> GetAgentInfoAsync(string sourceFolderJson)
        {
            Regex getValueRegex = new Regex(".*\": \"(?<value>[^\"]+)\"");

            DateTime lastRunOn = DateTime.Now;
            string agentBuildDirectory = null;
            using (Stream stream = File.OpenRead(sourceFolderJson))
            using (StreamReader reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    string line = await reader.ReadLineAsync();
                    if (line.Contains("lastRunOn"))
                    {
                        lastRunOn = DateTime.Parse(getValueRegex.Match(line).Groups["value"].Value.ToString());
                    }
                    else if (line.Contains("agent_builddirectory"))
                    {
                        agentBuildDirectory = getValueRegex.Match(line).Groups["value"].Value.ToString();
                    }
                }
            }
            return new Tuple<string, string, DateTime>(sourceFolderJson, agentBuildDirectory, lastRunOn);
        }
    }
}
