// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class WriteSigningRequired : BuildTask
    {
        /// <summary>
        /// Gets or sets the name of the Authenticode signature to apply.
        /// </summary>
        [Required]
        public string AuthenticodeSig { get; set; }

        /// <summary>
        /// Gets or sets the name of the strong-name signature to apply.
        /// </summary>
        public string StrongNameSig { get; set; }

        /// <summary>
        /// Gets or sets the name of the signing marker file.
        /// </summary>
        [Required]
        public string MarkerFile { get; set; }

        public override bool Execute()
        {
            // serialize the settings to a JSON file

            var signTypeItem = new SignTypeItem();
            signTypeItem.Authenticode = AuthenticodeSig;
            signTypeItem.StrongName = StrongNameSig;

            using (var streamWriter = new StreamWriter(File.OpenWrite(MarkerFile)))
            {
                using (var jsonWriter = new JsonTextWriter(streamWriter))
                {
                    JsonSerializer jsonSerializer = new JsonSerializer();
                    jsonSerializer.Serialize(jsonWriter, signTypeItem);
                }
            }

            return true;
        }
    }
}
