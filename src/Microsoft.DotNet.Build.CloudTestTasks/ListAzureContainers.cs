// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public sealed class ListAzureContainers : AzureConnectionStringBuildTask
    {
        /// <summary>
        /// Prefix of Azure containers desired to return;
        /// </summary>
        public string Prefix { get; set; } = String.Empty;

        /// <summary>
        /// An item group of container names to download.  
        /// </summary>
        [Output]
        public ITaskItem[] ContainerNames { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            ParseConnectionString();
            // If the connection string AND AccountKey & AccountName are provided, error out.
            if (Log.HasLoggedErrors)
            {
                return false;
            }

            Log.LogMessage(MessageImportance.Normal, "List of Azure containers in storage account '{0}'.", AccountName);
            string url = string.Format("https://{0}.blob.core.windows.net/?comp=list", AccountName);

            Log.LogMessage(MessageImportance.Low, "Sending request to list containers in account '{0}'.", AccountName);
            List<ITaskItem> discoveredContainers = new List<ITaskItem>();
            using (HttpClient client = new HttpClient())
            {
                string nextMarker = string.Empty;
                try
                {
                    var createRequest = AzureHelper.RequestMessage("GET", url, AccountName, AccountKey);

                    XmlDocument responseFile;
                    using (HttpResponseMessage response = await AzureHelper.RequestWithRetry(Log, client, createRequest))
                    {
                        responseFile = new XmlDocument();
                        responseFile.LoadXml(await response.Content.ReadAsStringAsync());
                        XmlNodeList elemList = responseFile.GetElementsByTagName("Name");


                        discoveredContainers.AddRange(from x in elemList.Cast<XmlNode>()
                                                      where x.InnerText.Contains(Prefix)
                                                      select new TaskItem(x.InnerText));

                        nextMarker = responseFile.GetElementsByTagName("NextMarker").Cast<XmlNode>().FirstOrDefault()?.InnerText;
                    }
                    while (!string.IsNullOrEmpty(nextMarker))
                    {
                        string urlNext = string.Format($"{url}&marker={nextMarker}");
                        var nextRequest = AzureHelper.RequestMessage("GET", urlNext, AccountName, AccountKey);
                        using (HttpResponseMessage nextResponse = AzureHelper.RequestWithRetry(Log, client, nextRequest).GetAwaiter().GetResult())
                        {
                            responseFile = new XmlDocument();
                            responseFile.LoadXml(await nextResponse.Content.ReadAsStringAsync());
                            XmlNodeList elemList = responseFile.GetElementsByTagName("Name");

                            discoveredContainers.AddRange(from x in elemList.Cast<XmlNode>()
                                                          where x.InnerText.Contains(Prefix)
                                                          select new TaskItem(x.InnerText));

                            nextMarker = responseFile.GetElementsByTagName("NextMarker").Cast<XmlNode>().FirstOrDefault()?.InnerText;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.LogErrorFromException(e, true);
                }
            }
            ContainerNames = discoveredContainers.ToArray();
            if (ContainerNames.Length == 0)
                Log.LogWarning("No containers were found.");
            else
                Log.LogMessage("Found {0} containers.", ContainerNames.Length);

            return !Log.HasLoggedErrors;
        }
    }
}
