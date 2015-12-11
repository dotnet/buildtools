using Microsoft.Build.Framework;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public override bool Execute()
        {
            if (String.IsNullOrEmpty(PackageTargetFramework))
            {
                _log.LogMessage(LogImportance.Low, $"Skipping validation since PackageTargetFramework is not defined");
                return true;
            }

            NuGetFramework fx = null;
            try
            {
                fx = NuGetFramework.Parse(PackageTargetFramework);
            }
            catch(Exception ex)
            {
                _log.LogError($"Could not parse PackageTargetFramework {PackageTargetFramework}. {ex}");
                return false;
            }

            Version assemblyVersion = null;
            if (!Version.TryParse(AssemblyVersion, out assemblyVersion))
            {
                _log.LogError($"Could not parse AssemblyVersion {AssemblyVersion}.");
                return false;
            }

            if (fx.Framework != FrameworkConstants.FrameworkIdentifiers.NetPlatform)
            {
                _log.LogMessage(LogImportance.Low, $"Skipping validation since PackageTargetFramework {fx} is not {FrameworkConstants.FrameworkIdentifiers.NetPlatform}");
                return true;
            }

            _generations = Generations.Load(GenerationDefinitionsFile);

            Dictionary<string, string> candidateRefs = CandidateReferences.ToDictionary(r => r.GetMetadata("FileName"), r => r.GetMetadata("FullPath"));

            Version idealGeneration = _generations.DetermineGenerationFromSeeds(AssemblyName, assemblyVersion, _log) ?? new Version(0, 0, 0, 0);
            if (idealGeneration > fx.Version)
            {
                _log.LogError($"Assembly {AssemblyName}, Version={assemblyVersion} is generation {idealGeneration} based on the seed data in {GenerationDefinitionsFile} which is greater than project generation {fx.Version}.");
            }

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
                
                if (!File.Exists(path))
                {
                    _log.LogError($"Reference {path} does not exist.");
                    continue;
                }

                var dependencyGeneration = _generations.DetermineGenerationFromFile(path, _log, candidateRefs: candidateRefs);

                if (dependencyGeneration > fx.Version)
                {
                    _log.LogError($"Dependency {path} is generation {dependencyGeneration} which is greater than project generation {fx.Version}.");
                }
                
                if (dependencyGeneration > idealGeneration)
                {
                    idealGeneration = dependencyGeneration;
                }
            }

            if (fx.Version > idealGeneration)
            {
                _log.LogMessage(LogImportance.Low, $"Generation {fx.Version} is higher than the ideal miniumum {idealGeneration}.");
            }


            return !_log.HasLoggedErrors;
        }

    }
}
