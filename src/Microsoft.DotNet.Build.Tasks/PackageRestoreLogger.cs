// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks
{
    public class PackageRestoreLogger : ILogger
    {
        public LoggerVerbosity Verbosity { get; set; }
        public string Parameters { get; set; }

        private HashSet<string> _packageSet = new HashSet<string>();
        private string _logFile;
        private bool _outputToConsole = true;
        private Func<string, bool> _packageUrlCheck = x => x.Contains("OK") || x.Contains("CACHE");

        public void Initialize(IEventSource eventSource)
        {
            ParseParameters();
            eventSource.MessageRaised += eventSource_MessageHandler;
        }

        private void ParseParameters()
        {
            if (!String.IsNullOrEmpty(Parameters))
            {
                string[] parameterPairs = Parameters.Split(';');
                foreach (string parameterPair in parameterPairs)
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
            switch (name.ToLower())
            {
                case "logfile":
                    _logFile = value;
                    break;
                case "logtoconsole":
                    _outputToConsole = Boolean.Parse(value);
                    break;
                default:
                    // ignore unrecognized parameters
                    break;
            }
        }

        private void eventSource_MessageHandler(object sender, BuildMessageEventArgs e)
        {
            if (_packageUrlCheck(e.Message))
            {
                _packageSet.Add(e.Message.Trim());
            }
        }

        private void Log(string message, StreamWriter logWriter)
        {
            if(logWriter != null)
            {
                logWriter.WriteLine(message);
            }
            if (_outputToConsole)
            {
                Console.WriteLine(message);
            }
        }

        public void Shutdown()
        {
            StreamWriter logWriter = null;
            if (!string.IsNullOrEmpty(_logFile))
            {
                try
                {
                    logWriter = new StreamWriter(new FileStream(_logFile, FileMode.Create, FileAccess.Write, FileShare.Read | FileShare.Delete, 4096, FileOptions.SequentialScan));
                }
                catch (IOException e)
                {
                    throw new InvalidOperationException("PackageRestoreLogger: Unable to create log file", e);
                }
            }
            HashSet<string> feedSet = new HashSet<string>();
            Log("PackageRestoreLogger Results:", logWriter);
            foreach (var package in _packageSet)
            {
                Log(package, logWriter);
            }
            if (logWriter != null)
            {
                logWriter.Dispose();
            }
        }
    }
}
