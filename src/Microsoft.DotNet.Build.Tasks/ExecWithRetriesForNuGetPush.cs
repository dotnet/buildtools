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
    public class ExecWithRetriesForNuGetPush : Microsoft.Build.Utilities.Task, ICancelableTask
    {
        [Required]
        public string Command { get; set; }

        public int MaxAttempts { get; set; } = 5;

        /// <summary>
        /// Base, in seconds, raised to the power of the number of retries so far.
        /// </summary>
        public double RetryDelayBase { get; set; } = 6;

        /// <summary>
        /// A constant, in seconds, added to (base^retries) to find the delay before retrying.
        /// 
        /// The default is -1 to make the first retry instant, because ((base^0)-1) == 0.
        /// </summary>
        public double RetryDelayConstant { get; set; } = -1;

        /// <summary>
        /// The "IgnoredErrorMessagesWithConditional" item collection
        /// allows you to specify error messages which you want to ignore.  If you
        /// specify the "ConditionalErrorMessage" metadata on the Item, then the error message is
        /// only ignored if the "conditional" error message was detected in a previous (or current)
        /// Exec attempt.
        /// 
        /// Example: <IgnoredErrorMessagesWithConditional Include="publish failed" />
        ///            Specifying this item would tell the task to report success, even if "publish failed" is detected
        ///            in the Exec output
        ///
        ///            <IgnoredErrorMessagesWithConditional Include="Overwriting existing packages is forbidden according to the package retention settings for this feed.">
        ///               <ConditionalErrorMessage>Pushing took too long</ConditionalErrorMessage>
        ///            </IgnoredErrorMessagesWithConditional>
        ///            This tells the task to report success if "Overwriting existing packages is forbidden..." is detected
        ///            in the Exec output, but only if a previous Exec attempt failed and reported "Pushing took too long".
        /// </summary>
        public ITaskItem[] IgnoredErrorMessagesWithConditional { get; set; }

        private CancellationTokenSource _cancelTokenSource = new CancellationTokenSource();

        private Exec _runningExec;

        public void Cancel()
        {
            _runningExec?.Cancel();
            _cancelTokenSource.Cancel();
        }

        public override bool Execute()
        {
            HashSet<string> activeIgnorableErrorMessages = new HashSet<string>();
            // Add any "Ignore" messages that don't have conditionals to our active list.
            if (IgnoredErrorMessagesWithConditional != null)
            {
                foreach (var message in IgnoredErrorMessagesWithConditional)
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
                    var conditionMessages = IgnoredErrorMessagesWithConditional.Where(m => 
                                               consoleOutput.Any(n => 
                                                  n.Contains(m.GetMetadata("ConditionalErrorMessage"))));
                    foreach (var condition in conditionMessages)
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

