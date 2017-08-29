// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks
{
    public class PreprocessFile : BuildTask
    {
        [Required]
        public string SourceFile { get; set; }

        [Required]
        public string OutputFile { get; set; }

        public string[] Defines { get; set; }

        public override bool Execute()
        {
            HashSet<string> defineSet = new HashSet<string>(Defines ?? new string[] { }, StringComparer.Ordinal);
            Stack<bool> preprocessorStack = new Stack<bool>();

            preprocessorStack.Push(true);

            using (StreamWriter outputStream = new StreamWriter(File.Open(OutputFile, FileMode.Create, FileAccess.Write, FileShare.None), Encoding.UTF8))
            {
                foreach (string line in File.ReadLines(SourceFile))
                {
                    string trimmedLine = line.Trim();

                    if (trimmedLine.StartsWith("#if ", StringComparison.Ordinal))
                    {
                        string defineName = trimmedLine.Substring(3).Trim();

                        if (defineName == "")
                        {
                            Log.LogError("Malformed #if, missing symbol name");
                            return false;
                        }

                        if (defineName[0] != '!')
                        {
                            preprocessorStack.Push(preprocessorStack.Peek() && defineSet.Contains(defineName));
                        }
                        else
                        {
                            preprocessorStack.Push(preprocessorStack.Peek() && !defineSet.Contains(defineName.Substring(1)));
                        }
                    }
                    else if (trimmedLine.StartsWith("#endif", StringComparison.Ordinal))
                    {
                        preprocessorStack.Pop();

                        if (preprocessorStack.Count == 0)
                        {
                            Log.LogError("Extra #endif detected.");
                            return false;
                        }
                    }
                    else if (preprocessorStack.Peek())
                    {
                        outputStream.WriteLine(line);
                    }
                    else
                    {
                        // Exclued by defines.
                    }
                }

                if (preprocessorStack.Count != 1)
                {
                    Log.LogError("Missing #endif detected.");
                    return false;
                }
            }

            return true;
        }
    }
}