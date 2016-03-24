// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Build.Framework;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.DotNet.Build.CloudTestTasks
{

    public class SendToEventHub : Task
    {

        /// <summary>
        ///     The event data used to form the body stream.
        /// </summary>
        [Required]
        public string EventData { get; set; }

        /// <summary>
        ///     The Event Hub Url 
        /// </summary>
        [Required]
        public string EventHubPath { get; set; }

        /// <summary>
        ///     The Shared Access Key Name of the event hub
        /// </summary>
        [Required]
        public string EventHubSharedAccessKey { get; set; }

        /// <summary>
        ///     The Shared Access Key Name of the event hub
        /// </summary>
        [Required]
        public string EventHubSharedAccessKeyName { get; set; }

        /// <summary>
        ///     The partition key for the event.
        /// </summary>
        public string PartitionKey { get; set; }

        /// <summary>
        ///     Gets or sets the content bytes.
        /// </summary>
        internal byte[] contentBytes { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            using (FileStream fs = new FileStream(EventData, FileMode.Open, FileAccess.Read))
            {
                using (StreamReader streamReader = new StreamReader(fs))
                {
                    using (HttpClient client = new HttpClient())
                    {
                        string url = String.IsNullOrEmpty(PartitionKey) ? EventHubPath + "/messages" : EventHubPath + "/partitions/" + PartitionKey +"/messages";
                        using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, url))
                        {
                            req.Headers.Add(AzureHelper.AuthorizationHeaderString, ConstructSharedAccessToken());
                            
                            contentBytes = Encoding.UTF8.GetBytes(streamReader.ReadToEnd());
                            using (Stream postStream = new MemoryStream())
                            {
                                postStream.Write(this.contentBytes, 0, this.contentBytes.Length);
                                postStream.Seek(0, SeekOrigin.Begin);
                                StreamContent contentStream = new StreamContent(postStream);
                                req.Content = contentStream;

                                Log.LogMessage(MessageImportance.High, "Sending {0} to event hub {1}", EventData, url);

                                using (HttpResponseMessage response = await client.SendAsync(req))
                                {
                                    this.Log.LogMessage(MessageImportance.Normal, "Received response to send event to event hub");

                                    if (response.StatusCode != HttpStatusCode.Created)
                                    {
                                        this.Log.LogError(
                                            "Failed to send event to event hub: StatusCode:{0} Response:{1}",
                                            response.StatusCode,
                                            await response.Content.ReadAsStringAsync());
                                        return false;
                                    }
                                }
                                Log.LogMessage(
                                    MessageImportance.Normal,
                                    "Successfully sent notification to event hub path {0}.",
                                    EventHubPath);
                            }

                        }
                    }
                }
            }

            return true;
        }

        private string ConstructSharedAccessToken()
        {
            //Time in milliseconds from Jan 1 1970 is expected by the API, and we want to allow the ability for this task to send for up to 20 minutes from now
            int expiry = (int)DateTime.UtcNow.AddMinutes(20).Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            string stringToSign = WebUtility.UrlEncode(this.EventHubPath) + "\n" + expiry;
            string signature;
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(this.EventHubSharedAccessKey)))
            {
                signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            }
            string sharedAccessToken = "SharedAccessSignature sr=" + WebUtility.UrlEncode(this.EventHubPath) + "&sig="
                           + WebUtility.UrlEncode(signature) + "&se=" + expiry + "&skn=" + this.EventHubSharedAccessKeyName;
            return sharedAccessToken;
        }
    }
}