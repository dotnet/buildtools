using Microsoft.Build.Framework;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;

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

        public double MaximumTimeInMinutes { get; set; } = 10;

        public int MaximumWorkspacesToClean { get; set; } = 8;

        public bool EnableLongPathRemoval { get; set; } = true;

        public int? SleepTimeInMilliseconds { get; set; }
        public ITaskItem[] ProcessNamesToKill { get; set; }

        private static readonly int s_DefaultRetries = 3;
        private static readonly int s_DefaultSleepTime = 2000;
        private DateTime _timerStarted;

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
            bool returnValue = false;
            Thread worker = new Thread(() => { returnValue = DoCleanupWork(); });
            worker.Start();

            //  We'll use this to make sure that we at least try to clean up processes we start before tearing down.
            _timerStarted = DateTime.Now;

            if (worker.Join((int)TimeSpan.FromMinutes(MaximumTimeInMinutes).TotalMilliseconds))
            {
                return returnValue;
            }
            else
            {
                Log.LogWarning($" Cleanup Task ran for maximum time ({MaximumTimeInMinutes} minutes), aborting");
                return false;
            }
        }

        private bool DoCleanupWork()
        {
            bool returnValue = true;

            if (Report)
            {
                ReportDiskUsage();
            }
            if (Clean)
            {
                returnValue = CleanupDirsAsync().Result;
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
                // Report disk usage for agent directory
                DriveInfo driveInfo = ReportCommonDiskUsage("Agent", AgentDirectory);

                var workingDirectories = Directory.GetDirectories(Path.Combine(AgentDirectory, "_work"));
                var totalWorkingDirectories = workingDirectories != null ? workingDirectories.Length : 0;

                Log.LogMessage($"    Total agent working directories: {totalWorkingDirectories}");

                if (totalWorkingDirectories > 0)
                {
                    int nameLength = 0;
                    foreach (string directoryName in workingDirectories)
                    {
                        nameLength = directoryName.Length > nameLength ? directoryName.Length : nameLength;
                    }
                    int sizeLength = string.Format("{0:N0}", driveInfo?.TotalSize).Length;
                    string columnFormat = "      {0,-" + nameLength.ToString() + "}  {1," + sizeLength.ToString() + ":N0}  {2}";
                    Log.LogMessage(string.Format(columnFormat, "Folder name", "Size (bytes)", "Last Modified DateTime"));
                    foreach (var workingDirectory in workingDirectories)
                    {
                        lastDirectoryChecked = workingDirectory;
                        Tuple<long, DateTime> directoryAttributes = GetDirectoryAttributes(workingDirectory);
                        Log.LogMessage(string.Format(columnFormat, workingDirectory, directoryAttributes.Item1, directoryAttributes.Item2));
                    }
                }

                // Report disk usage for TEMP directory
                ReportCommonDiskUsage("TEMP", GetTEMPDirectory());

                // Report disk usage for Nuget Cache directory
                List<string> nugetCacheDirs = GetNugetCacheDirectories();
                if (nugetCacheDirs.Count == 0)
                {
                    Log.LogMessage($"Disk usage report for Nuget cache directories is not available, because those directories do NOT exist.");
                }

                foreach (string nugetCacheDir in nugetCacheDirs)
                {
                    ReportCommonDiskUsage("Nuget cache", nugetCacheDir);
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

        private DriveInfo ReportCommonDiskUsage(string dirType, string directory)
        {
            try
            {
                if (String.IsNullOrEmpty(directory))
                {
                    Log.LogMessage($"Disk usage report for {dirType} directory is not available, because the directory does NOT exist.");
                    return null;
                }

                if (!Directory.Exists(directory))
                {
                    Log.LogMessage($"Disk usage report for {dirType} directory is not available, because the directory {directory} does NOT exist.");
                    return null;
                }

                string drive = Path.GetPathRoot(directory);
                if (String.IsNullOrEmpty(drive))
                {
                    Log.LogMessage($"Can't parse the drive correctly from directory {directory} because it's null or empty.");
                    return null;
                }

                DriveInfo driveInfo = new DriveInfo(drive);

                Log.LogMessage($"Disk usage report for {dirType} directory");
                Log.LogMessage($"  {dirType} directory: {directory}");
                Log.LogMessage($"  Drive letter: {drive}");
                Log.LogMessage($"  Total disk size: {string.Format("{0:N0}", driveInfo.TotalSize)} bytes");
                Log.LogMessage($"  Total disk free space: {string.Format("{0:N0}", driveInfo.TotalFreeSpace)} bytes");

                Log.LogMessage($"  {dirType} directory info");
                Log.LogMessage($"    Total size of {dirType} directory: {string.Format("{0:N0}", GetDirectoryAttributes(directory).Item1)} bytes");

                return driveInfo;
            }
            catch (PathTooLongException)
            {
                Log.LogWarning($"Hit PathTooLongException attempting to list info about directory {directory}.  There are likely files which cannot be cleaned up on the agent.");
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                Log.LogWarning($"Hit UnauthorizedAccessException attempting to list info about directory {directory}.  There are likely files which cannot be cleaned up on the agent.");
                return null;
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

        private async System.Threading.Tasks.Task<bool> CleanupDirsAsync()
        {
            bool returnStatus = true;
            DateTime now = DateTime.Now;

            // Cleanup the agents that the VSTS agent is tracking
            string[] sourceFolderJsons = Directory.GetFiles(Path.Combine(AgentDirectory, "_work", "SourceRootMapping"), "SourceFolder.json", SearchOption.AllDirectories);
            HashSet<string> knownDirectories = new HashSet<string>();
            List<System.Threading.Tasks.Task<bool>> cleanupTasks = new List<System.Threading.Tasks.Task<bool>>();

            Log.LogMessage($"Found {sourceFolderJsons.Length} known agent working directories.  (Will clean up to {MaximumWorkspacesToClean} of them)");

            foreach (var sourceFolderJson in sourceFolderJsons)
            {
                Log.LogMessage($"Examining {sourceFolderJson} ...");

                Tuple<string, string, DateTime> agentInfo = await GetAgentInfoAsync(sourceFolderJson);
                string workDirectory = Path.Combine(AgentDirectory, "_work", agentInfo.Item2);
                knownDirectories.Add(workDirectory);

                TimeSpan span = new TimeSpan(now.Ticks - agentInfo.Item3.Ticks);

                if (cleanupTasks.Count < MaximumWorkspacesToClean)
                {
                    if (span.TotalDays > RetentionDays)
                    {
                        cleanupTasks.Add(CleanupAgentAsync(workDirectory, Path.GetDirectoryName(agentInfo.Item1)));
                    }
                    else
                    {
                        Log.LogMessage($"Skipping cleanup for {sourceFolderJson}, it is newer than {RetentionDays} days old, last run date is '{agentInfo.Item3.ToString()}'");
                    }
                }
                else
                {
                    // We've taken enough cleanup tasks per the value of MaximumWorkspaces
                    break;
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
                    cleanupTasks.Add(CleanupDirectoryAsync(workingDirectory));
                }
            }
            System.Threading.Tasks.Task.WaitAll(cleanupTasks.ToArray());
            foreach (var cleanupTask in cleanupTasks)
            {
                returnStatus &= cleanupTask.Result;
            }

            // Cleanup the TEMP folder
            string tempDir = GetTEMPDirectory();
            Log.LogMessage($"Clean up the TEMP folder {tempDir}.");
            System.Threading.Tasks.Task.WaitAll(CleanupDirectoryAsync(tempDir));

            // Cleanup the Nuget Cache folders
            List<string> nugetCacheDirs = GetNugetCacheDirectories();
            Log.LogMessage($"Clean up the Nuget Cache folders.");

            if (nugetCacheDirs.Count == 0)
            {
                Log.LogMessage($"Not necessary to clean up Nuget cache directories, as they do NOT exist.");
                return returnStatus;
            }

            cleanupTasks.Clear();
            foreach (string nugetCacheDir in nugetCacheDirs)
                cleanupTasks.Add(CleanupDirectoryAsync(nugetCacheDir));
            System.Threading.Tasks.Task.WaitAll(cleanupTasks.ToArray());

            return returnStatus;
        }

        private async System.Threading.Tasks.Task<bool> CleanupAgentAsync(string workDirectory, string sourceFolderJson)
        {
            bool returnStatus = await CleanupDirectoryAsync(workDirectory);
            returnStatus &= await CleanupDirectoryAsync(sourceFolderJson).ConfigureAwait(false);
            return returnStatus;
        }

        private async System.Threading.Tasks.Task<bool> CleanupDirectoryAsync(string directory, int attempts = 0)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Log.LogMessage($"Attempting to cleanup {directory} ... ");

                    // Unlike OSX and Linux, Windows has a hard limit of 260 chars on paths.
                    // Some build definitions leave paths this long behind.  It's unusual, 
                    // but robocopy has been on Windows by default since XP and understands 
                    // how to stomp on long paths, so we'll use it to clean directories on Windows first.
                    if (EnableLongPathRemoval && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        // leave it 1 second to die if we have to kill it:
                        int maxTimeMilliseconds = (int) ((TimeSpan.FromMinutes(MaximumTimeInMinutes) - (DateTime.Now - _timerStarted)).TotalMilliseconds - 1000);
                        // And only start it if it has at least 1 second to run:
                        if (maxTimeMilliseconds > 1000)
                        {
                            Log.LogMessage($"Preventing PathTooLongException by using robocopy to delete {directory} ");
                            string emptyFolderToMirror = GetUniqueEmptyFolder();
                            Process roboProcess = Process.Start(new ProcessStartInfo("robocopy.exe", $"/mir {emptyFolderToMirror} {directory}  /NJH /NJS /NP") { UseShellExecute = false });
                            roboProcess.WaitForExit(maxTimeMilliseconds);

                            roboProcess.Refresh();
                            if (!roboProcess.HasExited)
                            {
                                Log.LogWarning($"RoboCopy process (PID: {roboProcess.Id}) did not exit in maximum allotted time ({maxTimeMilliseconds / 1000} sec), will try to kill");
                                roboProcess.Kill();
                            }

                            Directory.Delete(emptyFolderToMirror);
                        }
                    }

                    Directory.Delete(directory, true);
                    Log.LogMessage("Success");
                }
                else
                {
                    Log.LogMessage($"Specified directory, {directory}, does not exist");
                }
                return true;
            }
            catch (PathTooLongException)
            {
                // Theres no point in retrying for PathTooLong
                Log.LogMessage($"Failed in cleanup attempt due to PathTooLongException, quitting.");
                return false;
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
                    return await CleanupDirectoryAsync(directory, attempts).ConfigureAwait(false);
                }
            }
            Log.LogMessage("Failed to cleanup.");
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

        private string GetTEMPDirectory()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (DirExists(Environment.GetEnvironmentVariable("TEMP")))
                    return Environment.GetEnvironmentVariable("TEMP");
                else if (DirExists(Environment.GetEnvironmentVariable("TMP")))
                    return Environment.GetEnvironmentVariable("TMP");
                else
                {
                    Log.LogMessage("No TEMP dir found.");
                    return null;
                }
            }
            else
            {
                if (DirExists(Environment.GetEnvironmentVariable("TMPDIR")))
                    return Environment.GetEnvironmentVariable("TMPDIR");
                else if (DirExists(Environment.GetEnvironmentVariable("TMP")))
                    return Environment.GetEnvironmentVariable("TMP");
                else if (DirExists(Path.Combine(Environment.GetEnvironmentVariable("HOME"), "myagent/_work/_temp")))
                    return Path.Combine(Environment.GetEnvironmentVariable("HOME"), "myagent/_work/_temp");
                else
                {
                    Log.LogMessage("No TEMP dir found.");
                    return null;
                }
            }
        }

        private bool DirExists(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Log.LogMessage($"TEMP dir: {directory} does not exist.");
                return false;
            }
            return true;
        }

        private List<string> GetNugetCacheDirectories()
        {
            List<string> nugetCacheDirs = new List<string>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                AddDirToListIfExist(nugetCacheDirs, Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), "NuGet"));
                AddDirToListIfExist(nugetCacheDirs, Path.Combine(Environment.GetEnvironmentVariable("UserProfile"), ".nuget\\packages"));
            }
            else // OSX or Linux
            {
                AddDirToListIfExist(nugetCacheDirs, Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".local/share/NuGet"));
                AddDirToListIfExist(nugetCacheDirs, Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".nuget"));
            }
            return nugetCacheDirs;
        }

        private void AddDirToListIfExist(List<string> dirs, string directory)
        {
            if (Directory.Exists(directory))
            {
                dirs.Add(directory);
                Log.LogMessage($"Successfully add directory: {directory} to the list.");
            }
            else
            {
                Log.LogMessage($"Fail to add directory: {directory} to the list because it doesn't exist.");
            }
        }
    }
}
