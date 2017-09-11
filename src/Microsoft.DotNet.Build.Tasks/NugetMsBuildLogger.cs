// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Common;
using ILogger = NuGet.Common.ILogger;
using Task = System.Threading.Tasks.Task;

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

        public void Log(LogLevel level, string data)
        {
            LogAsync(level, data).Wait();
        }

        public Task LogAsync(LogLevel level, string data)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    LogDebug(data);
                    break;
                case LogLevel.Verbose:
                    LogVerbose(data);
                    break;
                case LogLevel.Information:
                    LogInformation(data);
                    break;
                case LogLevel.Minimal:
                    LogMinimal(data);
                    break;
                case LogLevel.Warning:
                    LogWarning(data);
                    break;
                case LogLevel.Error:
                    LogError(data);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
            return Task.CompletedTask;
        }

        public void Log(ILogMessage message)
        {
            LogAsync(message).Wait();
        }

        public async Task LogAsync(ILogMessage message)
        {
            await LogAsync(message.Level, message.Message);
        }
    }
}