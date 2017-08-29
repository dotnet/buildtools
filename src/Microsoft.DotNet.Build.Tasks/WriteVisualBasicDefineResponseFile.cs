// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using System.IO;

/// <summary>
/// Writes a response file that sets the define flag to the Visual Basic complier
/// based on what constants should be defined. This is needed due to 
/// https://github.com/Microsoft/msbuild/issues/422 which prevents us from
/// using the native MSBuild logic to invoke the build for our VB Projects when
/// running on Unix platforms.
/// </summary>
namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class WriteVisualBasicDefineResponseFile : BuildTask
    {
        /// <summary>
        /// The set of DefineConstants that would be passed to the Vbc Task.
        /// </summary>
        [Required]
        public string DefineConstants { get; set; }

        /// <summary>
        /// The response file to generate. If this file already exists, it is overwritten.
        /// </summary>
        [Required]
        public string File { get; set; }

        public override bool Execute()
        {
            using (var streamWriter = new StreamWriter(System.IO.File.Open(File, FileMode.Create)))
            {
                streamWriter.Write("/define:\"");
                streamWriter.Write(DefineConstants.Replace("\"", "\\\""));
                streamWriter.Write("\"");
            }

            return true;
        }
    }
}