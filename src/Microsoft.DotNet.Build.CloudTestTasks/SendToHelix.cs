// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public sealed class SendToHelix : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// Api endpoint for sending jobs.
        /// e.g. https://helix.dot.net/api/2017-03-34/jobs
        /// </summary>
        [Required]
        public string ApiEndpoint { get; set; }

        /// <summary>
        /// Access token for API. To obtain, see the profile on the server corresponding to the API endpoint.
        /// e.g. https://helix.dot.net/UserProfile
        /// </summary>
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
            Log.LogWarning("Obselete: Please use the SendJobsToHelix task");

            string apiUrl = ApiEndpoint;
            if (!String.IsNullOrEmpty(AccessToken))
            {
                string joinCharacter = ApiEndpoint.Contains("?") ? "&" : "?";
                apiUrl = ApiEndpoint + joinCharacter + "access_token=" + Uri.EscapeDataString(AccessToken);
            }

            Log.LogMessage(MessageImportance.Normal, "Posting job to {0}", ApiEndpoint);
            Log.LogMessage(MessageImportance.Low, "Event json is ", EventDataPath);

            using (HttpClient client = new HttpClient())
            {
                const int MaxAttempts = 15;
                // add a bit of randomness to the retry delay
                var rng = new Random();
                int retryCount = MaxAttempts;

                while (true)
                {
                    using (Stream postStream = File.OpenRead(EventDataPath))
                    {
                        try
                        {
                            HttpContent contentStream = new StreamContent(postStream);
                            contentStream.Headers.Add("Content-Type", "application/json");
                            using (HttpResponseMessage response = await client.PostAsync(apiUrl, contentStream))
                            {
                                if (response.IsSuccessStatusCode)
                                {
                                    JObject responseObject = new JObject();
                                    using (Stream responseStream = await response.Content.ReadAsStreamAsync())
                                    using (StreamReader streamReader = new StreamReader(responseStream))
                                    {
                                        string jsonResponse = streamReader.ReadToEnd();
                                        try
                                        {
                                            using (JsonReader jsonReader = new JsonTextReader(new StringReader(jsonResponse)))
                                            {
                                                responseObject = JObject.Load(jsonReader);
                                            }
                                        }
                                        catch
                                        {
                                            Log.LogWarning($"Hit exception attempting to parse JSON response.  Raw response string: {Environment.NewLine} {jsonResponse}");
                                        }
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
                        }
                        // still allow other types of exceptions to tear down the task for now
                        catch (HttpRequestException toLog)
                        {
                            Log.LogWarning("Exception thrown attempting to submit job to Helix:");
                            Log.LogWarningFromException(toLog, true);
                        }
                        // If this method supported task cancellation, we'd need to make sure the CancellationToken came from the Http Client... but it doesn't.
                        catch (TaskCanceledException)
                        {
                            Log.LogWarning($"Http Client timeout posting to Helix, will retry up to {MaxAttempts} times");
                        }

                        if (retryCount-- <= 0)
                        {
                            Log.LogError($"Failed to to publish to '{ApiEndpoint}' after {MaxAttempts} retries.");
                            return false;
                        }

                        Log.LogWarning("Failed to publish to '{0}', {1} retries remaining", ApiEndpoint, retryCount);
                        int delay = (MaxAttempts - retryCount) * rng.Next(1, 12);
                        await Task.Delay(delay * 1000);
                    }
                }
            }
        }
    }
}