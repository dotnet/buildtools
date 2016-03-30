// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public sealed class GetPerfTestAssemblies : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// An item group of test binaries to inspect for performance tests.
        /// </summary>
        [Required]
        public ITaskItem[] TestBinaries { get; set; }

        public bool GetFullPaths { get; set; }

        /// <summary>
        /// An item group containing performance test binaries.  Can be empty if no performance tests were found.
        /// </summary>
        [Output]
        public ITaskItem[] PerfTestAssemblies { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "About to inspect {0} test assemblies.", TestBinaries.Length);
            var perfTests = new List<ITaskItem>();

            foreach (var testBinary in TestBinaries)
            {
                Log.LogMessage(MessageImportance.Low, "Inspecting assembly {0}.", testBinary.ItemSpec);

                using (var stream = File.OpenRead(testBinary.ItemSpec))
                {
                    using (var peFile = new PEReader(stream))
                    {
                        if(!peFile.HasMetadata){
                            continue;
                        }
                        var mdReader = peFile.GetMetadataReader();

                        foreach (var asmRefHandle in mdReader.AssemblyReferences)
                        {
                            var asmRef = mdReader.GetAssemblyReference(asmRefHandle);
                            var asmRefName = mdReader.GetString(asmRef.Name);

                            // if an assembly contains a reference to xunit.performance.core
                            // then it contains at least one performance test.

                            if (string.Compare(asmRefName, "xunit.performance.core", StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                var fileName = (GetFullPaths) ? Path.GetFullPath(testBinary.ItemSpec) : Path.GetFileNameWithoutExtension(testBinary.ItemSpec);
                                perfTests.Add(new TaskItem(fileName));
                                Log.LogMessage("+ Assembly {0} contains one or more performance tests.", fileName);
                                break;
                            }
                        }
                    }
                }
            }

            if (perfTests.Count > 0)
            {
                PerfTestAssemblies = perfTests.ToArray();
                Log.LogMessage(MessageImportance.High, "Found {0} assemblies containing performance tests.", perfTests.Count);
            }
            else
            {
                Log.LogWarning("Didn't find any performance tests.");
            }

            return true;
        }
    }
}
