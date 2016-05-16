﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;

using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public sealed class ListAzureContainers : Task
    {
        /// <summary>
        /// The Azure account name used when creating the connection string.
        /// </summary>
        [Required]
        public string AccountName { get; set; }

        /// <summary>
        /// The Azure account key used when creating the connection string.
        /// </summary>
        [Required]
        public string AccountKey { get; set; }

        /// <summary>
         /// Prefix of Azure containers desired to return;
         /// </summary>
         public string Prefix { get; set; }

        /// <summary>
        /// An item group of blob filenames to download.  
        /// </summary>
        [Output]
        public ITaskItem[] ContainerNames { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            Log.LogMessage(MessageImportance.Normal, "List of Azure containers in storage account '{0}'.", AccountName);
            string url = string.Format("https://{0}.blob.core.windows.net/?comp=list", AccountName);
            
            Log.LogMessage(MessageImportance.Low, "Sending request to list containers in account '{0}'.", AccountName);

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    Func<HttpRequestMessage> createRequest = () =>
                    {
                        DateTime dateTime = DateTime.UtcNow;
                        var request = new HttpRequestMessage(HttpMethod.Get, url);
                        request.Headers.Add(AzureHelper.DateHeaderString, dateTime.ToString("R", CultureInfo.InvariantCulture));
                        request.Headers.Add(AzureHelper.VersionHeaderString, AzureHelper.StorageApiVersion);
                        request.Headers.Add(AzureHelper.AuthorizationHeaderString, AzureHelper.AuthorizationHeader(
                                AccountName,
                                AccountKey,
                                "GET",
                                dateTime,
                                request));
                        return request;
                    };

                    XmlDocument responseFile;
                    using (HttpResponseMessage response = await AzureHelper.RequestWithRetry(Log, client, createRequest))
                    {
                        responseFile = new XmlDocument();
                        responseFile.LoadXml(await response.Content.ReadAsStringAsync());
                        XmlNodeList elemList = responseFile.GetElementsByTagName("Name");

                        ContainerNames = (from x in elemList.Cast<XmlNode>()
                                            where x.InnerText.Contains(Prefix)
                                            select new TaskItem(x.InnerText)).ToArray();

                        if (ContainerNames.Length == 0)
                            Log.LogWarning("No containers were found.");
                        else
                            Log.LogMessage("Found {0} containers.", ContainerNames.Length);
                    }
                }
                catch (Exception e)
                {
                    Log.LogErrorFromException(e, true);
                    return false;
                }
            }

            return true;
        }
    }
}
