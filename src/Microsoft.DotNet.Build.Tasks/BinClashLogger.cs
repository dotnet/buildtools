// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks
{
    public class BinClashLogger : ILogger
    {
        internal class ProjectState
        {

            public ProjectState(ProjectStartedEventArgs args)
            {
                this.GlobalProperties = args.GlobalProperties;
                this.ProjectFile = args.ProjectFile;
                this.ContextId = args.BuildEventContext.ProjectContextId;
                this.ParentContextId = args.ParentProjectBuildEventContext.ProjectContextId;
                this.TargetPath = (string)args.Properties?.Cast<DictionaryEntry>().FirstOrDefault(p => ((string)p.Key).Equals("TargetPath")).Value;
            }

            public bool RanBuild { get; set; }
            public IDictionary<string, string> GlobalProperties { get; }
            public string TargetPath { get; }
            public string ProjectFile { get; }
            public int ContextId { get; }
            public int ParentContextId { get; }

            public string FormatProject(bool reference)
            {
                StringBuilder builder = new StringBuilder(ProjectFile);
                if (reference)
                {
                    builder.AppendLine(" -->");
                }
                else
                {
                    builder.AppendLine();
                }

                if (GlobalProperties.Any())
                {
                    builder.AppendLine("  Global Properties:");

                    foreach (var property in GlobalProperties)
                    {
                        builder.AppendLine($"    {property.Key} = {property.Value}");
                    }
                }

                return builder.ToString();
            }

        }

        /// <summary>
        /// All projects evaluations that built, indexed by context ID.
        /// </summary>
        Dictionary<int, ProjectState> projectHistory = new Dictionary<int, ProjectState>();

        /// <summary>
        /// This logger observes project builds to find projects that build more than once
        /// with the same output path but different global properties.
        /// </summary>
        public BinClashLogger()
        {
            // by default MSBuild will not pass properties during multi-node builds, configure it to pass TargetPath
            Environment.SetEnvironmentVariable("MSBuildForwardPropertiesFromChild", "TargetPath");
        }

        public string Parameters { get; set; }
        public LoggerVerbosity Verbosity { get; set; }

        public void Initialize(IEventSource eventSource)
        {
            eventSource.ProjectStarted += EventSource_ProjectStarted;
            eventSource.TargetStarted += EventSource_TargetStarted;
        }

        private void EventSource_ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            var state = new ProjectState(e);
            projectHistory.Add(e.BuildEventContext.ProjectContextId, state);

        }

        private void EventSource_TargetStarted(object sender, TargetStartedEventArgs e)
        {
            // we only care about the build target
            if (e.TargetName == "Build")
            {
                var state = projectHistory[e.BuildEventContext.ProjectContextId];
                state.RanBuild = true;

            }
        }

        public void Shutdown()
        {
            bool failed = false;
            Dictionary<string, ProjectState> clashMap = new Dictionary<string, ProjectState>();
            foreach (var state in projectHistory.Values.Where(s => s.RanBuild && !String.IsNullOrEmpty(s.TargetPath)))
            {
                ProjectState clashingProject = null;
                if (!clashMap.TryGetValue(state.TargetPath, out clashingProject))
                {
                    clashMap[state.TargetPath] = state;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"Error : Multiple projects built twice with the same target path {state.TargetPath}.");
                    Console.Error.Write(GetProjectStack(state));
                    Console.Error.WriteLine();
                    Console.Error.Write(GetProjectStack(clashingProject));
                    Console.ResetColor();
                    failed = true;
                }
            }

            if (failed)
            {
                throw new Exception();
            }
        }

        private string GetProjectStack(ProjectState state)
        {
            StringBuilder builder = new StringBuilder();
            Stack<ProjectState> projectStack = new Stack<ProjectState>();
            projectStack.Push(state);

            for (var contextId = state.ParentContextId; contextId > -1; contextId = state.ParentContextId)
            {
                state = projectHistory[contextId];
                projectStack.Push(state);
            }

            while (projectStack.Count != 0)
            {
                builder.Append(projectStack.Pop().FormatProject(projectStack.Count != 0));
            }

            return builder.ToString();
        }

    }
}
