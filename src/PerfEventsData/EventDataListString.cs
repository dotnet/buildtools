// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml;

namespace PerfEventsData
{
    /// <summary>
    /// Class containing non-scalar event data.
    /// </summary>
    /// <typeparam name="T">The event type (i.e. ClrEvents or KernelEvents).</typeparam>
    public class EventDataListString<T> : BaseEventData<T>
    {
        private ICollection<string> m_values;

        public EventDataListString()
        {
            // used for serialization
        }

        public EventDataListString(T eventName)
            : base(eventName)
        {
        }

        /// <summary>
        /// Gets or sets the ICollection of string values.
        /// </summary>
        public ICollection<string> Values { get { return m_values; } set { m_values = value; } }

        protected override string CompareValuesImpl(BaseEventData<T> other)
        {
            var otherT = (EventDataListString<T>)other;

            var sb = new StringBuilder();

            foreach (var value in m_values)
            {
                if (!otherT.Values.Contains(value) && Comparison != Comparison.LowerTheBetter)
                    sb.AppendLine(string.Format("FAIL: comparison for event {0}, baseline data contains value {1} not in live date.", Event, value));
            }

            foreach (var value in otherT.Values)
            {
                if (!m_values.Contains(value) && Comparison != Comparison.GreaterTheBetter)
                    sb.AppendLine(string.Format("FAIL: comparison for event {0}, live data contains value {1} not in baseline.", Event, value));
            }

            if (sb.Length == 0)
                return null;
            else
                return sb.ToString();
        }

        protected override BaseEventData<T> MergeEventDataImpl(IList<BaseEventData<T>> eventsData)
        {
            var mergedEvent = new EventDataListString<T>(eventsData[0].Event);
            var mergedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var eventData in eventsData)
            {
                foreach (var value in ((EventDataListString<T>)eventData).Values)
                {
                    if (!mergedValues.Contains(value))
                        mergedValues.Add(value);
                }
            }

            mergedEvent.Values = mergedValues;
            return mergedEvent;
        }

        // the following methods and properties are all helpers for XML serialization

        protected override void ReadXmlValue(XmlReader reader)
        {
            // reader should be pointing to the opening element
            Debug.Assert(reader.NodeType == XmlNodeType.Element);

            //     <ImageLoad Scalar="false" Type="System.String">
            //       <Value>advapi32.dll</Value>
            //       <Value>bcrypt.dll</Value>
            //     </ImageLoad>

            string elementName = reader.Name;
            var values = new List<string>();

            while (reader.Read() && reader.NodeType != XmlNodeType.EndElement)
            {
                Debug.Assert(string.CompareOrdinal(reader.Name, XmlSchemaValues.AttrValue) == 0);

                // move to the value
                var read = reader.Read();
                Debug.Assert(read);

                values.Add(reader.Value);

                // move to the end element
                read = reader.Read();
                Debug.Assert(read && reader.NodeType == XmlNodeType.EndElement);
            }

            m_values = values;

            // should be at the end of list of values
            Debug.Assert(reader.NodeType == XmlNodeType.EndElement && string.CompareOrdinal(reader.Name, elementName) == 0);
        }

        protected override void WriteXmlValue(XmlWriter writer)
        {
            if (m_values == null || m_values.Count == 0)
                throw new InvalidOperationException(string.Format("Non-scalar data for event {0} contains no data.", Event));

            // sort the data before writing it out
            var sortedValues = m_values.OrderBy(val => val);

            foreach (var value in sortedValues)
            {
                writer.WriteStartElement(XmlSchemaValues.AttrValue);
                writer.WriteValue(value);
                writer.WriteEndElement();
            }
        }
    }
}
