// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public sealed class WriteItemsToJson : Task
    {
        /// <summary>
        /// The name of the JSON file to be created.
        /// </summary>
        [Required]
        public string JsonFileName { get; set; }

        /// <summary>
        /// An item group to be converted into JSON format.  For each item, all custom
        /// metadata will be converted into JSON name properties, where the custom metadata
        /// name/value pair is converted to a JSON property/value pair.
        /// For values surrounded by square brackets write the content as an array.  Each
        /// element is to be delimited by two semicolons (e.g. [one;;two;;three]).
        /// </summary>
        [Required]
        public ITaskItem[] Items { get; set; }

        public override bool Execute()
        {
            if (Items.Length == 0)
                throw new ArgumentException("The provided items contained zero entries.");

            if (!Directory.Exists(Path.GetDirectoryName(JsonFileName)))
                Directory.CreateDirectory(Path.GetDirectoryName(JsonFileName));
 
            JsonSerializer jsonSerializer = new JsonSerializer();
            using (FileStream fs = File.Create(JsonFileName))
            using (StreamWriter streamWriter = new StreamWriter(fs))
            {
                using (JsonTextWriter jsonWriter = new JsonTextWriter(streamWriter))
                {
                    jsonWriter.Formatting = Formatting.Indented;

                    if (Items.Length > 1)
                        jsonWriter.WriteStartArray();

                    foreach (ITaskItem item in Items)
                    {
                        var customMd = item.CloneCustomMetadata();
                        jsonWriter.WriteStartObject();
                        foreach (var key in customMd.Keys)
                        {
                            var mdString = key.ToString();
                            var mdValue = customMd[key].ToString();

                            jsonWriter.WritePropertyName(mdString);

                            // if the value is surrounded in square brackets it's meant to be an array.
                            // split the value into its respective chunks and write it into a JSON array.
                            if (mdValue.Length > 0 && mdValue[0] == '[' && mdValue[mdValue.Length - 1] == ']')
                            {
                                mdValue = mdValue.Substring(1, mdValue.Length - 2);
                                jsonWriter.WriteStartArray();

                                var parts = mdValue.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var part in parts)
                                    jsonWriter.WriteValue(part);

                                jsonWriter.WriteEndArray();
                            }
                            else
                            {
                                jsonWriter.WriteValue(mdValue);
                            }
                        }

                        jsonWriter.WriteEndObject();
                    }

                    if (Items.Length > 1)
                        jsonWriter.WriteEndArray();

                    Log.LogMessage(MessageImportance.High, "Writing {0}.", JsonFileName);
                }
            }

            return true;
        }
    }
}
