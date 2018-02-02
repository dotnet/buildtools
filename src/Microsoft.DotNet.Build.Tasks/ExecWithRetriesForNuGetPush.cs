﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.DotNet.VersionTools.Automation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Run a command and retry if the exit code is not 0.
    /// </summary>
    public class ExecWithRetriesForNuGetPush : BuildTask
    {
        private const int DownloadComparisonBufferBytes = 1024 * 1024;

        [Required]
        public string Command { get; set; }

        /// <summary>
        /// Package file that is pushed by the given command. Only used if an
        /// IgnoredErrorMessagesWithConditional item is specified with ConditionalIfV2FeedIdentical
        /// set and the conditionally ignored error is found.
        /// </summary>
        public string PackageFile { get; set; }

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
        /// allows you to specify error messages which you want to ignore.
        /// 
        /// %(Identity): The error message text to ignore.
        /// %(ConditionalErrorMessage) (optional): If you specify this metadata on the Item, then
        ///   the error message is only ignored if the "conditional" error message was detected in
        ///   a previous (or current) Exec attempt.
        /// %(ConditionalIdenticalOnFeed) (optional): If this specifies a v2 feed endpoint, for
        ///   example "https://dotnet.myget.org/F/dotnet-core/api/v2", the error message is only
        ///   ignored if the feed contains the package and it's identical to the one being pushed.
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

        private bool? _packageFileIdentical;

        public void Cancel()
        {
            _runningExec?.Cancel();
            _cancelTokenSource.Cancel();
        }

        public override bool Execute()
        {
            var activeIgnorableErrorMessages = new HashSet<ITaskItem>();
            // Add any "Ignore" messages that don't have conditionals to our active list.
            if (IgnoredErrorMessagesWithConditional != null)
            {
                foreach (var message in IgnoredErrorMessagesWithConditional)
                {
                    string conditional = message.GetMetadata("ConditionalErrorMessage");
                    if (string.IsNullOrEmpty(conditional))
                    {
                        activeIgnorableErrorMessages.Add(message);
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
                        activeIgnorableErrorMessages.Add(condition);
                    }
                    // If an active "ignore" message is present in the console output, then return true instead of retrying.
                    foreach (var ignoreMessage in activeIgnorableErrorMessages)
                    {
                        if (consoleOutput.Any(c => c.Contains(ignoreMessage.ItemSpec)) &&
                            CheckNuGetV2FeedIdenticalCondition(ignoreMessage))
                        {
                            Log.LogMessage(MessageImportance.High, $"Error detected, but error condition is valid, ignoring error \"{ignoreMessage.ItemSpec}\"");
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

        private bool CheckNuGetV2FeedIdenticalCondition(ITaskItem ignore)
        {
            string v2Feed = ignore.GetMetadata("ConditionalIdenticalOnFeed");
            if (string.IsNullOrEmpty(v2Feed))
            {
                return true;
            }

            if (!_packageFileIdentical.HasValue)
            {
                var packageInfo = new NupkgInfo(PackageFile);
                string packageUrl = $"{v2Feed}/package/{packageInfo.Id}/{packageInfo.Version}";

                Log.LogMessage(
                    MessageImportance.High,
                    $"Downloading package from '{packageUrl}' " +
                    $"to check if identical to '{PackageFile}'");

                byte[] localBytes = File.ReadAllBytes(PackageFile);
                byte[] remoteBytes;

                using (var client = new HttpClient())
                using (var response = client.GetAsync(packageUrl).Result)
                {
                    remoteBytes = response.Content.ReadAsByteArrayAsync().Result;
                }

                _packageFileIdentical = localBytes.SequenceEqual(remoteBytes);
            }

            Log.LogMessage(
                MessageImportance.High,
                $"Package '{PackageFile}' identical: {_packageFileIdentical}");

            return _packageFileIdentical.Value;
        }
    }
}

