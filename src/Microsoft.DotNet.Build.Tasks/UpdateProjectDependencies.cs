using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public abstract class UpdateProjectDependencies : Task
    {
        [Required]
        public ITaskItem[] ProjectJsons { get; set; }

        private static JObject ReadProject(string projectJsonPath)
        {
            using (TextReader projectFileReader = File.OpenText(projectJsonPath))
            {
                var projectJsonReader = new JsonTextReader(projectFileReader);

                var serializer = new JsonSerializer();
                return serializer.Deserialize<JObject>(projectJsonReader);
            }
        }

        private static void WriteProject(JObject projectRoot, string projectJsonPath)
        {
            string projectJson = JsonConvert.SerializeObject(projectRoot, Formatting.Indented);

            File.WriteAllText(projectJsonPath, projectJson + Environment.NewLine);
        }

        public abstract bool UpdatePackage(JProperty package, string projectJsonPath);

        public override bool Execute()
        {
            foreach (var projectJsonPath in ProjectJsons.Select(item => item.ItemSpec))
            {
                JObject projectRoot = ReadProject(projectJsonPath);

                bool changedAnyPackage = projectRoot["dependencies"]
                    .OfType<JProperty>()
                    .Any(package => UpdatePackage(package, projectJsonPath));

                if (changedAnyPackage)
                {
                    WriteProject(projectRoot, projectJsonPath);
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
