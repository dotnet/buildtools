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
        private Generations _generations;

        [Required]
        public string GenerationDefinitionsFile { get; set; }
        
        public string PackageTargetFramework { get; set; }

        [Required]
        public ITaskItem[] References { get; set; }

        [Required]
        public string[] ReferencePaths
        {
            get;
            set;
        }

        public override bool Execute()
        {
            if (String.IsNullOrEmpty(PackageTargetFramework))
            {
                _log.LogMessage(LogImportance.Low, $"Skipping validation since PackageTargetFramework is not defined");
                return true;
            }

            NuGetFramework fx = NuGetFramework.Parse(PackageTargetFramework);

            if (fx.Framework != FrameworkConstants.FrameworkIdentifiers.NetPlatform)
            {
                _log.LogMessage(LogImportance.Low, $"Skipping validation since PackageTargetFramework {fx} is not {FrameworkConstants.FrameworkIdentifiers.NetPlatform}");
                return true;
            }

            LoadGenerations();

            foreach(var reference in References)
            {
                string path = reference.GetMetadata("FullPath");
                
                if (!File.Exists(path))
                {
                    _log.LogError($"Reference {path} does not exist.");
                    continue;
                }

                var dependencyGeneration = _generations.DetermineGeneration(path, _log);

                if (dependencyGeneration > fx.Version)
                {
                    _log.LogError($"Dependency {path} is generation {dependencyGeneration} which is greater than project generation {fx.Version}.");
                }
            }

            return !_log.HasLoggedErrors;
        }


        private void LoadGenerations()
        {
            _generations = Generations.Load(GenerationDefinitionsFile);

            _generations.ReferencePaths = ReferencePaths;
        }

    }
}
