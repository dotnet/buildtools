using Microsoft.Build.Framework;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public class FixNonExistentProjectDependencies : ValidateRestoredProjectDependencies
    {
        [Required]
        public string NuGetToolPath { get; set; }

        [Required]
        public ITaskItem[] NuGetSources { get; set; }

        protected override void HandleNonExistentDependency(
            string name,
            string version,
            IEnumerable<string> libraryVersionsRestored,
            string lockFilePath)
        {
            // If possible, choose a stable version.
            var requestedVersion = NuGetVersion.Parse(version);
            var restoredVersions = libraryVersionsRestored.Select(v => NuGetVersion.Parse(v));

            if (!requestedVersion.IsPrerelease)
            {
                Console.WriteLine(
                    "Can't find upgrade path for nonexistent stable dependency '{0}' '{1}'",
                    name,
                    version);
                return;
            }

            var matchingStableVersion = restoredVersions.FirstOrDefault(v =>
                !v.IsPrerelease &&
                v.Major == requestedVersion.Major &&
                v.Minor == requestedVersion.Minor &&
                v.Patch == requestedVersion.Patch);

            if (matchingStableVersion != null)
            {
                Log.LogWarning(
                    "For {0}, changing nonexistent version '{1}' to stable '{2}'",
                    name,
                    version,
                    matchingStableVersion.ToNormalizedString());
            }
            else
            {
                // Find a version of the same package on any source that matches the prerelease version.
                string sourceArgString = string.Join(" ", NuGetSources.Select(item => "-Source " + item.ItemSpec));

                NuGetVersion matchedVersion = null;

                using (var nugetList = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = NuGetToolPath,
                        Arguments = "list " + name + " -Prerelease -AllVersions -NonInteractive " + sourceArgString,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                    }
                })
                {
                    Log.LogMessage("Running NuGet list for package " + name);

                    nugetList.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                        {
                            return;
                        }

                        string[] parts = e.Data.Split(' ');

                        if (parts[0] == name)
                        {
                            var foundVersion = NuGetVersion.Parse(parts[1]);
                            if (foundVersion.Release == requestedVersion.Release)
                            {
                                matchedVersion = foundVersion;
                                // Quit NuGet list early if possible.
                                nugetList.Kill();
                            }
                        }
                    };

                    if (nugetList.Start())
                    {
                        nugetList.BeginOutputReadLine();
                        nugetList.WaitForExit();

                        if (matchedVersion == null)
                        {
                            Log.LogError("No package found with id {0} and prerelease '{1}'", name, version);
                        }
                        else
                        {
                            Log.LogWarning(
                                "For {0}, changing nonexistent version '{1}' to discovered prerelease '{2}'",
                                name,
                                version,
                                matchedVersion.ToNormalizedString());
                        }
                    }
                    else
                    {
                        Log.LogError("Could not start NuGet process to retrieve package versions.");
                    }
                }
            }
        }
    }
}
