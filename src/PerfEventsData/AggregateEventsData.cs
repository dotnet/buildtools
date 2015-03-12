// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace PerfEventsData
{
    /// <summary>
    /// Class containing the aggregate CLR and kernel performance data.
    /// This class implements IXmlSerializable for data reporting and restoration purposes.
    /// </summary>
    public class AggregateEventsData : IXmlSerializable
    {
        private string m_testName;
        private Platform m_platform;
        private Architecture m_arch;
        private Dictionary<ClrPerfEvents, BaseEventData<ClrPerfEvents>> m_clrEventsData;
        private Dictionary<KernelPerfEvents, BaseEventData<KernelPerfEvents>> m_kernelEventsData;

        public AggregateEventsData()
        {
            // used for serialization
            m_clrEventsData = new Dictionary<ClrPerfEvents, BaseEventData<ClrPerfEvents>>();
            m_kernelEventsData = new Dictionary<KernelPerfEvents, BaseEventData<KernelPerfEvents>>();
        }

        public AggregateEventsData(string testName, Platform platform, Architecture architecture) : this()
        {
            m_testName = testName;
            m_platform = platform;
            m_arch = architecture;
        }

        /// <summary>
        /// Gets the test name for this set of performance data.
        /// </summary>
        public string TestName { get { return m_testName; } }

        /// <summary>
        /// Gets the OS platform for this set of performance data.
        /// </summary>
        public Platform Platform { get { return m_platform; } }

        /// <summary>
        /// Gets the architecture for this set of performance data.
        /// </summary>
        public Architecture Architecture { get { return m_arch; } }

        /// <summary>
        /// Gets the collection of CLR events data.
        /// </summary>
        public Dictionary<ClrPerfEvents, BaseEventData<ClrPerfEvents>> ClrEventsData { get { return m_clrEventsData; } }

        /// <summary>
        /// Gets the collection of kernel events data.
        /// </summary>
        public Dictionary<KernelPerfEvents, BaseEventData<KernelPerfEvents>> KernelEventsData { get { return m_kernelEventsData; } }

        /// <summary>
        /// Adds CLR event data.  Duplicates are not allowed.
        /// </summary>
        /// <param name="clrEventData">An instance of BaseEventData with ClrPerfEvents.</param>
        public void AddData(BaseEventData<ClrPerfEvents> clrEventData)
        {
            if (clrEventData == null)
                throw new ArgumentNullException("clrEventData", "Argument cannot be null.");

            if (m_clrEventsData.ContainsKey(clrEventData.Event))
                throw new ArgumentException(string.Format("Data for {0} CLR event already exists.", clrEventData.Event));

            m_clrEventsData.Add(clrEventData.Event, clrEventData);
        }

        /// <summary>
        /// Adds kernel event data.  Duplicates are not allowed.
        /// </summary>
        /// <param name="kernelEventData">An instance of BaseEventData with KernelPerfEvents.</param>
        public void AddData(BaseEventData<KernelPerfEvents> kernelEventData)
        {
            if (kernelEventData == null)
                throw new ArgumentNullException("kernelEventData", "Argument cannot be null.");

            if (m_kernelEventsData.ContainsKey(kernelEventData.Event))
                throw new ArgumentException(string.Format("Data for {0} kernel event already exists.", kernelEventData.Event));

            m_kernelEventsData.Add(kernelEventData.Event, kernelEventData);
        }

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            if (string.CompareOrdinal(reader.Name, typeof(AggregateEventsData).Name) != 0)
                throw new InvalidOperationException(string.Format("Root element '{0}' was unexpected.", reader.Name));

            var archAttrVal = reader.GetAttribute(typeof(Architecture).Name);
            Debug.Assert(!string.IsNullOrEmpty(archAttrVal));

            var result = Enum.TryParse<Architecture>(archAttrVal, out m_arch);
            if (!result)
                throw new XmlException(string.Format("The value '{0}' is not valid for {1}@{2}.", archAttrVal, typeof(AggregateEventsData).Name, typeof(Architecture).Name));

            var platformAttrVal = reader.GetAttribute(typeof(Platform).Name);
            Debug.Assert(!string.IsNullOrEmpty(platformAttrVal));

            result = Enum.TryParse<Platform>(platformAttrVal, out m_platform);
            if (!result)
                throw new XmlException(string.Format("The value '{0}' is not valid for {1}@{2}.", platformAttrVal, typeof(AggregateEventsData).Name, typeof(Platform).Name));

            m_testName = reader.GetAttribute(XmlSchemaValues.TestName);
            Debug.Assert(!string.IsNullOrEmpty(m_testName));

            while (reader.Read())
            {
                if (string.CompareOrdinal(reader.Name, typeof(ClrPerfEvents).Name) == 0)
                {
                    while (reader.Read() && reader.NodeType != XmlNodeType.EndElement)
                    {
                        BaseEventData<ClrPerfEvents> clrEventData = (BaseEventData<ClrPerfEvents>)ReadXmlEventData(reader);
                        m_clrEventsData.Add(clrEventData.Event, clrEventData);
                    }

                    // reached the end of ClrEvents
                    Debug.Assert(string.CompareOrdinal(reader.Name, typeof(ClrPerfEvents).Name) == 0 && reader.NodeType == XmlNodeType.EndElement);
                }
                else if (string.CompareOrdinal(reader.Name, typeof(KernelPerfEvents).Name) == 0)
                {
                    while (reader.Read() && reader.NodeType != XmlNodeType.EndElement)
                    {
                        BaseEventData<KernelPerfEvents> kernelEventData = (BaseEventData<KernelPerfEvents>)ReadXmlEventData(reader);
                        m_kernelEventsData.Add(kernelEventData.Event, kernelEventData);
                    }

                    // reached the end of KernelEvents
                    Debug.Assert(string.CompareOrdinal(reader.Name, typeof(KernelPerfEvents).Name) == 0 && reader.NodeType == XmlNodeType.EndElement);
                }
                else
                {
                    // there are two reasons for ending up in this else block,
                    // either we are at the end of the document or we've come
                    // across an element type that we don't understand.
                    if (string.CompareOrdinal(reader.Name, typeof(AggregateEventsData).Name) != 0)
                        throw new InvalidOperationException(string.Format("Unrecognized element '{0}'.", reader.Name));
                    else
                        Debug.Assert(reader.NodeType == XmlNodeType.EndElement);
                }
            }
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            writer.WriteStartAttribute(m_arch.GetType().Name);
            writer.WriteValue(m_arch.ToString());
            writer.WriteEndAttribute();

            writer.WriteStartAttribute(m_platform.GetType().Name);
            writer.WriteValue(m_platform.ToString());
            writer.WriteEndAttribute();

            writer.WriteStartAttribute(XmlSchemaValues.TestName);
            writer.WriteValue(m_testName);
            writer.WriteEndAttribute();

            writer.WriteStartElement(typeof(ClrPerfEvents).Name);
            foreach (var clrEventData in m_clrEventsData.Values)
                ((IXmlSerializable)clrEventData).WriteXml(writer);
            writer.WriteEndElement();

            writer.WriteStartElement(typeof(KernelPerfEvents).Name);
            foreach (var kernelEventData in m_kernelEventsData.Values)
                ((IXmlSerializable)kernelEventData).WriteXml(writer);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Generic helper for reading XML event data.
        /// </summary>
        /// <param name="reader">The XML reader provided by the deserializer.</param>
        /// <returns>An object that derives from BaseEventData of T.</returns>
        private object ReadXmlEventData(XmlReader reader)
        {
            Type eventDataType = Type.GetType(reader.GetAttribute(XmlSchemaValues.AttrType));
            object eventData = Activator.CreateInstance(eventDataType);

            // invoke the IXmlSerializable.ReadXml(XmlReader) method
            eventDataType.GetInterface("IXmlSerializable").GetMethod("ReadXml").Invoke(eventData, new object[] { reader });
            return eventData;
        }
    }
}
