using Microsoft.Build.Framework;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks
{
    public class CleanupVSTSAgent : Microsoft.Build.Utilities.Task
    {
        [Required]
        public string AgentDirectory { get; set; }

        [Required]
        public double RetentionDays { get; set; }

        public int? Retries { get; set; }

        public int? SleepTimeInMilliseconds { get; set; }

        private static readonly int s_DefaultRetries = 5;
        private static readonly int s_DefaultSleepTime = 10000;

        public override bool Execute()
        {
            if(!Retries.HasValue)
            {
                Retries = s_DefaultRetries;
            }
            if(!SleepTimeInMilliseconds.HasValue)
            {
                SleepTimeInMilliseconds = s_DefaultSleepTime;
            }

            return CleanupAgentsAsync().Result;
        }
        private async System.Threading.Tasks.Task<bool> CleanupAgentsAsync()
        {
            bool returnStatus = true;
            DateTime now = DateTime.Now;

            // Cleanup the agents that the VSTS agent is tracking
            string[] sourceFolderJsons = Directory.GetFiles(Path.Combine(AgentDirectory, "_work", "SourceRootMapping"), "SourceFolder.json", SearchOption.AllDirectories);
            HashSet<string> knownDirectories = new HashSet<string>();
            List<System.Threading.Tasks.Task<bool>> cleanupTasks = new List<System.Threading.Tasks.Task<bool>>();

            Console.WriteLine($"Found {sourceFolderJsons.Length} known agent working directories. ");

            foreach (var sourceFolderJson in sourceFolderJsons)
            {
                Console.WriteLine($"Examining {sourceFolderJson} ...");

                Tuple<string, string, DateTime> agentInfo = GetAgentInfoAsync(sourceFolderJson).Result;
                string workDirectory = Path.Combine(AgentDirectory, "_work", agentInfo.Item2);
                knownDirectories.Add(workDirectory);

                TimeSpan span = new TimeSpan(now.Ticks - agentInfo.Item3.Ticks);

                if (span.TotalDays > RetentionDays)
                {
                    cleanupTasks.Add(CleanupAgentAsync(workDirectory, Path.GetDirectoryName(agentInfo.Item1)));
                }
                else
                {
                    Console.WriteLine($"Skipping cleanup for {sourceFolderJson}, it is newer than {RetentionDays} days old, last run date is '{agentInfo.Item3.ToString()}'");
                }
            }

            System.Threading.Tasks.Task.WaitAll(cleanupTasks.ToArray());
            foreach(var cleanupTask in cleanupTasks)
            {
                returnStatus &= cleanupTask.Result;
            }

            // Attempt to cleanup any working folders which the VSTS agent doesn't know about.
            Console.WriteLine("Looking for additional '_work' directories which are unknown to the agent.");
            Regex workingDirectoryRegex = new Regex(@"\\\d+$");
            var workingDirectories = Directory.GetDirectories(Path.Combine(AgentDirectory, "_work"), "*", SearchOption.TopDirectoryOnly).Where(w => workingDirectoryRegex.IsMatch(w));
            foreach (var workingDirectory in workingDirectories)
            {
                if (!knownDirectories.Contains(workingDirectory))
                {
                    returnStatus &= await CleanupAgentDirectoryAsync(workingDirectory, 0).ConfigureAwait(false);
                }
            }
            return returnStatus;
        }

        private async System.Threading.Tasks.Task<bool> CleanupAgentAsync(string workDirectory, string sourceFolderJson)
        {
            bool returnStatus = await CleanupAgentDirectoryAsync(workDirectory, 0);
            returnStatus &= await CleanupAgentDirectoryAsync(sourceFolderJson, 0).ConfigureAwait(false);
            return returnStatus;
        }

        private async System.Threading.Tasks.Task<bool> CleanupAgentDirectoryAsync(string directory, int attempts)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Console.Write($"Attempting to cleanup {directory} ... ");
                    Directory.Delete(directory, true);
                    Console.WriteLine("Success");
                }
                else
                {
                    Console.WriteLine($"Specified directory, {directory}, does not exist");
                }
                return true;
            }
            catch (Exception e)
            {
                attempts++;
                Console.WriteLine($"Failed in cleanup attempt... {Retries - attempts} retries left.");
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                if(attempts < Retries)
                {
                    Console.WriteLine($"Will retry again in {SleepTimeInMilliseconds} ms");
                    await System.Threading.Tasks.Task.Delay(SleepTimeInMilliseconds.Value);
                    return await CleanupAgentDirectoryAsync(directory, attempts).ConfigureAwait(false);
                }
            }
            Console.WriteLine("Failed to cleanup agent");
            return false;
        }

        private async System.Threading.Tasks.Task<Tuple<string, string, DateTime>> GetAgentInfoAsync(string sourceFolderJson)
        {
            Regex getValueRegex = new Regex(".*\": \"(?<value>[^\"]+)\"");

            DateTime lastRunOn = DateTime.Now;
            string agentBuildDirectory = null;
            using (Stream stream = File.OpenRead(sourceFolderJson))
            using (StreamReader reader = new StreamReader(stream))
            {
                while(!reader.EndOfStream)
                {
                    string line = await reader.ReadLineAsync();
                    if(line.Contains("lastRunOn"))
                    {
                        lastRunOn = DateTime.Parse(getValueRegex.Match(line).Groups["value"].Value.ToString());
                    }
                    else if(line.Contains("agent_builddirectory"))
                    {
                        agentBuildDirectory = getValueRegex.Match(line).Groups["value"].Value.ToString();
                    }
                }
            }
            return new Tuple<string, string, DateTime>(sourceFolderJson, agentBuildDirectory, lastRunOn);
        }
    }
}
