// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using PerfEventsData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace ComparePerfEventsData
{
    class Program
    {
        static int Main(string[] args)
        {
            var exitCode = 1;

            try
            {
                var cmdOptions = PowerArgs.Args.Parse<CommandLineOptions>(args);

                if (string.Compare(cmdOptions.Baseline, cmdOptions.Live, StringComparison.OrdinalIgnoreCase) == 0)
                    throw new ArgumentException("The values of baseline and live parameters cannot be identical.");

                var xmls = new XmlSerializer(typeof(AggregateEventsData));

                AggregateEventsData baseline = null;
                AggregateEventsData live = null;

                using (StreamReader reader = new StreamReader(cmdOptions.Baseline))
                {
                    baseline = (AggregateEventsData)xmls.Deserialize(reader);
                }

                using (StreamReader reader = new StreamReader(cmdOptions.Live))
                {
                    live = (AggregateEventsData)xmls.Deserialize(reader);
                }

                var dataComparer = new AggregateEventsDataComparer(baseline, live);

                if (dataComparer.CompareResults())
                {
                    Console.WriteLine("PASS: perf results are ok");
                    exitCode = 0;
                }
                else
                {
                    Console.WriteLine("FAIL: perf results out of tolerance");
                }
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
