// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;

namespace PerfEventsData
{
    /// <summary>
    /// Class containing scalar event data of type long.
    /// </summary>
    /// <typeparam name="T">The event type (i.e. ClrEvents or KernelEvents).</typeparam>
    public class EventDataScalarLong<T> : BaseEventData<T>
    {
        private long m_value;

        public EventDataScalarLong()
        {
            // used for serialization
        }

        public EventDataScalarLong(T eventName)
            : base(eventName)
        {
        }

        /// <summary>
        /// Gets or sets the data value.
        /// </summary>
        public long Value { get { return m_value; } set { m_value = value; } }

        protected override string CompareValuesImpl(BaseEventData<T> other)
        {
            var otherT = (EventDataScalarLong<T>)other;

            string errorMessage = null;

            if (m_value != otherT.Value)
            {
                // check that the variance is within acceptable limits
                double difference;

                if (otherT.Value > m_value && Comparison != Comparison.GreaterTheBetter)
                    difference = otherT.Value - m_value;
                else if (otherT.Value < m_value && Comparison != Comparison.LowerTheBetter)
                    difference = m_value - otherT.Value;
                else
                    difference = 0; // difference is ok

                if (difference > Tolerance)
                    errorMessage = string.Format("FAIL: comparison for event {0}, baseline = {1}, live = {2}, difference = {3}, tolerance = {4}", Event, m_value, otherT.Value, difference, Tolerance);
            }

            return errorMessage;
        }

        protected override BaseEventData<T> MergeEventDataImpl(IList<BaseEventData<T>> eventsData)
        {
            var mergedEvent = new EventDataScalarLong<T>(eventsData[0].Event);

            long sum = 0;
            foreach (var eventData in eventsData)
                sum += ((EventDataScalarLong<T>)eventData).Value;

            mergedEvent.Value = sum / eventsData.Count;

            // set a default tolerance based on the standard deviation

            double variance = 0;
            foreach (var eventData in eventsData)
                variance += Math.Pow(((EventDataScalarLong<T>)eventData).Value - mergedEvent.Value, 2);

            mergedEvent.Tolerance = Math.Sqrt(variance / eventsData.Count);

            return mergedEvent;
        }

        // the following methods and properties are all helpers for XML serialization

        protected override void ReadXmlValue(XmlReader reader)
        {
            // reader should be pointing to the opening element
            Debug.Assert(reader.NodeType == XmlNodeType.Element);

            // <GCSampledObjectAllocationHigh Scalar="true" Type="System.Int64">567113780</GCSampledObjectAllocationHigh>

            // move to the value
            var read = reader.Read();
            Debug.Assert(read);

            m_value = long.Parse(reader.Value);

            // move to the closing element
            read = reader.Read();
            Debug.Assert(read && reader.NodeType == XmlNodeType.EndElement);
        }

        protected override void WriteXmlValue(XmlWriter writer)
        {
            writer.WriteValue(m_value);
        }
    }
}
