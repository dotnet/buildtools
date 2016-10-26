// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using ILogger = NuGet.Common.ILogger;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Logging helper for Nuget tasks
    /// </summary>
    internal class NugetMsBuildLogger : ILogger
    {
        private readonly TaskLoggingHelper _taskLogging;

        public NugetMsBuildLogger(TaskLoggingHelper taskLogging)
        {
            _taskLogging = taskLogging;
        }

        public void LogDebug(string data)
        {
            _taskLogging.LogMessage(MessageImportance.Low, data);
        }

        public void LogError(string data)
        {
            _taskLogging.LogError(data);
        }

        public void LogErrorSummary(string data)
        {
            _taskLogging.LogMessage(MessageImportance.High, data);
        }

        public void LogInformation(string data)
        {
            _taskLogging.LogMessage(MessageImportance.Normal, data);
        }

        public void LogInformationSummary(string data)
        {
            LogInformation(data);
        }

        public void LogMinimal(string data)
        {
            _taskLogging.LogMessage(MessageImportance.High, data);
        }

        public void LogVerbose(string data)
        {
            _taskLogging.LogMessage(MessageImportance.Low, data);
        }

        public void LogWarning(string data)
        {
            _taskLogging.LogWarning(data);
        }
    }
}