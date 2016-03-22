using Xunit;

namespace XunitUwpRunner
{
    internal class RunLogger : IRunnerLogger
    {
        readonly object lockObject = new object();

        public object LockObject
        {
            get
            {
                return lockObject;
            }
        }

        public void LogError(StackFrameInfo stackFrame, string message)
        {
        }

        public void LogImportantMessage(StackFrameInfo stackFrame, string message)
        {
        }

        public void LogMessage(StackFrameInfo stackFrame, string message)
        {
        }

        public void LogWarning(StackFrameInfo stackFrame, string message)
        {
        }
    }
}
