// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
                GlobalProperties = args.GlobalProperties;
                ProjectFile = args.ProjectFile;
                ContextId = args.BuildEventContext.ProjectContextId;
                ParentContextId = args.ParentProjectBuildEventContext.ProjectContextId;
                TargetPath = (string)args.Properties?.Cast<DictionaryEntry>().FirstOrDefault(p => ((string)p.Key).Equals("TargetPath")).Value;
            }

            public bool RanBuild { get; set; }
            public IDictionary<string, string> GlobalProperties { get; }
            public string TargetPath { get; }
            public string ProjectFile { get; }
            public int ContextId { get; }
            public int ParentContextId { get; }
        }

        /// <summary>
        /// All projects evaluations that built, indexed by context ID.
        /// </summary>
        private Dictionary<int, ProjectState> _projectHistory = new Dictionary<int, ProjectState>();
        private string _logFile = null;
        private StreamWriter _fileWriter = null;

        private bool _append = false;
        private bool _exceptionOnError = true;
        private bool _outputToStdErr = true;
        private bool _ignoreNonExistentTargetPaths = true;


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
            eventSource.ProjectStarted += ProjectStarted;
            eventSource.TargetStarted += TargetStarted;

            ParseParameters();

            if (_logFile != null)
            {
                _fileWriter = new StreamWriter(new FileStream(_logFile, _append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read | FileShare.Delete, 4096, FileOptions.SequentialScan));
            }
        }

        private void ParseParameters()
        {
            if (!String.IsNullOrEmpty(Parameters))
            {
                string[] parameterPairs = Parameters.Split(';');
                foreach(string parameterPair in parameterPairs)
                {
                    if (parameterPair.Length > 0)
                    {
                        string[] parameterNameValue = parameterPair.Split('=');

                        ApplyParameter(parameterNameValue[0], parameterNameValue.Length > 1 ? parameterNameValue[1] : null);
                    }
                }
            }
        }

        private void ApplyParameter(string name, string value)
        {
            switch(name.ToLower())
            {
                case "logfile":
                    _logFile = value;
                    break;
                case "exceptiononerror":
                    _exceptionOnError = Boolean.Parse(value);
                    break;
                case "outputtostderr":
                    _outputToStdErr = Boolean.Parse(value);
                    break;
                case "ignorenonexistenttargetpaths":
                    _ignoreNonExistentTargetPaths = Boolean.Parse(value);
                    break;
                case "append":
                    _append = Boolean.Parse(value);
                    break;
                default:
                    // ignore unrecognized parameters
                    break;
            }
        }

        private void ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            var state = new ProjectState(e);
            _projectHistory.Add(e.BuildEventContext.ProjectContextId, state);
        }

        private void TargetStarted(object sender, TargetStartedEventArgs e)
        {
            // we only care about the build target
            if (e.TargetName == "Build")
            {
                var state = _projectHistory[e.BuildEventContext.ProjectContextId];
                state.RanBuild = true;
            }
        }

        public void Shutdown()
        {
            int clashes = 0;
            Dictionary<string, ProjectState> clashMap = new Dictionary<string, ProjectState>();
            foreach (var state in _projectHistory.Values.Where(s => s.RanBuild && !String.IsNullOrEmpty(s.TargetPath)))
            {
                if (_ignoreNonExistentTargetPaths && !File.Exists(state.TargetPath))
                {
                    continue;
                }

                ProjectState clashingProject = null;
                if (!clashMap.TryGetValue(state.TargetPath, out clashingProject))
                {
                    clashMap[state.TargetPath] = state;
                }
                else
                {
                    StringBuilder errorMessage = new StringBuilder($"Error : Multiple projects built twice with the same target path {state.TargetPath}.");
                    errorMessage.AppendLine();
                    errorMessage.AppendLine(GetProjectStack(state));
                    errorMessage.AppendLine(GetProjectStack(clashingProject));
                    errorMessage.AppendLine();
                    WriteError(errorMessage.ToString());
                    clashes++;
                }
            }

            if (_fileWriter != null)
            {
                _fileWriter.Dispose();
            }

            if (_exceptionOnError && (clashes != 0))
            {
                throw new Exception($"{clashes} bin clashes were detected during the build.");
            }
        }

        private static void FormatProject(StringBuilder builder, ProjectState project, bool isReference)
        {
            builder.Append(project.ProjectFile);

            if (isReference)
            {
                builder.AppendLine(" -->");
            }
            else
            {
                builder.AppendLine();
            }

            if (project.GlobalProperties.Any())
            {
                builder.AppendLine("  Global Properties:");

                foreach (var property in project.GlobalProperties)
                {
                    builder.AppendLine($"    {property.Key} = {property.Value}");
                }
            }

            return;
        }

        private string GetProjectStack(ProjectState state)
        {
            StringBuilder builder = new StringBuilder();
            Stack<ProjectState> projectStack = new Stack<ProjectState>();
            projectStack.Push(state);

            for (var contextId = state.ParentContextId; contextId > -1; contextId = state.ParentContextId)
            {
                state = _projectHistory[contextId];
                projectStack.Push(state);
            }

            while (projectStack.Count != 0)
            {
                FormatProject(builder, projectStack.Pop(), isReference: projectStack.Count != 0);
            }

            return builder.ToString();
        }

        private void WriteError(string errorText)
        {
            if (_fileWriter != null)
            {
                _fileWriter.Write(errorText);
            }

            if (_outputToStdErr)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.Write(errorText);
                Console.ResetColor();
            }
        }

    }
}
