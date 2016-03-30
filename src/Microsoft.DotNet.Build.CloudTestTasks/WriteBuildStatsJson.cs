// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Newtonsoft.Json;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    /// <summary>
    /// Used to write build stats to a JSON file.
    /// </summary>
    public class WriteTestBuildStatsJson : Task
    {
        /// <summary>
        /// Helper class to be serialized to JSON.
        /// </summary>
        class TestBuildStatsJson
        {
            internal class FailedProject
            {
                public FailedProject(string name)
                {
                    ProjectName = name;
                }

                public string ProjectName { get; }
            }

            public TestBuildStatsJson(ITaskItem[] correlationIDs, string logUri, int builtCount, ITaskItem[] buildFailures, int testCount)
            {
                Aggregates = new List<string>();
                ProjectsFailed = new List<FailedProject>();

                foreach (ITaskItem correlationId in correlationIDs)
                    Aggregates.Add(correlationId.ItemSpec);

                foreach (ITaskItem buildFailure in buildFailures)
                    ProjectsFailed.Add(new FailedProject(buildFailure.ItemSpec));

                LogUri = logUri;
                ProjectsBuiltCount = builtCount;
                TestCount = testCount;
            }

            public List<string> Aggregates { get; }

            public string LogUri { get; }

            public int ProjectsBuiltCount { get; }

            public List<FailedProject> ProjectsFailed { get; }

            public string Stage { get { return "Build"; } }

            public int TestCount { get; }
        }

        /// <summary>
        /// The collection of correlation IDs to which these build stats apply.
        /// </summary>
        [Required]
        public ITaskItem[] CorrelationIds { get; set; }

        /// <summary>
        /// The URI of the build log.
        /// </summary>
        [Required]
        public string LogUri { get; set; }

        /// <summary>
        /// The name of the build stats JSON file to create.
        /// </summary>
        [Required]
        public string OutputFile { get; set; }

        /// <summary>
        /// The number of projects successfully built.
        /// </summary>
        [Required]
        public int ProjectsBuiltCount { get; set; }

        /// <summary>
        /// The collection of project names that failed to build.
        /// </summary>
        [Required]
        public ITaskItem[] ProjectsFailed { get; set; }

        /// <summary>
        /// The total number of tests across all test binaries.
        /// </summary>
        [Required]
        public int TestCount { get; set; }

        public override bool Execute()
        {
            using (FileStream fs = new FileStream(OutputFile, FileMode.Create, FileAccess.Read | FileAccess.Write | FileAccess.ReadWrite))
            using (StreamWriter streamWriter = new StreamWriter(fs))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(streamWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;

                TestBuildStatsJson buildStats = new TestBuildStatsJson(CorrelationIds, LogUri, ProjectsBuiltCount, ProjectsFailed, TestCount);

                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(jsonWriter, buildStats);
            }

            return true;
        }
    }
}
