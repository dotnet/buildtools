// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class ReadSigningRequired : BuildTask
    {
        /// <summary>
        /// Gets or sets the list of signing marker files.
        /// </summary>
        [Required]
        public ITaskItem[] MarkerFiles { get; set; }

        /// <summary>
        /// Gets or sets the list of task items containing signing metadata.
        /// </summary>
        [Output]
        public ITaskItem[] SigningMetadata { get; set; }

        public override bool Execute()
        {
            // for each marker file, deserialize the JSON data and convert it to a task item
            var signTypeItems = new List<Tuple<string, SignTypeItem>>();

            foreach (var markerFile in MarkerFiles)
            {
                if (!File.Exists(markerFile.ItemSpec))
                {
                    Log.LogError("The specified marker file '{0}' doesn't exist.", markerFile.ItemSpec);
                    return false;
                }

                using (var streamReader = new StreamReader(File.OpenRead(markerFile.ItemSpec)))
                {
                    using (var jsonReader = new JsonTextReader(streamReader))
                    {
                        JsonSerializer jsonSerializer = new JsonSerializer();
                        var signTypeItem = jsonSerializer.Deserialize<SignTypeItem>(jsonReader);

                        // the ItemSpec should be the name of the file to sign.  by convention the marker
                        // file is the full path to the file plus a marker extension, so strip the extension.
                        var itemSpec = markerFile.ItemSpec.Substring(0, markerFile.ItemSpec.LastIndexOf('.'));
                        signTypeItems.Add(new Tuple<string, SignTypeItem>(itemSpec, signTypeItem));
                    }
                }
            }

            SigningMetadata = new ITaskItem[signTypeItems.Count];
            for (int i = 0; i < signTypeItems.Count; ++i)
            {
                var taskItem = new TaskItem(signTypeItems[i].Item1);

                if (!string.IsNullOrEmpty(signTypeItems[i].Item2.Authenticode))
                    taskItem.SetMetadata("Authenticode", signTypeItems[i].Item2.Authenticode);
                if (!string.IsNullOrEmpty(signTypeItems[i].Item2.StrongName))
                    taskItem.SetMetadata("StrongName", signTypeItems[i].Item2.StrongName);

                SigningMetadata[i] = taskItem;
            }

            return true;
        }
    }
}
