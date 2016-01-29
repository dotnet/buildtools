// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Common.Desktop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    /// <summary>
    /// Used to send JSON blobs to DocumentDb.
    /// </summary>
    public class SendJsonToDocumentDb : Task
    {
        /// <summary>
        /// The account key used to connect to DocumentDB.
        /// </summary>
        [Required]
        public string AccountKey { get; set; }

        /// <summary>
        /// The name of the document collection.
        /// </summary>
        [Required]
        public string Collection { get; set; }

        /// <summary>
        /// The name of the document database.
        /// </summary>
        [Required]
        public string Database { get; set; }

        /// <summary>
        /// The ID of the document to be created.
        /// </summary>
        [Required]
        public string DocumentId { get; set; }

        /// <summary>
        /// The DocumentDB endpont URI.
        /// </summary>
        [Required]
        public string EndpointUri { get; set; }

        /// <summary>
        /// The JSON file to be uploaded.
        /// </summary>
        [Required]
        public string JsonFile { get; set; }

        static SendJsonToDocumentDb()
        {
            AssemblyResolver.Enable();
        }

        public override bool Execute()
        {
            // load the JSON file
            JObject json;

            using (StreamReader streamReader = new StreamReader(JsonFile))
            using (JsonTextReader jsonReader = new JsonTextReader(streamReader))
            {
                var jsonSerializer = new JsonSerializer();
                json = (JObject)jsonSerializer.Deserialize(jsonReader);

                // add the document ID to the JSON blob
                json.Add("id", DocumentId);
            }

            using (var con = CreateDocumentDbConnection())
            {
                // for specified document ID check if there is a document for it, if there
                // isn't then create a new one with the JSON blob that was loaded earlier.

                var result = con.Client.CreateDocumentQuery(string.Format("dbs/{0}/colls/{1}", con.Database.Id, con.Collection.Id))
                    .Where(doc => doc.Id == DocumentId)
                    .AsEnumerable()
                    .FirstOrDefault();

                if (result == null)
                {
                    Log.LogMessage(MessageImportance.Normal, "Creating new document with ID '{0}'", DocumentId);
                    con.Client.CreateDocumentAsync(con.Collection.SelfLink, json).Wait();
                }
                else
                {
                    Log.LogWarning("A document with ID '{0}' already exists, no content was uploaded.", DocumentId);
                }
            }

            return true;
        }

        private class DocumentConnection : IDisposable
        {
            public DocumentConnection(Database db, DocumentClient client, DocumentCollection collection)
            {
                Database = db;
                Client = client;
                Collection = collection;
            }

            public Database Database { get; }

            public DocumentClient Client { get; }

            public DocumentCollection Collection { get; }

            public void Dispose()
            {
                Client.Dispose();
            }
        }

        private DocumentConnection CreateDocumentDbConnection()
        {
            var client = new DocumentClient(new Uri(EndpointUri), AccountKey);

            // get the database and if it doesn't exist create it

            Database database = client.CreateDatabaseQuery()
                .Where(db => db.Id == Database)
                .AsEnumerable()
                .FirstOrDefault();

            if (database == null)
            {
                Log.LogMessage(MessageImportance.Low, "The database {0} does not exist, will create it.", Database);
                var task = client.CreateDatabaseAsync(new Database { Id = Database });
                database = task.Result;
            }

            // get the document collection and if it doesn't exist create it

            DocumentCollection collection = client.CreateDocumentCollectionQuery(database.SelfLink)
                .Where(c => c.Id == Collection)
                .AsEnumerable()
                .FirstOrDefault();

            if (collection == null)
            {
                Log.LogMessage(MessageImportance.Low, "The collection {0} does not exist, will create it.", Collection);
                var task = client.CreateDocumentCollectionAsync(database.SelfLink, new DocumentCollection { Id = Collection });
                collection = task.Result;
            }

            Log.LogMessage(MessageImportance.Normal, "Connected to DocumentDB database {0}, collection {1}.", Database, Collection);
            return new DocumentConnection(database, client, collection);
        }
    }
}
