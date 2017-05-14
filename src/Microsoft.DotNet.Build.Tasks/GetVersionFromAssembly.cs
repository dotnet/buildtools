using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GetVersionFromAssembly : Task
    {
        [Required]
        public string PathToAssembly { get; set; }

        [Output]
        public string AssemblyVersion { get; set; }

        public override bool Execute()
        {
            if (!File.Exists(PathToAssembly))
            {
                Log.LogError($"{PathToAssembly} does not exist.");
                return false;
            }
            try
            {
                Regex regex = new Regex(@"Commit Hash: ([A-Za-z0-9].*)");
                string productVersion = FileVersionInfo.GetVersionInfo(PathToAssembly).ProductVersion;
                Match match = regex.Match(productVersion);
                if (match.Success && match.Groups.Count > 1)
                {
                    AssemblyVersion = match.Groups[1].Value;
                }
                else
                {
                    Log.LogError($"{PathToAssembly} has an invalid ProductVersion or does not contain a commit hash.");
                    return false;
                }
            } catch (Exception)
            {
                Log.LogError($"{PathToAssembly} is invalid or has no ProductVersion defined.");
                throw;
            }
            return true;
        }
    }
}
