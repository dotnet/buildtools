// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Compose.Model.Command;
using Newtonsoft.Json;
using System;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks.Compose
{
    internal static class ConsumesHelpers
    {
        private static JsonSerializerSettings s_settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        public static ConsumesOutput ReadConsumesFile(string consumesJsonPath)
        {
            return Deserialize(File.ReadAllText(consumesJsonPath));
        }

        public static void WriteConsumesFile(string consumesJsonPath, ConsumesOutput output)
        {
            File.WriteAllText(consumesJsonPath, Serialize(output));
        }

        public static ConsumesOutput Deserialize(string consumesJsonContent)
        {
            return JsonConvert.DeserializeObject<ConsumesOutput>(consumesJsonContent, s_settings);
        }

        public static string Serialize(ConsumesOutput output)
        {
            return JsonConvert.SerializeObject(output, s_settings) + Environment.NewLine;
        }
    }
}
