using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Produces
{
    public class Produces : Task
    {
        internal static readonly string PackageExtension = ".nupkg";
        internal static readonly string SymbolsExtension = ".symbols";

        [Required]
        public string Folder { get; set; }
        [Required]
        public string OutputFile { get; set; }

        public override bool Execute()
        {
            JObject objectRoot = new JObject();
            foreach (var folder in Directory.GetDirectories(Folder))
            {
                var grouping = Path.GetFileName(folder);
                var artifacts = Directory.GetFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(f => f, f => Path.GetFileName(f));
                var clashes = artifacts.GroupBy(a => a.Value).Where(a => a.Count() > 1);
                if (clashes.Count() > 0)
                {
                    foreach (var clash in clashes)
                    {
                        Console.Error.WriteLine("Error: Artifact names must be unique per grouping ('{0}').  Artifact '{1}' found in '{2}'.", grouping, clash.Key, string.Join(",", clash.Select(c => c.Key).ToArray()));
                    }
                    return false;
                }
                var packages = new JProperty("packages",
                                             new JObject(artifacts.Keys.Where(a => Path.GetExtension(a).Equals(PackageExtension) && !a.Contains(SymbolsExtension)).Select(a => CreatePackageJProperty(a))));
                var symbolsPackages = new JProperty("symbols-packages",
                                                    new JObject(artifacts.Keys.Where(a => Path.GetExtension(a).Equals(PackageExtension) && a.Contains(SymbolsExtension)).Select(a => CreatePackageJProperty(a))));
                var nuget = new JProperty("nuget", new JArray() { new JObject(packages), new JObject(symbolsPackages) });

                JObject obj = new JObject();

                if ((packages != null && packages.First.HasValues) || (
                    (symbolsPackages != null && symbolsPackages.First.HasValues)))
                {
                    obj.Add(nuget);
                }

                var files = new JProperty("files", JArray.FromObject(artifacts.Values.Where(a => !Path.GetExtension(a).Equals(PackageExtension))));
                if (files != null && files.First.HasValues)
                {
                    obj.Add(files);
                }
                if (obj.Count > 0)
                {
                    objectRoot.Add(grouping, obj);
                }
            }
            if (objectRoot.Count == 0)
            {
                Console.WriteLine("Produces did not generate any output. No artifacts found at {0}, skipped writing {1}", Folder, OutputFile);
            }
            else
            {
                var data = JsonConvert.SerializeObject(objectRoot, Formatting.Indented);
                File.WriteAllText(OutputFile, data);
            }
            return true;
        }
        private JProperty CreatePackageJProperty(string nupkg)
        {
            PackageArchiveReader archive = new PackageArchiveReader(nupkg);
            var id = archive.GetIdentity();
            return new JProperty(id.Id, id.Version.ToString());
        }
    }
}
