﻿using System;
using System.Collections.Concurrent;
using System.IO;
using System.Xml.Linq;
using Xunit.Abstractions;

namespace Xunit.ConsoleClient
{
    public class StandardOutputVisitor : XmlTestExecutionVisitor
    {
        string assemblyFileName;
        readonly object consoleLock;
        readonly ConcurrentDictionary<string, ExecutionSummary> completionMessages;
        readonly string defaultDirectory;
        readonly bool showProgress;

        public StandardOutputVisitor(object consoleLock,
                                     string defaultDirectory,
                                     XElement assemblyElement,
                                     Func<bool> cancelThunk,
                                     ConcurrentDictionary<string, ExecutionSummary> completionMessages = null,
                                     bool showProgress = false)
            : base(assemblyElement, cancelThunk)
        {
            this.consoleLock = consoleLock;
            this.defaultDirectory = defaultDirectory;
            this.completionMessages = completionMessages;
            this.showProgress = showProgress;
        }

        protected override bool Visit(ITestAssemblyStarting assemblyStarting)
        {
            assemblyFileName = Path.GetFileName(assemblyStarting.TestAssembly.Assembly.AssemblyPath);

            lock (consoleLock)
                Console.WriteLine("Starting:    {0}", Path.GetFileNameWithoutExtension(assemblyFileName));

            return base.Visit(assemblyStarting);
        }

        protected override bool Visit(ITestAssemblyFinished assemblyFinished)
        {
            // Base class does computation of results, so call it first.
            var result = base.Visit(assemblyFinished);

            lock (consoleLock)
                Console.WriteLine("Finished:    {0}", Path.GetFileNameWithoutExtension(assemblyFileName));

            if (completionMessages != null)
                completionMessages.TryAdd(Path.GetFileNameWithoutExtension(assemblyFileName), new ExecutionSummary
                {
                    Total = assemblyFinished.TestsRun,
                    Failed = assemblyFinished.TestsFailed,
                    Skipped = assemblyFinished.TestsSkipped,
                    Time = assemblyFinished.ExecutionTime,
                    Errors = Errors
                });

            return result;
        }

        protected override bool Visit(ITestFailed testFailed)
        {
            lock (consoleLock)
            {
                // TODO: Thread-safe way to figure out the default foreground color

                Program.SetConsoleForegroundColor(ConsoleColor.Red);
                Console.Error.WriteLine("   {0} [FAIL]", XmlEscape(testFailed.Test.DisplayName));
                Program.SetConsoleForegroundColor(ConsoleColor.Gray);
                Console.Error.WriteLine("      {0}", ExceptionUtility.CombineMessages(testFailed).Replace(Environment.NewLine, Environment.NewLine + "      "));

                WriteStackTrace(ExceptionUtility.CombineStackTraces(testFailed));
            }

            return base.Visit(testFailed);
        }

        protected override bool Visit(ITestPassed testPassed)
        {
            return base.Visit(testPassed);
        }

        protected override bool Visit(ITestSkipped testSkipped)
        {
            lock (consoleLock)
            {
                // TODO: Thread-safe way to figure out the default foreground color
                Program.SetConsoleForegroundColor(ConsoleColor.Yellow);
                Console.Error.WriteLine("   {0} [SKIP]", XmlEscape(testSkipped.Test.DisplayName));
                Program.SetConsoleForegroundColor(ConsoleColor.Gray);
                Console.Error.WriteLine("      {0}", XmlEscape(testSkipped.Reason));
            }

            return base.Visit(testSkipped);
        }

        protected override bool Visit(ITestStarting testStarting)
        {
            if (showProgress)
            {
                lock (consoleLock)
                {
                    Console.WriteLine("   {0} [STARTING]", XmlEscape(testStarting.Test.DisplayName));
                }
            }
            return base.Visit(testStarting);
        }

        protected override bool Visit(ITestFinished testFinished)
        {
            if (showProgress)
            {
                lock (consoleLock)
                {
                    Console.WriteLine("   {0} [FINISHED] Time: {1}s", XmlEscape(testFinished.Test.DisplayName), testFinished.ExecutionTime);
                }
            }
            return base.Visit(testFinished);
        }

        protected override bool Visit(IErrorMessage error)
        {
            WriteError("FATAL", error);

            return base.Visit(error);
        }

        protected override bool Visit(ITestAssemblyCleanupFailure cleanupFailure)
        {
            WriteError(String.Format("Test Assembly Cleanup Failure ({0})", cleanupFailure.TestAssembly.Assembly.AssemblyPath), cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestCaseCleanupFailure cleanupFailure)
        {
            WriteError(String.Format("Test Case Cleanup Failure ({0})", cleanupFailure.TestCase.DisplayName), cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestClassCleanupFailure cleanupFailure)
        {
            WriteError(String.Format("Test Class Cleanup Failure ({0})", cleanupFailure.TestClass.Class.Name), cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestCollectionCleanupFailure cleanupFailure)
        {
            WriteError(String.Format("Test Collection Cleanup Failure ({0})", cleanupFailure.TestCollection.DisplayName), cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestCleanupFailure cleanupFailure)
        {
            WriteError(String.Format("Test Cleanup Failure ({0})", cleanupFailure.Test.DisplayName), cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestMethodCleanupFailure cleanupFailure)
        {
            WriteError(String.Format("Test Method Cleanup Failure ({0})", cleanupFailure.TestMethod.Method.Name), cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected void WriteError(string failureName, IFailureInformation failureInfo)
        {
            lock (consoleLock)
            {
                Program.SetConsoleForegroundColor(ConsoleColor.Red);
                Console.Error.WriteLine("   [{0}] {1}", failureName, XmlEscape(failureInfo.ExceptionTypes[0]));
                Program.SetConsoleForegroundColor(ConsoleColor.Gray);
                Console.Error.WriteLine("      {0}", XmlEscape(ExceptionUtility.CombineMessages(failureInfo)));

                WriteStackTrace(ExceptionUtility.CombineStackTraces(failureInfo));
            }
        }

        void WriteStackTrace(string stackTrace)
        {
            if (String.IsNullOrWhiteSpace(stackTrace))
                return;

            Program.SetConsoleForegroundColor(ConsoleColor.DarkGray);
            Console.Error.WriteLine("      Stack Trace:");

            Program.SetConsoleForegroundColor(ConsoleColor.Gray);
            foreach (var stackFrame in stackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
            {
                Console.Error.WriteLine("         {0}", StackFrameTransformer.TransformFrame(stackFrame, defaultDirectory));
            }
        }
    }
}