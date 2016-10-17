// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.DotNet.VersionTools.Util
{
    /// <summary>
    /// Converts a class that inherits List`1 and has 1 T, many Ts, or null. 1 T is treated as a
    /// value, many Ts is treated as an array, and null is null.
    /// </summary>
    internal class ListOrSingleConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            IList list = (IList)value;
            object toWrite = list.Cast<object>().ToArray();
            // If there is only one element, write it without wrapping in an array.
            if (list.Count == 1)
            {
                toWrite = list[0];
            }
            serializer.Serialize(writer, toWrite);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }
            IList list = (IList)Activator.CreateInstance(objectType);

            Type baseListType = FindListBaseType(objectType);
            Type baseListArgType = baseListType.GetTypeInfo().GetGenericArguments()[0];

            if (reader.TokenType == JsonToken.StartArray)
            {
                IList elements = (IList)serializer.Deserialize(reader, baseListType);
                foreach (object element in elements)
                {
                    list.Add(element);
                }
            }
            else
            {
                // Read the individual element into the list using IList.Add implementation.
                list.Add(serializer.Deserialize(reader, baseListArgType));
            }
            return list;
        }

        public override bool CanConvert(Type objectType)
        {
            Type baseListType = FindListBaseType(objectType);

            return baseListType != null &&
                // Don't convert raw List<T> to avoid infinite recursion.
                baseListType != objectType;
        }

        /// <summary>
        /// Looks in "type"'s inheritance chain to find a List`1 type and returns it.
        /// </summary>
        private Type FindListBaseType(Type type)
        {
            if (type.IsConstructedGenericType)
            {
                Type genericType = type.GetGenericTypeDefinition();
                if (genericType == typeof(List<>))
                {
                    return type;
                }
            }
            Type baseType = type.GetTypeInfo().BaseType;
            if (baseType != null)
            {
                return FindListBaseType(baseType);
            }
            return null;
        }
    }
}