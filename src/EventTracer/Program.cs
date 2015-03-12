// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using PerfEventsData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace EventTracer
{
    class Program
    {
        static int Main(string[] args)
        {
            var exitCode = 1;

            try
            {
                var cmdOptions = PowerArgs.Args.Parse<CommandLineOptions>(args);

                using (var cteHelper = new CollectTraceEventsHelper(cmdOptions))
                {
                    Console.WriteLine("{0} collecting events for session {1}.", cmdOptions.Mode, cmdOptions.TestName);
                }

                // after stopping, generate an XML report of the data if the necessary params were provided
                if (cmdOptions.Mode == ExecutionMode.Stop && !string.IsNullOrEmpty(cmdOptions.XmlReport) && !string.IsNullOrEmpty(cmdOptions.ProcessName))
                {
                    TraceEventsReportGenerator report = new TraceEventsReportGenerator(cmdOptions);
                    var reportData = report.Generate();

                    XmlSerializer xmls = new XmlSerializer(typeof(AggregateEventsData));

                    using (StreamWriter writer = new StreamWriter(cmdOptions.XmlReport))
                    {
                        xmls.Serialize(writer, reportData);
                    }
                }

                exitCode = 0;
            }
            catch (PowerArgs.ArgException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(PowerArgs.ArgUsage.GenerateUsageFromTemplate<CommandLineOptions>());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            return exitCode;
        }
    }
}
