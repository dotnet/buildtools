// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class SendToEventHub : Task
    {
        /// <summary>
        /// The Service Bus connection string.
        /// </summary>
        [Required]
        public string ConnectionString { get; set; }

        /// <summary>
        /// The path to the Event Hub.
        /// </summary>
        [Required]
        public string EventHubPath { get; set; }

        /// <summary>
        /// The event data used to form the body stream.
        /// </summary>
        [Required]
        public string EventData { get; set; }

        public override bool Execute()
        {
            using (var streamReader = new StreamReader(EventData))
            {
                EventHubClient client = EventHubClient.CreateFromConnectionString(ConnectionString, EventHubPath);
                client.Send(new EventData(Encoding.UTF8.GetBytes(streamReader.ReadToEnd())));
                Log.LogMessage(MessageImportance.Normal, "Successfully sent notification to event hub path {0}.", EventHubPath);
            }
            return true;
        }
    }
}
