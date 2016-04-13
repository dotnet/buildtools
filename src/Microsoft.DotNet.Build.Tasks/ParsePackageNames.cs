using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System;

namespace Microsoft.DotNet.Build.Tasks
{
    public class ParsePackageNames : Task
    {
        [Required]
        public string [] PackageDrops { get; set; }

        [Required]
        public string PackageNameRegex { get; set; }

        [Output]
        public ITaskItem[] PackageNames { get; set; }

        public override bool Execute()
        {
            Regex packageNameRegex = new Regex(PackageNameRegex);
            List<ITaskItem> packageNameItems = new List<ITaskItem>();

            foreach (string packageDrop in PackageDrops)
            {
                if (!Directory.Exists(packageDrop))
                {
                    Log.LogWarning("PackageDrop does not exist - '{0}'", packageDrop);
                    continue;
                }
                IEnumerable<ITaskItem> packages = Directory.GetFiles(packageDrop).Select(f => new TaskItem(Path.GetFileNameWithoutExtension(f)));


                foreach (ITaskItem package in packages)
                {
                    Match m = packageNameRegex.Match(package.ItemSpec);
                    if (m.Success)
                    {
                        TaskItem packageName = new TaskItem(m.Groups[0].Value);
                        packageName.SetMetadata("PackageName", m.Groups[1].Value);
                        packageName.SetMetadata("PackageVersion", m.Groups[2].Value);
                        packageName.SetMetadata("PrereleaseVersion", m.Groups[3].Value);
                        packageNameItems.Add(packageName);
                    }
                }
            }
            PackageNames = packageNameItems?.OrderBy(an => an.ItemSpec.ToString(), StringComparer.Ordinal)?.ToArray();
            return true;
        }
    }
}
