using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Xunit.Abstractions;

namespace Xunit.ConsoleClient
{
    public class StandardUapVisitor : XmlTestExecutionVisitor
    {
        string assemblyName;
        readonly ConcurrentDictionary<string, ExecutionSummary> completionMessages;
        readonly StreamWriter log;
        readonly bool showProgress;
        readonly bool failSkips;

        public StandardUapVisitor(XElement assemblyElement,
                                     Func<bool> cancelThunk,
                                     StreamWriter log,
                                     ConcurrentDictionary<string, ExecutionSummary> completionMessages,
                                     bool showProgress,
                                     bool failSkips)
            : base(assemblyElement, cancelThunk)
        {
            this.completionMessages = completionMessages;
            this.log = log;
            this.showProgress = showProgress;
            this.failSkips = failSkips;
        }

        public ExecutionSummary ExecutionSummary 
        {
            get 
            {
                ExecutionSummary summary;
                if (completionMessages.TryGetValue(assemblyName, out summary))
                {
                    return summary;
                }

                return new ExecutionSummary();
            }
        }

        protected override bool Visit(ITestAssemblyStarting assemblyStarting)
        {
            assemblyName = Path.GetFileNameWithoutExtension(assemblyStarting.TestAssembly.Assembly.AssemblyPath);

            log.WriteLine($"Starting:    {assemblyName}");

            return base.Visit(assemblyStarting);
        }

        protected override bool Visit(ITestAssemblyFinished assemblyFinished)
        {
            // Base class does computation of results, so call it first.
            var result = base.Visit(assemblyFinished);

            log.WriteLine($"Finished:    {assemblyName}");

            completionMessages.TryAdd(assemblyName, new ExecutionSummary
            {
                Total = assemblyFinished.TestsRun,
                Failed = !failSkips ? assemblyFinished.TestsFailed : assemblyFinished.TestsFailed + assemblyFinished.TestsSkipped,
                Skipped = !failSkips ? assemblyFinished.TestsSkipped : 0,
                Time = assemblyFinished.ExecutionTime,
                Errors = Errors
            });

            return result;
        }

        protected override bool Visit(ITestFailed testFailed)
        {
            log.WriteLine($"   {XmlEscape(testFailed.Test.DisplayName)} [FAIL]");
            log.WriteLine($"      {ExceptionUtility.CombineMessages(testFailed).Replace(Environment.NewLine, Environment.NewLine + "      ")}");

            WriteStackTrace(ExceptionUtility.CombineStackTraces(testFailed));

            return base.Visit(testFailed);
        }

        protected override bool Visit(ITestPassed testPassed)
        {
            return base.Visit(testPassed);
        }

        protected override bool Visit(ITestSkipped testSkipped)
        {
            if (failSkips)
            {
                return Visit(new TestFailed(testSkipped.Test, 0M, "", new[] { "FAIL_SKIP" }, new[] { testSkipped.Reason }, new[] { "" }, new[] { -1 }));
            }

            log.WriteLine($"   {XmlEscape(testSkipped.Test.DisplayName)} [SKIP]");
            log.WriteLine($"      {XmlEscape(testSkipped.Reason)}");

            return base.Visit(testSkipped);
        }

        protected override bool Visit(ITestStarting testStarting)
        {
            if (showProgress)
            {
                log.WriteLine($"   {XmlEscape(testStarting.Test.DisplayName)} [STARTING]");
            }
            return base.Visit(testStarting);
        }

        protected override bool Visit(ITestFinished testFinished)
        {
            if (showProgress)
            {
                log.WriteLine($"   {XmlEscape(testFinished.Test.DisplayName)} [FINISHED] Time: {testFinished.ExecutionTime}s");
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
            WriteError($"Test Assembly Cleanup Failure ({cleanupFailure.TestAssembly.Assembly.AssemblyPath})", cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestCaseCleanupFailure cleanupFailure)
        {
            WriteError($"Test Case Cleanup Failure ({cleanupFailure.TestCase.DisplayName})", cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestClassCleanupFailure cleanupFailure)
        {
            WriteError($"Test Class Cleanup Failure ({cleanupFailure.TestClass.Class.Name})", cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestCollectionCleanupFailure cleanupFailure)
        {
            WriteError($"Test Collection Cleanup Failure ({cleanupFailure.TestCollection.DisplayName})", cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestCleanupFailure cleanupFailure)
        {
            WriteError($"Test Cleanup Failure ({cleanupFailure.Test.DisplayName})", cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestMethodCleanupFailure cleanupFailure)
        {
            WriteError($"Test Method Cleanup Failure ({cleanupFailure.TestMethod.Method.Name})", cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected void WriteError(string failureName, IFailureInformation failureInfo)
        {
            log.WriteLine($"   [{failureName}] {XmlEscape(failureInfo.ExceptionTypes[0])}");
            log.WriteLine($"      {XmlEscape(ExceptionUtility.CombineMessages(failureInfo))}");

            WriteStackTrace(ExceptionUtility.CombineStackTraces(failureInfo));
        }

        void WriteStackTrace(string stackTrace)
        {
            if (String.IsNullOrWhiteSpace(stackTrace))
                return;

            log.WriteLine("      Stack Trace:");

            foreach (var stackFrame in stackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
            {
                log.WriteLine($"         {StackFrameTransformer.TransformFrame(stackFrame, Directory.GetCurrentDirectory())}");
            }
        }
    }
}