// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Newtonsoft.Json;
using System;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    /* Given an msbuild item group, and a property, determine if the specified property is in the item group. */
    public class IsPropertyInItemGroup : ITask
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

        private string _varValue;
        [Required]
        public string Value
        {
            get { return _varValue; }
            set { _varValue = value; }
        }

        private string[] _items;
        [Required]
        public string[] Items
        {
            get { return _items; }
            set { _items = value; }
        }
        private bool _caseSensitive = true;
        public bool CaseSensitive
        {
            get { return _caseSensitive; }
            set { _caseSensitive = value; }
        }
        private string _exists = string.Empty;
        [Output]
        public string Exists
        {
            get { return _exists; }
            set { _exists = value; }
        }

        public bool Execute()
        {
            _exists = string.Empty;
            foreach (string item in _items)
            {
                if (_caseSensitive)
                {
                    if (item.Equals(Value))
                    {
                        _exists = "true";
                        break;
                    }
                }
                else
                {
                    if (item.Equals(Value, StringComparison.OrdinalIgnoreCase))
                    {
                        _exists = "true";
                        break;
                    }
                }
            }
            return true;
        }
    }
}
