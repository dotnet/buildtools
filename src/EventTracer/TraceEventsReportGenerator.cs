// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using PerfEventsData;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace EventTracer
{
    /// <summary>
    /// Class used to generate PerfEventsData from ETW data.
    /// </summary>
    class TraceEventsReportGenerator
    {
        private string m_testName;
        private string m_etlFile;
        private string m_process;
        private ClrTraceEventParser.Keywords m_clrEvents;
        private KernelTraceEventParser.Keywords m_kernelEvents;
        private AggregateEventsData m_aed;
        private bool m_processFound;

        public TraceEventsReportGenerator(CommandLineOptions commandLineOptions)
        {
            if (!File.Exists(commandLineOptions.DataFile))
                throw new FileNotFoundException("Couldn't find the specified data file.", commandLineOptions.DataFile);

            m_testName = commandLineOptions.TestName;
            m_etlFile = commandLineOptions.DataFile;
            m_process = commandLineOptions.ProcessName;
            m_clrEvents = commandLineOptions.ClrEvents.Events;
            m_kernelEvents = commandLineOptions.KernelEvents;
        }

        /// <summary>
        /// Generate the PerfEventsData from the info provided to the ctor.
        /// </summary>
        /// <returns>A new instance of the AggregateEventsData class.</returns>
        public AggregateEventsData Generate()
        {
            using (var eventSource = new ETWTraceEventSource(m_etlFile))
            {
                if (eventSource.EventsLost > 0)
                    Console.WriteLine("WARNING: {0} events were lost during capture.", eventSource.EventsLost);

                m_aed = new AggregateEventsData(m_testName, Platform.Windows, eventSource.PointerSize == 8 ? Architecture.Amd64 : Architecture.X86);

                ParseClrTraceEvents(eventSource);
                ParseKernelTraceEvents(eventSource);

                // process the stream of events
                eventSource.Process();
            }

            if (!m_processFound)
                throw new ArgumentException(string.Format("No data was found for process named {0}.  Please ensure the name of the process is correct.", m_process));

            return m_aed;
        }

        /// <summary>
        /// Wire up parsing of CLR event data from the specified ETWTraceEventSource.
        /// </summary>
        /// <param name="eventSource">The ETWTraceEventSource from which to parse the data.</param>
        private void ParseClrTraceEvents(ETWTraceEventSource eventSource)
        {
            if (m_clrEvents == ClrTraceEventParser.Keywords.None)
                return;

            var clrTraceEventParser = new ClrTraceEventParser(eventSource);

            // iterate over each set bit, wiring up a callback to parse the data
            ulong eventBits = (ulong)m_clrEvents;
            int bitPos = 0;

            while (eventBits > 0)
            {
                // cycle through until a set bit is found
                while ((((ulong)eventBits) & (ulong)(1 << bitPos)) == 0)
                {
                    ++bitPos;
                    Debug.Assert(bitPos < 64);
                }

                var bitVal = (ulong)(1 << bitPos);

                // now strip it from eventBits and covert it to its enum value
                eventBits ^= bitVal;
                ClrTraceEventParser.Keywords clrEvent = (ClrTraceEventParser.Keywords)bitVal;

                // aggregate the high and low events if both are available (enabling both provides a more complete value)
                if ((clrEvent & ClrTraceEventParser.Keywords.GCSampledObjectAllocationHigh) == ClrTraceEventParser.Keywords.GCSampledObjectAllocationHigh ||
                    (clrEvent & ClrTraceEventParser.Keywords.GCSampledObjectAllocationLow) == ClrTraceEventParser.Keywords.GCSampledObjectAllocationLow ||
                    (clrEvent & ClrTraceEventParser.Keywords.GCAllObjectAllocation) == ClrTraceEventParser.Keywords.GCAllObjectAllocation)
                {
                    if (!m_aed.ClrEventsData.ContainsKey(ClrPerfEvents.GCBytesAllocated))
                        m_aed.AddData(new EventDataScalarLong<ClrPerfEvents>(ClrPerfEvents.GCBytesAllocated));

                    var gcBytesAllocated = (EventDataScalarLong<ClrPerfEvents>)m_aed.ClrEventsData[ClrPerfEvents.GCBytesAllocated];

                    clrTraceEventParser.GCSampledObjectAllocation += delegate(GCSampledObjectAllocationTraceData data)
                    {
                        if (string.Compare(data.ProcessName, m_process, true) == 0)
                        {
                            gcBytesAllocated.Value += data.TotalSizeForTypeSample;
                            m_processFound = true;
                        }
                    };
                }
                else
                {
                    Console.WriteLine("WARNING: CLR event {0} NYI for reporting.", clrEvent);
                }
            }
        }

        /// <summary>
        /// Wire up parsing of kernel event data from the specified ETWTraceEventSource.
        /// </summary>
        /// <param name="eventSource">The ETWTraceEventSource from which to parse the data.</param>
        private void ParseKernelTraceEvents(ETWTraceEventSource eventSource)
        {
            if (m_kernelEvents == KernelTraceEventParser.Keywords.None)
                return;

            var kernelParser = new KernelTraceEventParser(eventSource);

            // iterate over each set bit, wiring up a callback to parse the data
            uint eventBits = (uint)m_kernelEvents;
            int bitPos = 0;

            while (eventBits > 0)
            {
                // cycle through until a set bit is found
                while ((((uint)eventBits) & (uint)(1 << bitPos)) == 0)
                {
                    ++bitPos;
                    Debug.Assert(bitPos < 32);
                }

                var bitVal = (uint)(1 << bitPos);

                // now strip it from eventBits and covert it to its enum value
                eventBits ^= bitVal;
                KernelTraceEventParser.Keywords kernelEvent = (KernelTraceEventParser.Keywords)bitVal;

                if ((kernelEvent & KernelTraceEventParser.Keywords.ImageLoad) == KernelTraceEventParser.Keywords.ImageLoad)
                {
                    var modLoads = new EventDataListString<KernelPerfEvents>(KernelPerfEvents.ModuleLoad);

                    modLoads.Values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    kernelParser.ImageLoad += delegate(ImageLoadTraceData data)
                    {
                        if (string.Compare(data.ProcessName, m_process, true) == 0)
                        {
                            var modName = Path.GetFileName(data.FileName);
                            if (!modLoads.Values.Contains(modName))
                                modLoads.Values.Add(modName);

                            m_processFound = true;
                        }
                    };

                    m_aed.AddData(modLoads);
                }
                else
                {
                    Console.WriteLine("WARNING: Kernel event {0} NYI for reporting.", kernelEvent);
                }
            }
        }
    }
}
