// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.VersionTools.Util
{
    internal static class FileUtils
    {
        public static bool ReplaceFileContents(string path, Func<string, string> replacement)
        {
            string contents;
            Encoding encoding;

            // Atttempt to preserve the file's encoding, using a UTF-8 encoding with no BOM if the file's
            // encoding cannot be detected. 
            using (StreamReader reader = new StreamReader(new FileStream(path, FileMode.Open), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true))
            {
                contents = reader.ReadToEnd();
                encoding = reader.CurrentEncoding;
            }

            string newContents = replacement(contents);

            if (contents != newContents)
            {
                Trace.TraceInformation($"Writing changes to {path}");
                File.WriteAllText(path, newContents, encoding);
                return true;
            }
            return false;
        }
    }
}
