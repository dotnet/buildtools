// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class ValidatePackageTargetFramework : PackagingTask
    {
        private static readonly HashSet<string> designTimeFacades = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mscorlib",
            "System.Core",
            "System",
            "System.Net",
            "System.Numerics",
            "System.Runtime.Serialization",
            "System.Windows",
            "System.Xml",
            "System.Xml.Linq",
            "System.Xml.Serialization",
            "System.ComponentModel.DataAnnotations",
            "System.ServiceModel",
            "System.ServiceModel.Web"
        };

        private Generations _generations;

        [Required]
        public string AssemblyName { get; set; }

        [Required]
        public string AssemblyVersion { get; set; }

        [Required]
        public string GenerationDefinitionsFile { get; set; }
        
        public string PackageTargetFramework { get; set; }

        [Required]
        public ITaskItem[] DirectReferences { get; set; }

        [Required]
        public ITaskItem[] CandidateReferences { get; set; }

        public ITaskItem[] IgnoredReferences { get; set; }

        public bool UseNetPlatform { get; set; }

        public override bool Execute()
        {
            if (String.IsNullOrEmpty(PackageTargetFramework))
            {
                Log.LogMessage(LogImportance.Low, $"Skipping validation since PackageTargetFramework is not defined");
                return true;
            }

            NuGetFramework fx = null;
            try
            {
                fx = NuGetFramework.Parse(PackageTargetFramework);
            }
            catch(Exception ex)
            {
                Log.LogError($"Could not parse PackageTargetFramework {PackageTargetFramework}. {ex}");
                return false;
            }

            Version assemblyVersion = null;
            if (!Version.TryParse(AssemblyVersion, out assemblyVersion))
            {
                Log.LogError($"Could not parse AssemblyVersion {AssemblyVersion}.");
                return false;
            }

            if (UseNetPlatform)
            {
                if (fx.Framework == FrameworkConstants.FrameworkIdentifiers.NetStandard)
                {
                    Log.LogError($"Validating for legacy 'dotnet' moniker but package targets new 'netstandard' moniker.");
                    return false;
                }
                else if (fx.Framework != FrameworkConstants.FrameworkIdentifiers.NetPlatform)
                {
                    Log.LogMessage(LogImportance.Low, $"Skipping validation since PackageTargetFramework {fx} is not {FrameworkConstants.FrameworkIdentifiers.NetPlatform}");
                    return true;
                }
            }
            else
            {
                if (fx.Framework == FrameworkConstants.FrameworkIdentifiers.NetPlatform)
                {
                    if (fx.Version > new Version(5, 0))
                    {
                        NuGetFramework netstandardFx = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetStandard, new Version(fx.Version.Major - 4, fx.Version.Minor - 1));
                        Log.LogError($"{fx.GetShortFolderName()} is no longer supported please update to {netstandardFx.GetShortFolderName()}.  Validating as {netstandardFx.GetShortFolderName()}.");
                        // update to netstandard so that we can get the right number of errors
                        fx = netstandardFx;
                    }
                    else
                    {
                        Log.LogError($"Invalid framework version {fx.GetShortFolderName()} please update to appropriate netstandard version.");
                        // update to nestandard so that we'll do the actual calculation
                        fx = FrameworkConstants.CommonFrameworks.NetStandard;
                    }
                }


                if (fx.Framework != FrameworkConstants.FrameworkIdentifiers.NetStandard)
                {
                    Log.LogMessage(LogImportance.Low, $"Skipping validation since PackageTargetFramework {fx} is not {FrameworkConstants.FrameworkIdentifiers.NetStandard}");
                    return true;
                }
            }

            _generations = Generations.Load(GenerationDefinitionsFile, UseNetPlatform);

            Dictionary<string, string> candidateRefs = CandidateReferences.ToDictionary(r => r.GetMetadata("FileName"), r => r.GetMetadata("FullPath"));

            Version idealGeneration = _generations.DetermineGenerationFromSeeds(AssemblyName, assemblyVersion, Log) ?? new Version(0, 0, 0, 0);
            if (idealGeneration > fx.Version)
            {
                Log.LogError($"Assembly {AssemblyName}, Version={assemblyVersion} is generation {idealGeneration} based on the seed data in {GenerationDefinitionsFile} which is greater than project generation {fx.Version}.");
            }

            HashSet<string> ignoredRefs = null;

            if (IgnoredReferences != null)
            {
                ignoredRefs = new HashSet<string>(IgnoredReferences.Select(ir => ir.ItemSpec), StringComparer.OrdinalIgnoreCase);
            }

            Version defaultGeneration = UseNetPlatform ? FrameworkConstants.CommonFrameworks.DotNet.Version : FrameworkConstants.CommonFrameworks.NetStandard.Version;

            foreach (var reference in DirectReferences)
            {
                string path = reference.GetMetadata("FullPath");

                // workaround issue where portable targeting pack design time facades
                // include dangling refs and refs to higher versions of contracts than 
                // exist in the targeting pack.
                if (path.IndexOf(".NETPortable", StringComparison.OrdinalIgnoreCase) != -1 &&
                    designTimeFacades.Contains(Path.GetFileNameWithoutExtension(path)))
                {
                    continue;
                }

                if (ignoredRefs != null && ignoredRefs.Contains(Path.GetFileNameWithoutExtension(path)))
                {
                    continue;
                }
                
                if (!File.Exists(path))
                {
                    Log.LogError($"Reference {path} does not exist.");
                    continue;
                }

                var dependencyGeneration = _generations.DetermineGenerationFromFile(path, Log, candidateRefs: candidateRefs, ignoredRefs: ignoredRefs) ?? defaultGeneration;

                if (dependencyGeneration > fx.Version)
                {
                    Log.LogError($"Dependency {path} is generation {dependencyGeneration} which is greater than project generation {fx.Version}.");
                }
                
                if (dependencyGeneration > idealGeneration)
                {
                    idealGeneration = dependencyGeneration;
                }
            }

            if (fx.Version > idealGeneration)
            {
                Log.LogMessage(LogImportance.Low, $"Generation {fx.Version} is higher than the ideal miniumum {idealGeneration}.");
            }


            return !Log.HasLoggedErrors;
        }

    }
}
