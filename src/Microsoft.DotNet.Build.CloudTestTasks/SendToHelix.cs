// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public sealed class SendToHelix : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// Api endpoint for sending jobs.
        /// e.g. https://helixview.azurewebsites.net/api/jobs
        /// </summary>
        [Required]
        public string ApiEndpoint { get; set; }

        /// <summary>
        /// Access token for API. To obtain, see the profile on the server corresponding to the API endpoint.
        /// e.g. https://helixview.azurewebsites.net/UserProfile
        /// </summary>
        [Required]
        public string AccessToken { get; set; }

        /// <summary>
        /// The event data path used to form the body stream.
        /// </summary>
        [Required]
        public string EventDataPath { get; set; }

        /// <summary>
        /// Once a helix job is started, this is the identifier of that job
        /// </summary>
        [Output]
        public string JobId { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        private async Task<bool> ExecuteAsync()
        {
            string joinCharacter = ApiEndpoint.Contains("?") ? "&" : "?";
            string apiUrl = ApiEndpoint + joinCharacter + "access_token=" + Uri.EscapeDataString(AccessToken);

            Log.LogMessage(MessageImportance.Low, "Posting job to {0}", ApiEndpoint);
            Log.LogMessage(MessageImportance.Low, "Event json is ", EventDataPath);

            using (HttpClient client = new HttpClient())
            {
                const int MaxAttempts = 15;
                // add a bit of randomness to the retry delay
                var rng = new Random();
                int retryCount = MaxAttempts;

                while (true)
                {
                    HttpResponseMessage response = new HttpResponseMessage();

                    try
                    {
                        using (Stream stream = File.OpenRead(EventDataPath))
                        {
                            HttpContent contentStream = new StreamContent(stream);
                            contentStream.Headers.Add("Content-Type", "application/json");
                            response = await client.PostAsync(apiUrl, contentStream);
                        }

                        if (response.IsSuccessStatusCode)
                        {
                            JObject responseObject;
                            using (Stream stream = await response.Content.ReadAsStreamAsync())
                            using (StreamReader streamReader = new StreamReader(stream))
                            using (JsonReader jsonReader = new JsonTextReader(streamReader))
                            {
                                responseObject = JObject.Load(jsonReader);
                            }

                            JobId = (string)responseObject["Name"];
                            if (String.IsNullOrEmpty(JobId))
                            {
                                Log.LogError("Publish to '{0}' did not return a job ID", ApiEndpoint);
                                return false;
                            }

                            Log.LogMessage(MessageImportance.High, "Started Helix job: CorrelationId = {0}", JobId);
                            return true;
                        }
                        else
                        {
                            string responseContent = await response.Content.ReadAsStringAsync();
                            Log.LogWarning($"Helix Api Response: StatusCode {response.StatusCode} {responseContent}");
                        }
                    }
                    // still allow other types of exceptions to tear down the task for now
                    catch (HttpRequestException toLog)
                    {
                        Log.LogWarning("Exception thrown attempting to submit job to Helix:");
                        Log.LogWarningFromException(toLog, true);
                    }

                    if (retryCount-- <= 0)
                    {
                        Log.LogError($"Unable to publish to '{ApiEndpoint}' after {MaxAttempts} retries. Received status code: {response.StatusCode} {response.ReasonPhrase}");
                        return false;
                    }

                    Log.LogWarning("Failed to publish to '{0}', {1} retries remaining", ApiEndpoint, retryCount);
                    int delay = (MaxAttempts - retryCount) * rng.Next(1, 7);
                    await System.Threading.Tasks.Task.Delay(delay * 1000);
                }
            }
        }
    }
}