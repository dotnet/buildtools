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
    public class CleanupVSTSAgent : BuildTask
    {
        public bool Clean { get; set; }

        public bool Report { get; set; }

        [Required]
        public string AgentDirectory { get; set; }

        [Required]
        public double RetentionDays { get; set; }

        public int? Retries { get; set; }

        public int MaximumWorkspacesToClean { get; set; } = 8;

        public bool EnableLongPathRemoval { get; set; } = true;

        public int? SleepTimeInMilliseconds { get; set; }

        public ITaskItem[] ProcessNamesToKill { get; set; }

        public string [] AdditionalCleanupDirectories { get; set; }

        private static readonly int s_DefaultRetries = 3;
        private static readonly int s_DefaultSleepTime = 2000;
        private DateTime _timerStarted;

        private HashSet<string> _protectedDirectories = new HashSet<string>();
        private HashSet<string> _additionalCleanupDirectories = new HashSet<string>();
        private Dictionary<string, bool> _availableUnixCommands = new Dictionary<string, bool>();

        public override bool Execute()
        {
            string entryLocation = System.Reflection.Assembly.GetEntryAssembly().Location;
            if(!string.IsNullOrEmpty(entryLocation))
            {
                _protectedDirectories.Add(entryLocation);
            }
            string currentDirectory = Directory.GetCurrentDirectory();
            if (!string.IsNullOrEmpty(currentDirectory))
            {
                _protectedDirectories.Add(currentDirectory);
            }

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
            _timerStarted = DateTime.Now;

            GenerateAdditionalCleanupDirectories();
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

        private void GenerateAdditionalCleanupDirectories()
        {
            if (AdditionalCleanupDirectories != null)
            {
                _additionalCleanupDirectories = new HashSet<string>(AdditionalCleanupDirectories);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _additionalCleanupDirectories.Add(Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), "NuGet"));
                _additionalCleanupDirectories.Add(Path.Combine(Environment.GetEnvironmentVariable("UserProfile"), ".nuget\\packages"));
                _additionalCleanupDirectories.Add(Environment.GetEnvironmentVariable("TEMP"));
                _additionalCleanupDirectories.Add(Environment.GetEnvironmentVariable("TMP"));
            }
            else
            {
                _additionalCleanupDirectories.Add(Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".local/share/NuGet"));
                _additionalCleanupDirectories.Add(Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".nuget"));
                _additionalCleanupDirectories.Add(Environment.GetEnvironmentVariable("TMPDIR"));
                _additionalCleanupDirectories.Add(Environment.GetEnvironmentVariable("TMP"));
                _additionalCleanupDirectories.Add(Path.Combine(Environment.GetEnvironmentVariable("HOME"), "myagent/_work/_temp"));
            }
            _additionalCleanupDirectories.RemoveWhere(f => string.IsNullOrWhiteSpace(f));
            _additionalCleanupDirectories.RemoveWhere(f => !Directory.Exists(f));

            foreach(string additionalCleanupDirectory in _additionalCleanupDirectories)
            {
                Log.LogMessage($"Found additional cleanup directory {additionalCleanupDirectory}");
            }
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
                ReportCommonDiskUsage("Agent", AgentDirectory);

                foreach (string additionalCleanupDirectory in _additionalCleanupDirectories)
                {
                    ReportCommonDiskUsage("Cleanup", additionalCleanupDirectory);
                }
            }
            catch (PathTooLongException)
            {
                Log.LogWarning("Hit PathTooLongException attempting to list info about agent directory.  Ensure you are running the framework version of this task and 'EnableLongPathRemoval' is 'true'..");
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
                Log.LogWarning($"Hit PathTooLongException attempting to list info about directory {directory}.  Ensure you are running the framework version of this task and 'EnableLongPathRemoval' is 'true'.");
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
            int cleanupTaskCount = 0;
            List<System.Threading.Tasks.Task<bool>> cleanupTasks = new List<System.Threading.Tasks.Task<bool>>();

            // Cleanup the agents that the VSTS agent is tracking
            if (Directory.Exists(AgentDirectory))
            {
                GiveFullPermissionsIfUnixFolder(AgentDirectory);
                string[] sourceFolderJsons = Directory.GetFiles(AgentDirectory, "SourceFolder.json", SearchOption.AllDirectories);
                HashSet<string> knownDirectories = new HashSet<string>();

                Log.LogMessage($"Found {sourceFolderJsons.Length} known agent working directories.  (Will clean up to {MaximumWorkspacesToClean} of them)");

                string workDirectoryRoot = Directory.GetDirectories(AgentDirectory, "_work", SearchOption.AllDirectories).FirstOrDefault();

                foreach (var sourceFolderJson in sourceFolderJsons)
                {
                    Log.LogMessage($"Examining {sourceFolderJson} ...");

                    Tuple<string, string, DateTime> agentInfo = await GetAgentInfoAsync(sourceFolderJson);
                    string workDirectory = Path.Combine(workDirectoryRoot, agentInfo.Item2);
                    knownDirectories.Add(workDirectory);

                    TimeSpan span = new TimeSpan(now.Ticks - agentInfo.Item3.Ticks);
                    if (cleanupTaskCount < MaximumWorkspacesToClean)
                    {
                        if (span.TotalDays > RetentionDays)
                        {
                            cleanupTasks.Add(CleanupAgentAsync(workDirectory, Path.GetDirectoryName(agentInfo.Item1)));
                            cleanupTaskCount++;
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
                if (cleanupTaskCount < MaximumWorkspacesToClean)
                {
                    Regex workingDirectoryRegex = new Regex(@"[/\\]\d+$");
                    var workingDirectories = Directory.GetDirectories(workDirectoryRoot, "*", SearchOption.TopDirectoryOnly).Where(w => workingDirectoryRegex.IsMatch(w));
                    foreach (var workingDirectory in workingDirectories)
                    {
                        if (cleanupTaskCount >= MaximumWorkspacesToClean)
                        {
                            break;
                        }
                        if (!knownDirectories.Contains(workingDirectory))
                        {
                            cleanupTasks.Add(CleanupDirectoryAsync(workingDirectory));
                            cleanupTaskCount++;
                        }
                    }
                }
                System.Threading.Tasks.Task.WaitAll(cleanupTasks.ToArray());
                foreach (var cleanupTask in cleanupTasks)
                {
                    returnStatus &= cleanupTask.Result;
                }
            }
            else
            {
                Log.LogMessage($"Agent directory not found at '{AgentDirectory}', skipping agent cleanup.");
            }


            cleanupTasks.Clear();
            foreach (string additionalCleanupDirectory in _additionalCleanupDirectories)
            {
                cleanupTasks.Add(CleanupDirectoryAsync(additionalCleanupDirectory, 0, true));
            }
            System.Threading.Tasks.Task.WaitAll(cleanupTasks.ToArray());

            return returnStatus;
        }

        private async System.Threading.Tasks.Task<bool> CleanupAgentAsync(string workDirectory, string sourceFolderJson)
        {
            bool returnStatus = await CleanupDirectoryAsync(workDirectory);
            returnStatus &= await CleanupDirectoryAsync(sourceFolderJson).ConfigureAwait(false);
            return returnStatus;
        }

        private async System.Threading.Tasks.Task<bool> CleanupDirectoryAsync(string directory, int attempts = 0, bool ignoreExceptions = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return true;
                }
                // A protected directory is, for instance, the directory which is currently running the cleanup task.
                // The cleanup task should never try to clean itself up
                foreach (string protectedDirectory in _protectedDirectories)
                {
                    if (protectedDirectory.Contains(directory) || directory.Contains(protectedDirectory))
                    {
                        Console.WriteLine($"Specified cleanup directory ('{directory}') is a protected directory ('{protectedDirectory}'), skipping.");
                        return true;
                    }
                }
                if (!Directory.Exists(directory))
                {
                    Log.LogMessage($"Specified directory, {directory}, does not exist");
                }
                else
                {
                    GiveFullPermissionsIfUnixFolder(directory);

                    Log.LogMessage($"Attempting to cleanup {directory} ... ");
#if net45
                    // Unlike OSX and Linux, Windows has a hard limit of 260 chars on paths.
                    // Some build definitions leave paths this long behind.  It's unusual, 
                    // but robocopy has been on Windows by default since XP and understands 
                    // how to stomp on long paths, so we'll use it to clean directories on Windows first.
                    if (EnableLongPathRemoval && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        RemoveDirectoryRecursiveLongPath(directory);
                    }
                    else
                    {
#endif
                        Directory.Delete(directory, true);
#if net45
                    }
#endif
                    Log.LogMessage("Success");
                }
                return true;
            }
            catch (Exception e)
            {
                if (ignoreExceptions)
                {
                    Log.LogMessage($"Failed in cleanup attempt of '{directory}', but directory is non-blocking, ignoring failure.");
                    Log.LogMessage($"{e.GetType().ToString()} - {e.Message}");
                    return true;
                }
                else
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
            }
            Log.LogMessage("Failed to cleanup.");
            return false;
        }

        private void GiveFullPermissionsIfUnixFolder(string directory)
        {
            // Change file permissions on directory we're attempting to delete so we don't hit "Permission denied"
            if (IsUnixCommandAvailable("sudo") &&
               IsUnixCommandAvailable("chmod"))
            {
                string chmodPermissions = "777";
                Log.LogMessage($"Changing file permissions to '{chmodPermissions}' for directory '{directory}'");
                int maximumChmodTimeInMinutes = 3;
                int maxTimeMilliseconds = (int)((TimeSpan.FromMinutes(maximumChmodTimeInMinutes) - (DateTime.Now - _timerStarted)).TotalMilliseconds - 1000);
                Process chmodProcess = Process.Start(new ProcessStartInfo("sudo", $"chmod {chmodPermissions} -R {directory}"));
                chmodProcess.WaitForExit(maxTimeMilliseconds);
                chmodProcess.Refresh();
                if (!chmodProcess.HasExited)
                {
                    Log.LogWarning($"Chmod process (PID: {chmodProcess.Id} did not exit in maximum allotted time ({maximumChmodTimeInMinutes} mins), will try to kill");
                    chmodProcess.Kill();
                }
            }
        }

        // Keep a dictionary of Unix commands which we check for availability so that we have quick access to 
        // determine if the command has already been checked and if it is available.
        private bool IsUnixCommandAvailable(string command)
        {
            if(_availableUnixCommands.ContainsKey(command))
            {
                return _availableUnixCommands[command];
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _availableUnixCommands.Add(command, false);
            }
            else
            {
                Process commandProcess = Process.Start(new ProcessStartInfo("which", command));
                commandProcess.WaitForExit();
                _availableUnixCommands.Add(command, commandProcess.ExitCode == 0);
            }
            return _availableUnixCommands[command];
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
#if net45
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool DeleteFileW([MarshalAs(UnmanagedType.LPWStr)]string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RemoveDirectory(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool FindClose(IntPtr hFindFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetFileAttributesW(string lpFileName, FileAttributes dwFileAttributes);

        // The CharSet must match the CharSet of the corresponding PInvoke signature
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        // Many Windows API's will use the unicode syntax when the path is prepended with '\\?\', this
        // is instead of the ANSI paths which have a 260 character max path limit
        private string MakeLongPath(string path)
        {
            if (path == null ||
                path.Length == 0)
            {
                return path;
            }
            if (!path.StartsWith(@"\\?\"))
            {
                path = @"\\?\" + path;
            }
            return path;
        }

        private bool RemoveDirectoryRecursiveLongPath(string directory)
        {
            string[] directories = GetDirectoriesLongPath(directory);

            // Windows DeleteFile API will only delete a directory if it is empty,
            // so recurse down to the leaf directory 
            foreach (string dir in directories)
            {
                RemoveDirectoryRecursiveLongPath(dir);
            }

            string[] files = GetFilesLongPath(directory);
            foreach (string file in files)
            {
                // Clear read-only attribute
                SetFileAttributesW(file, FileAttributes.Normal);

                bool deleted = DeleteFileW(MakeLongPath(file));
                if (!deleted)
                {
                    ThrowIfWindowsErrorCode($"Failed to delete file '{file}'");
                }
            }
            directories = GetDirectoriesLongPath(directory);
            foreach (string dir in directories)
            {
                RemoveDirectoryLongPath(MakeLongPath(dir));
            }

            RemoveDirectoryLongPath(directory);
            return true;
        }

        private string[] GetDirectoriesLongPath(string directory)
        {
            return GetFilesByAttributes(directory, FileAttributes.Directory);
        }

        private string[] GetFilesLongPath(string directory)
        {
            return GetFilesByAttributes(directory, FileAttributes.Normal | FileAttributes.Archive | FileAttributes.NotContentIndexed);
        }

        // Given a directory, return an array of files in that directory with the specified attributes.  Supports long paths.
        private string[] GetFilesByAttributes(string directory, FileAttributes attributes)
        {
            directory = MakeLongPath(directory);
            List<string> files = new List<string>();
            WIN32_FIND_DATA findData;

            IntPtr hFile = FindFirstFile(directory + "\\*", out findData);
            int error = Marshal.GetLastWin32Error();

            if (hFile.ToInt32() != -1)
            {
                do
                {
                    string file = findData.cFileName;
                    if ((findData.dwFileAttributes & (int)attributes) != 0)
                    {
                        if (file != "." && file != "..")
                        {
                            files.Add(Path.Combine(directory, file));
                        }
                    }
                }
                while (FindNextFile(hFile, out findData));

                FindClose(hFile);
            }
            return files.ToArray();
        }

        private void RemoveDirectoryLongPath(string directory)
        {
            bool deleted = RemoveDirectory(MakeLongPath(directory));
            if (!deleted)
            {
                ThrowIfWindowsErrorCode($"Failed to remove directory '{directory}'");
            }
        }

        private void ThrowIfWindowsErrorCode(string message)
        {
            int lastError = Marshal.GetLastWin32Error();
            if(lastError != 0)
            {
                // Not trying to translate all of the error codes possibilities into readable text, just report the code
                throw new Exception($"{message}: error={lastError}, error code values are described at https://msdn.microsoft.com/en-us/library/windows/desktop/ms681382(v=vs.85).aspx");
            }
        }
#endif
    }
}
