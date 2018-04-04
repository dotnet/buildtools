using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
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
        readonly Stopwatch clock;
        private ConcurrentDictionary<string, long> runningTests;
        private Thread watcher;
        readonly int longTestMaxMilliseconds = 1_000 * 60 * 5;
        readonly int longTestCheckMilliseconds = 1_000 * 60;


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

            this.clock =  new Stopwatch();
            this.runningTests = new ConcurrentDictionary<string, long>();
        }

        protected override bool Visit(ITestAssemblyStarting assemblyStarting)
        {
            assemblyFileName = Path.GetFileName(assemblyStarting.TestAssembly.Assembly.AssemblyPath);

            lock (consoleLock)
                Console.WriteLine("Starting:    {0}", Path.GetFileNameWithoutExtension(assemblyFileName));

            clock.Start();
            watcher = new Thread(new ThreadStart(TestWatcher));
            watcher.IsBackground = true;
            watcher.Start();

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

            runningTests = null;
            clock.Stop();
            return result;
        }

        protected override bool Visit(ITestFailed testFailed)
        {
            lock (consoleLock)
            {
                // TODO: Thread-safe way to figure out the default foreground color

                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("   {0} [FAIL]", XmlEscape(testFailed.Test.DisplayName));
                Console.ForegroundColor = ConsoleColor.Gray;
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
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Error.WriteLine("   {0} [SKIP]", XmlEscape(testSkipped.Test.DisplayName));
                Console.ForegroundColor = ConsoleColor.Gray;
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

            if (!runningTests.TryAdd(testStarting.Test.DisplayName, clock.ElapsedMilliseconds))
            {
                lock (consoleLock)
                {
                    Console.WriteLine("ERROR: Failed to add {0} to running tests set.", testStarting.Test.DisplayName);
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
            if (!runningTests.TryRemove(testFinished.Test.DisplayName, out long elapsed))
            {
                lock (consoleLock)
                {
                    Console.WriteLine("ERROR: Failed to find {0} in running test set.", testFinished.Test.DisplayName);
                }
            }
            if (elapsed > longTestMaxMilliseconds)
            {
                lock (consoleLock)
                {
                    Console.WriteLine("WARNING: Long running test {0} finished in {1}ms.", testFinished.Test.DisplayName, elapsed);
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
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("   [{0}] {1}", failureName, XmlEscape(failureInfo.ExceptionTypes[0]));
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Error.WriteLine("      {0}", XmlEscape(ExceptionUtility.CombineMessages(failureInfo)));

                WriteStackTrace(ExceptionUtility.CombineStackTraces(failureInfo));
            }
        }

        void WriteStackTrace(string stackTrace)
        {
            if (String.IsNullOrWhiteSpace(stackTrace))
                return;

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Error.WriteLine("      Stack Trace:");

            Console.ForegroundColor = ConsoleColor.Gray;
            foreach (var stackFrame in stackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
            {
                Console.Error.WriteLine("         {0}", StackFrameTransformer.TransformFrame(stackFrame, defaultDirectory));
            }
        }

        private void TestWatcher()
        {
            try
            {
                while (runningTests != null)
                {
                    Thread.Sleep(longTestCheckMilliseconds);

                    if (runningTests == null)
                    {
                        break;
                    }

                    long  now = clock.ElapsedMilliseconds;
                    foreach (KeyValuePair<string, long> pair in runningTests)
                    {
                        if (( now - pair.Value) > longTestMaxMilliseconds)
                        {
                            lock (consoleLock)
                            {
                                Console.WriteLine("WARNING: {0} is running for {1}s.", pair.Key, (now - pair.Value) / 1000);
                            }
                        }
                    }
                }
            }
            catch { };
        }
    }
}
