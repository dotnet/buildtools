using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GenerateAssemblyList : BuildTask
    {
        [Required]
        public string InputListLocation { get; set; }

        [Required]
        public string OutputListLocation { get; set; }

        public override bool Execute()
        {
            HashSet<string> processedFileNames = new HashSet<string>();
            AssemblyList corerun = new AssemblyList("Microsoft.NETCore.Runtime", "Microsoft.NETCore.TestHost", "Microsoft.NETCore.Windows.ApiSets",  "Microsoft.NETCore.Jit");
            AssemblyList xunit = new AssemblyList("xunit");
            List<string> unmatched = new List<string>();
            Stream inputListLocationStream = File.OpenRead(InputListLocation);
            Stream outputListLocationStream = new FileStream(OutputListLocation, FileMode.Create, FileAccess.Write, FileShare.None);

            using (StreamReader listReader = new StreamReader(inputListLocationStream))
            {
                string line;
                while ((line = listReader.ReadLine()) != null)
                {
                    var fileName = Path.GetFileName(line);

                    if (corerun.TryAdd(line))
                    {
                        continue;
                    }

                    if (xunit.TryAdd(line))
                    {
                        continue;
                    }

                    unmatched.Add(line);
                }
            }

            Dictionary<string, List<string>> headers = new Dictionary<string, List<string>>
            {
                {"corerun", corerun.Dependencies},
                {"xunit", xunit.Dependencies},
                {"testdependency", unmatched},
            };

            using (StreamWriter listRewriter = new StreamWriter(outputListLocationStream))
            using (JsonWriter jsonWriter = new JsonTextWriter(listRewriter))
            {
                jsonWriter.Formatting = Formatting.Indented;
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(jsonWriter, headers);
            }

            return true;
        }

        private class AssemblyList
        {
            public List<string> Dependencies { get; private set; }
            private readonly List<string> _patternToContain;

            public AssemblyList(params string[] patterns)
            {
                _patternToContain = new List<string>(patterns);
                Dependencies = new List<string>();
            }

            public bool TryAdd(string packageId)
            {
                if (_patternToContain.Any(packageId.Contains))
                {
                    Dependencies.Add(packageId);
                    return true;
                }
                return false;
            }
        }
    }
}
