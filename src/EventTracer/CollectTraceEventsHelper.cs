// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System;

namespace EventTracer
{
    /// <summary>
    /// Helper class to start or stop collecting ETW data.
    /// </summary>
    internal class CollectTraceEventsHelper : IDisposable
    {
        private TraceEventSession m_traceSession;

        public CollectTraceEventsHelper(CommandLineOptions options)
        {
            if (TraceEventSession.IsElevated() != true)
                throw new InvalidOperationException("Collecting perf events requires administrative privileges.");

            if (options.ClrEvents.Events == ClrTraceEventParser.Keywords.None && options.KernelEvents == KernelTraceEventParser.Keywords.None)
                throw new PowerArgs.ArgException("Must specify at least one CLR or kernel event.");

            // verify session name
            var existingSessions = TraceEventSession.GetActiveSessionNames();

            if (options.Mode == ExecutionMode.Start && existingSessions.Contains(options.TestName))
                throw new InvalidOperationException(string.Format("The session name '{0}' is already in use.", options.TestName));
            else if (options.Mode == ExecutionMode.Stop && !existingSessions.Contains(options.TestName))
                throw new InvalidOperationException(string.Format("The session name '{0}' does not exist.", options.TestName));

            m_traceSession = new TraceEventSession(options.TestName, options.DataFile);

            if (options.Mode == ExecutionMode.Start)
            {
                m_traceSession.BufferSizeMB = 512;

                // starting a new session, enable providers
                m_traceSession.EnableKernelProvider(options.KernelEvents);
                m_traceSession.EnableProvider(ClrTraceEventParser.ProviderGuid, TraceEventLevel.Informational, (ulong)options.ClrEvents.Events);

                // keep the session active after the process terminates
                m_traceSession.StopOnDispose = false;
            }
        }

        public void Dispose()
        {
            m_traceSession.Dispose();
        }
    }
}
