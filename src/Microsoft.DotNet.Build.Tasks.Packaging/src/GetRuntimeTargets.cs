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
    public class GetRuntimeTargets : ITask
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

        public bool Execute()
        {
            return ParseRuntimeJsonFile();
        }

        private string[] _targetItems;
        [Output]
        public string[] TargetItems
        {
            get { return _targetItems; }
            set { _targetItems = value; }
        }

        private bool ParseRuntimeJsonFile()
        {
            if (string.IsNullOrEmpty(_jsonFilename) || !File.Exists(_jsonFilename))
                return false;

            // This is ugly, just tacking on the code here, need to move this to a different task.
            JObject jObject = JObject.Parse(File.ReadAllText(_jsonFilename));

            var targets = from t in jObject["targets"] select t;

            List<string> items = new List<string>();
            foreach (JToken target in targets)
            {
                JProperty property = (JProperty)target;
                items.Add(property.Name);
            }
            _targetItems = items.ToArray();
            return true;
        }
    }
}
