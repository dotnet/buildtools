using System;
using System.Diagnostics;

namespace Xunit.NetCore.Extensions
{
    public class DesktopTestTraceListener : DefaultTraceListener
    {
        public override void Fail(string message, string detailMessage)
        {
            StackTrace stack = new StackTrace(true);
            string stackTrace;

            try
            {
                stackTrace = stack.ToString();
            }
            catch
            {
                stackTrace = "";
            }

            throw new DebugAssertException(message, detailMessage, stackTrace);
        }

        private sealed class DebugAssertException : Exception
        {
            internal DebugAssertException(string message, string detailMessage, string stackTrace) :
                base(message + Environment.NewLine + detailMessage + Environment.NewLine + stackTrace)
            {
            }
        }
    }
}