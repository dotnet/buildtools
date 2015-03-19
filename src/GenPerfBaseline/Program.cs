// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using PerfEventsData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace GenPerfBaseline
{
    class Program
    {
        static AggregateEventsData MegeData(ICollection<AggregateEventsData> eventsData)
        {
            if (eventsData == null)
                throw new ArgumentNullException("eventsData");

            // verify that the test and platform values are homogenous

            string testName = null;
            Platform? platform = null;
            Architecture? arch = null;

            foreach (var eventData in eventsData)
            {
                if (testName == null)
                    testName = eventData.TestName;
                else if (string.Compare(testName, eventData.TestName, StringComparison.OrdinalIgnoreCase) != 0)
                    throw new InvalidOperationException(string.Format("Cannot merge data from different tests '{0}' and '{1}'.", testName, eventData.TestName));

                if (platform == null)
                    platform = eventData.Platform;
                else if (platform.Value != eventData.Platform)
                    throw new InvalidOperationException(string.Format("Cannot merge data from different architectures {0} and {1}.", platform.Value, eventData.Platform));

                if (arch == null)
                    arch = eventData.Architecture;
                else if (arch.Value != eventData.Architecture)
                    throw new InvalidOperationException(string.Format("Cannot merge data from different architectures {0} and {1}.", arch.Value, eventData.Architecture));
            }

            AggregateEventsData mergedEventsData = new AggregateEventsData(testName, platform.Value, arch.Value);

            var clrEvents = new List<BaseEventData<ClrPerfEvents>>(eventsData.Count);
            var kernelEvents = new List<BaseEventData<KernelPerfEvents>>(eventsData.Count);

            foreach (var eventData in eventsData)
            {
                foreach (var clrEventData in eventData.ClrEventsData.Values)
                    clrEvents.Add(clrEventData);

                foreach (var kernelEventData in eventData.KernelEventsData.Values)
                    kernelEvents.Add(kernelEventData);
            }

            mergedEventsData.AddData(clrEvents[0].MergeEventData(clrEvents));
            mergedEventsData.AddData(kernelEvents[0].MergeEventData(kernelEvents));

            return mergedEventsData;
        }

        static int Main(string[] args)
        {
            var exitCode = 1;

            try
            {
                var cmdOptions = PowerArgs.Args.Parse<CommandLineOptions>(args);

                string path = Environment.CurrentDirectory;
                string pattern = cmdOptions.Input;

                if (cmdOptions.Input.IndexOf(Path.DirectorySeparatorChar) > -1)
                {
                    path = Path.GetDirectoryName(cmdOptions.Input);
                    pattern = Path.GetFileName(cmdOptions.Input);
                }

                var files = Directory.GetFiles(path, pattern);
                if (files == null)
                    throw new FileNotFoundException(string.Format("In directory '{0}' no files were found matching pattern '{1}'.", path, pattern));

                if (files.Length < 3)
                    throw new InvalidOperationException(string.Format("Merging data requires at least three input files ({0} available).", files.Length));

                var eventsDataList = new List<AggregateEventsData>(files.Length);
                var xmls = new XmlSerializer(typeof(AggregateEventsData));

                foreach (var file in files)
                {
                    using (StreamReader reader = new StreamReader(file))
                    {
                        var eventsData = (AggregateEventsData)xmls.Deserialize(reader);
                        eventsDataList.Add(eventsData);
                    }
                }

                // merge the data and write the result
                var result = MegeData(eventsDataList);
                using (StreamWriter writer = new StreamWriter(cmdOptions.Output))
                {
                    xmls.Serialize(writer, result);
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
