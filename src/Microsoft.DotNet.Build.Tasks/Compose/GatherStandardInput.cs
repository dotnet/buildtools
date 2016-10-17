// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks.Compose
{
    public class GatherStandardInput : Task
    {
        public string WriteFilePath { get; set; }

        [Output]
        public string Contents { get; set; }

        public override bool Execute()
        {
            Contents = ReadAllStandardInput();

            if (!string.IsNullOrEmpty(WriteFilePath))
            {
                File.WriteAllText(WriteFilePath, Contents);
            }
            return true;
        }

        private static string ReadAllStandardInput()
        {
            using (var stdInReader = new StreamReader(Console.OpenStandardInput()))
            {
                return stdInReader.ReadToEnd();
            }
        }
    }
}
