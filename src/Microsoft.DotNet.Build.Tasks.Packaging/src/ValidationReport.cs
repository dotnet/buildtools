using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class ValidationReport
    {
        public ValidationReport()
        {
            Targets = new Dictionary<string, Target>();
        }

        public string Id { get; set; }
        public string Version { get; set; }

        public Dictionary<string, string> SupportedFrameworks { get; set; }
        public Dictionary<string,Target> Targets { get; set; }

        public void Save(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var file = File.CreateText(path))
            {
                var serializer = new JsonSerializer();
                serializer.StringEscapeHandling = StringEscapeHandling.EscapeNonAscii;
                serializer.Formatting = Formatting.Indented;
                serializer.Serialize(file, this);
            }
        }

        public static ValidationReport Load(string path)
        {
            using (var file = File.OpenText(path))
            using (var jsonTextReader = new JsonTextReader(file))
            {
                var serializer = new JsonSerializer();
                return serializer.Deserialize<ValidationReport>(jsonTextReader);
            }
        }
    }

    public class Target
    {
        public string Framework { get; set; }
        public string RuntimeID { get; set; }

        public PackageAsset[] CompileAssets { get; set; }
        public PackageAsset[] RuntimeAssets { get; set; }
    }

    public class PackageAsset
    {
        public string LocalPath { get; set; }
        public string PackagePath { get; set; }
        public string SourceProject { get; set; }
    }
}
