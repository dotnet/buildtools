// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Tracing.Parsers;
using PowerArgs;
using System;
using System.Collections.Generic;

namespace EventTracer
{
    /// <summary>
    /// Indicates mode of execution for handling ETW data.
    /// </summary>
    [Flags]
    enum ExecutionMode
    {
        /// <summary>
        /// Start collecting ETW data.
        /// </summary>
        [ArgDescription("Start collecting ETW data.")]
        Start = 1,

        /// <summary>
        /// Stop collecting ETW data.
        /// </summary>
        [ArgDescription("Stop collecting ETW data.")]
        Stop = 2
    }

    /// <summary>
    /// Class to contain CLR trace events to record.
    /// This is part of the workaround for reviving CLR events.
    /// </summary>
    class ClrEvents
    {
        private ClrTraceEventParser.Keywords m_events;

        public ClrEvents(ClrTraceEventParser.Keywords events)
        {
            m_events = events;
        }

        public ClrTraceEventParser.Keywords Events { get { return m_events; } }
    }

    /// <summary>
    /// Class to contain command line argument values.
    /// </summary>
    class CommandLineOptions
    {
        [ArgDescription("How the tool should run.")]
        [ArgRequired]
        public ExecutionMode Mode { get; set; }

        [ArgDescription("The test name for event collection.")]
        [ArgRequired]
        public string TestName { get; set; }

        [ArgDescription("The results data file.")]
        [ArgRequired]
        public string DataFile { get; set; }

        [ArgDefaultValue(ClrTraceEventParser.Keywords.All)]
        [ArgDescription("The CLR events to capture.")]
        public ClrEvents ClrEvents { get; set; }

        [ArgDefaultValue(KernelTraceEventParser.Keywords.All)]
        [ArgDescription("The kernel events to capture.")]
        public KernelTraceEventParser.Keywords KernelEvents { get; set; }

        [ArgDescription("The name of the XML report file to generate.")]
        public string XmlReport { get; set; }

        [ArgDescription("The name of the process that contains the metrics of interest.")]
        public string ProcessName { get; set; }

        [ArgReviver]
        public static ClrEvents Revive(string key, string value)
        {
            // this reviver is a workaround due to a limitation in PowerArgs,
            // it doesn't support enums with an underlying type other than int.

            ClrTraceEventParser.Keywords events = ClrTraceEventParser.Keywords.None;

            var options = value.Split(new char[] { ',' });
            foreach (var option in options)
            {
                ClrTraceEventParser.Keywords result;
                var success = Enum.TryParse<ClrTraceEventParser.Keywords>(option, true, out result);
                if (!success)
                    throw new ArgException(string.Format("Bad value '{0}' parsing CLR events.", option));

                events |= result;
            }

            return new ClrEvents(events);
        }
    }
}
