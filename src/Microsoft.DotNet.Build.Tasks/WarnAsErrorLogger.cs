
using Microsoft.Build.Framework;
using System;

namespace Microsoft.DotNet.Build.Tasks
{
    public class WarnAsErrorLogger : ILogger
    {
        private bool _warningsLogged;

        public string Parameters { get; set; }
        public LoggerVerbosity Verbosity { get; set; }

        public void Initialize(IEventSource eventSource)
        {
            eventSource.WarningRaised += EventSource_WarningRaised;
        }

        private void EventSource_WarningRaised(object sender, BuildWarningEventArgs e)
        {
            _warningsLogged = true;
        }

        public void Shutdown()
        {
            if (_warningsLogged)
            {
                throw new Exception("Warings were logged, failing the build.");
            }
        }
    }
}
