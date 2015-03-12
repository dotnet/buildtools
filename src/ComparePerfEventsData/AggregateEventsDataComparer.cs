// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using PerfEventsData;
using System;
using System.Collections.Generic;

namespace ComparePerfEventsData
{
    /// <summary>
    /// Class for comparing baseline and live events data.
    /// </summary>
    class AggregateEventsDataComparer
    {
        AggregateEventsData m_baseline;
        AggregateEventsData m_live;

        public AggregateEventsDataComparer(AggregateEventsData baseline, AggregateEventsData live)
        {
            if (baseline == null)
                throw new ArgumentNullException("baseline");

            if (live == null)
                throw new ArgumentNullException("live");

            // ensure that the data being compared is from the same architecture, platform and test

            if (string.Compare(baseline.TestName, live.TestName, StringComparison.OrdinalIgnoreCase) != 0)
                throw new ArgumentException(string.Format("Cannot compare results across different tests.  Baseline is {0} and live is {1}.", baseline.TestName, live.TestName));

            // TODO: there might be cases where cross-platform comparisons make sense
            if (baseline.Platform != live.Platform)
                throw new ArgumentException(string.Format("Cannot compare results across different platforms.  Baseline is {0} and live is {1}.", baseline.Platform, live.Platform));

            if (baseline.Architecture != live.Architecture)
                throw new ArgumentException(string.Format("Cannot compare results across different architectures.  Baseline is {0} and live is {1}.", baseline.Architecture, live.Architecture));

            m_baseline = baseline;
            m_live = live;
        }

        /// <summary>
        /// Performs the comparison.
        /// </summary>
        /// <returns>True if live is within acceptable tolerance of baseline, else false.</returns>
        public bool CompareResults()
        {
            bool comparisonFailed = false;

            comparisonFailed |= CompareResults(m_baseline.ClrEventsData, m_live.ClrEventsData);
            comparisonFailed |= CompareResults(m_baseline.KernelEventsData, m_live.KernelEventsData);

            return !comparisonFailed;
        }

        private bool CompareResults<T>(Dictionary<T, BaseEventData<T>> baselineCollection, Dictionary<T, BaseEventData<T>> liveCollection)
        {
            bool comparisonFailed = false;

            foreach (var baselineEvent in baselineCollection.Values)
            {
                if (liveCollection.ContainsKey(baselineEvent.Event))
                {
                    // perform the comparison
                    var liveEvent = liveCollection[baselineEvent.Event];

                    var resultMessage = baselineEvent.CompareValues(liveEvent);
                    if (resultMessage != null)
                    {
                        Console.WriteLine(resultMessage);
                        comparisonFailed = true;
                    }
                    else
                    {
                        Console.WriteLine("PASS: {0}", baselineEvent.Event);
                    }

                    // remove the entry from live so we can track any remaining data
                    liveCollection.Remove(baselineEvent.Event);
                }
                else
                {
                    // live data was a subset of baseline data
                    Console.WriteLine("ERROR: Live data set contains no data for {0}.", baselineEvent.Event);
                    comparisonFailed = true;
                }
            }

            if (liveCollection.Count > 0)
            {
                // live data set contained data not in the baseline
                foreach (var liveEvent in liveCollection.Values)
                    Console.WriteLine("ERROR: Live data set contains data for {0} which is not in the baseline.", liveEvent.Event);

                comparisonFailed = true;
            }

            return comparisonFailed;
        }
    }
}
