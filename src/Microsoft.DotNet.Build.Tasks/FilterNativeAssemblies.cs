using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.DotNet.Build.Tasks
{
    public class FilterNativeAssemblies : Task
    {
        /// <summary>
        /// An item group of assemblies to be checked whether they are managed or native
        /// </summary>
        [Required]
        public ITaskItem[] Assemblies { get; set; }

        /// <summary>
        /// An item group containing only managed assemblies
        /// </summary>
        [Output]
        public ITaskItem[] ManagedAssemblies { get; set; }

        public override bool Execute()
        {
            var assemblies = new List<ITaskItem>();

            foreach (ITaskItem assembly in Assemblies)
            {
                try
                {
                    using (var filestream = new FileStream(assembly.ItemSpec, FileMode.Open, FileAccess.Read))
                    using (PEReader peReader = new PEReader(filestream))
                    {
                        if (peReader.HasMetadata)
                        {
                            MetadataReader reader = peReader.GetMetadataReader();
                            if (reader.IsAssembly)
                            {
                                assemblies.Add(assembly);
                            }
                        }
                        else
                        {
                            //Native
                            continue;
                        }
                    }
                }
                catch (BadImageFormatException)
                {
                    //Unknown
                    continue;
                }
            }

            ManagedAssemblies = assemblies.ToArray();
            return !Log.HasLoggedErrors;
        }
    }
}
