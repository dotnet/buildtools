// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace PerfEventsData
{
    /// <summary>
    /// Base class for event data, providing common functionality.
    /// </summary>
    /// <typeparam name="T">The event type (i.e. ClrEvents or KernelEvents).</typeparam>
    public abstract class BaseEventData<T> : IXmlSerializable
    {
        private T m_event;
        private Comparison m_comparison;
        private double m_tolerance;

        public BaseEventData()
        {
            // used for serialization
            m_comparison = Comparison.LowerTheBetter;
            m_tolerance = 0.0;
        }

        public BaseEventData(T eventName) : this()
        {
            m_event = eventName;
        }

        /// <summary>
        /// Gets the event value with respect to T.
        /// </summary>
        public T Event { get { return m_event; } }

        /// <summary>
        /// Gets the comparison type for this event.
        /// </summary>
        public Comparison Comparison { get { return m_comparison; } }

        /// <summary>
        /// Gets the tolerance of deviation from the baseline.
        /// </summary>
        public double Tolerance { get { return m_tolerance; } protected set { m_tolerance = value; } }

        /// <summary>
        /// Compares the values of this baseline event to the values in the specified live event.
        /// </summary>
        /// <param name="other">The live event to compare.</param>
        /// <returns>Null if the live values are within acceptable limits with respect to the baseline, else a string with a failure report.</returns>
        public string CompareValues(BaseEventData<T> other)
        {
            // ensure we're comparing similar types (e.g. scalar int to scalar int etc.)
            if (this.GetType() != other.GetType())
                throw new InvalidOperationException(string.Format("Baseline type {0} cannot be compared against live type {1}.", this.GetType().Name, other.GetType().Name));

            return CompareValuesImpl(other);
        }

        /// <summary>
        /// Merges event data from the specified collection of events.  The collection must be homogenous.
        /// </summary>
        /// <param name="eventsData">Collection of data to merge.</param>
        /// <returns>An object deriving from BaseEventData of T containing the merged data.</returns>
        public BaseEventData<T> MergeEventData(IList<BaseEventData<T>> eventsData)
        {
            // ensure the collection is homogenous
            for (int i = 1; i < eventsData.Count; ++i)
            {
                if (eventsData[i].GetType() != eventsData[i - 1].GetType())
                    throw new InvalidOperationException();

                if (!eventsData[i].Event.Equals(eventsData[i - 1].Event))
                    throw new InvalidOperationException();
            }

            return MergeEventDataImpl(eventsData);
        }

        public override int GetHashCode()
        {
            return m_event.GetHashCode();
        }

        protected abstract string CompareValuesImpl(BaseEventData<T> other);

        protected abstract BaseEventData<T> MergeEventDataImpl(IList<BaseEventData<T>> eventsData);

        // the following methods and properties are all helpers for XML serialization

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            m_event = (T)Enum.Parse(typeof(T), reader.Name);

            // the Comparison attribute is optional
            var comparison = reader.GetAttribute(typeof(Comparison).Name);
            if (comparison != null)
            {
                var result = Enum.TryParse<Comparison>(comparison, out m_comparison);
                if (!result)
                    throw new XmlException(string.Format("Invalid value '{0}' for attribute {1}@{2}.", reader.Name, typeof(Comparison).Name));
            }

            // the Tolerance attribute is optional
            var tolerance = reader.GetAttribute(XmlSchemaValues.Tolerance);
            if (tolerance != null)
            {
                var result = double.TryParse(tolerance, out m_tolerance);
                if (!result)
                    throw new XmlException(string.Format("Invalid value '{0}' for attribute {1}@{2}, value should be of type double.", tolerance, reader.Name, XmlSchemaValues.Tolerance));
            }

            ReadXmlValue(reader);
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(m_event.ToString());

            if (m_tolerance != 0.0)
            {
                writer.WriteStartAttribute(XmlSchemaValues.Tolerance);
                writer.WriteValue(m_tolerance);
                writer.WriteEndAttribute();
            }

            writer.WriteStartAttribute(XmlSchemaValues.AttrType);
            writer.WriteValue(this.GetType().ToString());
            writer.WriteEndAttribute();

            WriteXmlValue(writer);
            writer.WriteEndElement();
        }

        protected abstract void ReadXmlValue(XmlReader reader);

        protected abstract void WriteXmlValue(XmlWriter writer);
    }
}
