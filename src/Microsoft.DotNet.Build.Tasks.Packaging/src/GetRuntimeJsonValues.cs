// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    // Read a runtime.json file into an msbuild item group
    public class GetRuntimeJsonValues : ITask
    {
        private IBuildEngine _engine;
        IBuildEngine ITask.BuildEngine
        {
            get { return _engine; }
            set { _engine = value; }
        }

        private ITaskHost _host;
        ITaskHost ITask.HostObject
        {
            get { return _host; }
            set { _host = value; }
        }

        // runtime.json file path
        private string _jsonFilename;
        [Required]
        public string JsonFilename
        {
            get { return _jsonFilename; }
            set { _jsonFilename = value; }
        }

        // runtime.json values as an item group
        private string[] _jsonItems;
        [Output]
        public string[] JsonItems
        {
            get { return _jsonItems; }
            set { _jsonItems = value; }
        }
        public bool Execute()
        {
            return ParseRuntimeJsonFile();
        }

        private bool ParseRuntimeJsonFile()
        {
            if (string.IsNullOrEmpty(_jsonFilename) || !File.Exists(_jsonFilename))
                return false;
            List<string> items = new List<string>();
            JObject jObject = JObject.Parse(File.ReadAllText(_jsonFilename));

            var runtimes = from r in jObject["runtimes"] select r;
            foreach (JToken runtime in runtimes)
            {
                JProperty prop = (JProperty)runtime;
                string leafItem = ReadJsonLeaf(runtime);
                if (!items.Contains(leafItem))
                    items.Add(leafItem);
            }
            _jsonItems = items.ToArray();
            return true;
        }
        private string ReadJsonLeaf(JToken jToken)
        {
            if (jToken.HasValues)
            {
                foreach (JToken value in jToken.Values())
                {
                    return ReadJsonLeaf(value);
                }
            }
            else
            {
                if (jToken is JValue)
                {
                    JValue jValue = (JValue)jToken;
                    return jValue.Value.ToString();
                }
            }
            return string.Empty;
        }
    }
}
