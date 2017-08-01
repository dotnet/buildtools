// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Run a command and retry if the exit code is not 0.
    /// </summary>
    public class ExecWithRetriesForNuGetPush : ExecWithRetries
    {
        /// <summary>
        /// There's a very special failure scenario that we want to ignore.  That scenario is
        /// when NuGet hits a timeout on one "push" attempt, and then gets a "Forbidden" response
        /// because the package "already exists" on the next response.  This indicates that the
        /// timeout occurred, but the push was actually successful.
        ///
        /// To address the condition, I'm implementing a special "IgnoredErrorMessagesWithConditional"
        /// property that allows you to specify error messages which you want to ignore.  If you
        /// specify the "ConditionalErrorMessage" metadata on the Item, then the error message is
        /// only ignored if the "conditional" error message was detected in a previous (or current)
        /// Exec attempt.
        /// </summary>
        public ITaskItem[] IgnoredErrorMessagesWithConditional { get; set; }

        private CancellationTokenSource _cancelTokenSource = new CancellationTokenSource();

        private Exec _runningExec;

        public override bool Execute()
        {
            HashSet<string> activeIgnorableErrorMessages = new HashSet<string>();
            // Add any "Ignore" messages that don't have conditionals to our active list.
            if (IgnoredErrorMessagesWithConditional != null)
            {
                foreach(var message in IgnoredErrorMessagesWithConditional)
                {
                    string conditional = message.GetMetadata("ConditionalErrorMessage");
                    if (string.IsNullOrEmpty(conditional))
                    {
                        activeIgnorableErrorMessages.Add(message.ItemSpec);
                    }
                }
            }
            for (int i = 0; i < MaxAttempts; i++)
            {
                string attemptMessage = $"(attempt {i + 1}/{MaxAttempts})";
                _runningExec = new Exec
                {
                    BuildEngine = BuildEngine,
                    Command = Command,
                    LogStandardErrorAsError = false,
                    IgnoreExitCode = true,
                    ConsoleToMSBuild = true
                };
                if (!_runningExec.Execute())
                {
                    Log.LogError("Child Exec task failed to execute.");
                    break;
                }

                int exitCode = _runningExec.ExitCode;
                if (exitCode == 0)
                {
                    return true;
                }

                if (_runningExec.ConsoleOutput != null &&
                    IgnoredErrorMessagesWithConditional != null &&
                    _runningExec.ConsoleOutput.Length > 0)
                {
                    var consoleOutput = _runningExec.ConsoleOutput.Select(c => c.ItemSpec);
                    // If the console output contains a "conditional" message, add the item to the active list.
                    var conditionMessages = IgnoredErrorMessagesWithConditional.Where(m => consoleOutput.Any(n => n.Contains(m.GetMetadata("ConditionalErrorMessage"))));
                    foreach(var condition in conditionMessages)
                    {
                        activeIgnorableErrorMessages.Add(condition.ItemSpec);
                    }
                    // If an active "ignore" message is present in the console output, then return true instead of retrying.
                    foreach (var ignoreMessage in activeIgnorableErrorMessages)
                    {
                        if (consoleOutput.Any(c => c.Contains(ignoreMessage)))
                        {
                            Log.LogMessage(MessageImportance.High, $"Error detected, but error condition is valid, ignoring error \"{ignoreMessage}\"");
                            return true;
                        }
                    }
                }
                string message = $"Exec FAILED: exit code {exitCode} {attemptMessage}";

                if (i + 1 == MaxAttempts || _cancelTokenSource.IsCancellationRequested)
                {
                    Log.LogError(message);
                    break;
                }

                Log.LogMessage(MessageImportance.High, message);

                TimeSpan delay = TimeSpan.FromSeconds(
                    Math.Pow(RetryDelayBase, i) + RetryDelayConstant);

                Log.LogMessage(MessageImportance.High, $"Retrying after {delay}...");

                try
                {
                    Task.Delay(delay, _cancelTokenSource.Token).Wait();
                }
                catch (AggregateException e) when (e.InnerException is TaskCanceledException)
                {
                    break;
                }
            }
            return false;
        }
    }
}

