// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Resources;

namespace Microsoft.DotNet.Build.Tasks
{
    public class CreateExternalAssembliesResources : Task
    {
        [Required]
        public ITaskItem[] InputAssemblies { get; set; }

        [Required]
        public string OutputPath { get; set; }

        public override bool Execute()
        {
            try
            {
                if (!Directory.Exists(OutputPath))
                {
                    Directory.CreateDirectory(OutputPath);
                }

                CreateReswFiles();
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, showStackTrace: false);
            }

            return !Log.HasLoggedErrors;
        }

        public void CreateReswFiles()
        {
            foreach (ITaskItem assemblySpec in InputAssemblies)
            {
                string assemblyPath = assemblySpec.ItemSpec;
                try
                {
                    Assembly assembly = Assembly.LoadFrom(assemblyPath);
                    foreach (string resourceName in assembly.GetManifestResourceNames())
                    {
                        if (!resourceName.EndsWith(".resources"))
                        {
                            continue; // we only need to get the resources strings to produce the resw files.
                        }

                        string reswName = Path.GetFileNameWithoutExtension(resourceName);
                        string reswPath = Path.Combine(OutputPath, $"{reswName}.resw");
                        using (FileStream stream = File.Create(reswPath))
                        using (ResourceReader resourceReader = new ResourceReader(assembly.GetManifestResourceStream(resourceName)))
                        using (ResXResourceWriter resourceWriter = new ResXResourceWriter(stream))
                        {
                            IDictionaryEnumerator enumerator = resourceReader.GetEnumerator();
                            while (enumerator.MoveNext())
                            {
                                resourceWriter.AddResource(enumerator.Key.ToString(), enumerator.Value.ToString());
                            }
                        }
                    }
                }
                catch (BadImageFormatException)
                {
                    continue; // native assemblies can't be loaded.
                }
            }
        }
    }
}