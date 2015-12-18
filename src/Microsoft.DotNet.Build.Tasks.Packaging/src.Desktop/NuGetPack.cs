using Microsoft.Build.Framework;
using NuGet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class NuGetPack : PackagingTask
    {

        [Required]
        public ITaskItem[] Nuspecs
        {
            get;
            set;
        }

        [Required]
        public string OutputDirectory
        {
            get;
            set;
        }

        public string[] Properties
        {
            get;
            set;
        }

        public bool ExcludeEmptyDirectories
        {
            get;
            set;
        }

        public override bool Execute()
        {
            if (Nuspecs == null || Nuspecs.Length == 0)
            {
                Log.LogError("Nuspecs argument must be specified");
                return false;
            }

            if (String.IsNullOrEmpty(OutputDirectory))
            {
                Log.LogError("OuputDirectory argument must be specified");
                return false;
            }

            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }

            IPropertyProvider properties = null;

            if (Properties != null && Properties.Length > 0)
            {
                Dictionary<string, string> propertyDictionary = new Dictionary<string, string>();
                foreach (string property in Properties)
                {
                    var propertyPair = property.Split(new[] { '=' }, 2);

                    if (propertyPair.Length < 2)
                    {
                        Log.LogError($"Invalid property pair {property}.  Properties should be of the form name=value.");
                        continue;
                    }

                    propertyDictionary[propertyPair[0]] = propertyPair[1];
                }

                properties = new DictionaryPropertyProvider(propertyDictionary);
            }


            foreach (var nuspec in Nuspecs)
            {
                string nuspecPath = nuspec.GetMetadata("FullPath");

                if (!File.Exists(nuspecPath))
                {
                    Log.LogError($"Nuspec {nuspecPath} does not exist");
                    continue;
                }

                try
                {
                    PackageBuilder builder = new PackageBuilder(nuspecPath, properties, !ExcludeEmptyDirectories);

                    string id = builder.Id, version = builder.Version.ToString();

                    if (String.IsNullOrEmpty(id))
                    {
                        Log.LogError($"Nuspec {nuspecPath} does not contain a valid Id");
                        continue;
                    }

                    if (String.IsNullOrEmpty(version))
                    {
                        Log.LogError($"Nuspec {nuspecPath} does not contain a valid version");
                        continue;
                    }

                    string nupkgPath = Path.Combine(OutputDirectory, $"{id}.{version}.nupkg");

                    using (var fileStream = File.Create(nupkgPath))
                    {
                        builder.Save(fileStream);
                    }

                    Log.LogMessage($"Created '{nupkgPath}'");
                }
                catch (Exception e)
                {
                    Log.LogError($"Error when creating nuget package from {nuspecPath}. {e}");
                }
            }

            return !Log.HasLoggedErrors;
        }

        private class DictionaryPropertyProvider : IPropertyProvider
        {
            private readonly IDictionary<string, string> _properties;

            public DictionaryPropertyProvider(IDictionary<string, string> properties)
            {
                _properties = properties;
            }

            public dynamic GetPropertyValue(string propertyName)
            {
                string value;
                if (_properties.TryGetValue(propertyName, out value))
                {
                    return value;
                }
                return null;
            }
        }
    }
}
