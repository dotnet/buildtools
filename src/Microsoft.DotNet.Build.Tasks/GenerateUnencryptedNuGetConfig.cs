// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GenerateUnencryptedNuGetConfig : BuildTask
    {
        private const string NotImplementedMessage =
            "Unencrypted NuGet.Config functionality is unavailable. Use an encrypted " +
            "NuGet.Config instead. (Only available on desktop framework.)";

        [Required]
        public ITaskItem[] Sources { get; set; }

        [Output]
        public string NuGetConfigContent { get; set; }

        public override bool Execute()
        {
            throw new NotImplementedException(NotImplementedMessage);
        }

        /// <summary>
        /// Create a NuGet.Config XML string set up for the given source items.
        /// </summary>
        public static string CreateXmlString(IEnumerable<ITaskItem> sources)
        {
            var nuGetConfig = new XmlDocument();

            XmlElement configuration = nuGetConfig.CreateElement("configuration");
            nuGetConfig.AppendChild(configuration);

            if (sources != null && sources.Any())
            {
                throw new NotImplementedException(NotImplementedMessage);
            }

            return nuGetConfig.OuterXml;
        }
    }
}
