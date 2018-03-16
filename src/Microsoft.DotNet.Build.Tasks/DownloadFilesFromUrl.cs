using Microsoft.Build.Framework;
using System;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class DownloadFilesFromUrl : BuildTask
    {
        /// <summary>
        /// The items to download.
        /// Url and DestinationFile are required in the item's metadata, DestinationDir is optional.
        /// </summary>
        [Required]
        public ITaskItem[] Items { get; set; }

        /// <summary>
        /// The destination directory for all the items to be downloaded.
        /// This parameter can be overriden per item, if an item has DestinationDir in it's metadata, we will take that directory.
        /// If both, the item's and this property are empty, the items will be downloaded to the working directory.
        /// </summary>
        public string DestinationDir { get; set; }

        /// <summary>
        /// The default is to fail on error, set this to true if you want to just warn.
        /// </summary>
        public bool TreatErrorsAsWarnings { get; set; }

        /// <summary>
        /// The list of files created. It is not guaranted that all the input items will be successfully downloaded
        /// when TreatErrorsAsWarings is set to true.
        /// </summary>
        [Output]
        public ITaskItem[] FilesCreated { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        private async Task<bool> ExecuteAsync()
        {
            if (Items == null || Items.Length <= 0)
            {
                return true;
            }

            var filesCreated = new List<ITaskItem>();
            using (HttpClient client = new HttpClient(GetHttpHandler()))
            {
                foreach (var item in Items)
                {
                    string downloadSource = item.GetMetadata("Url");
                    if (!Uri.IsWellFormedUriString(downloadSource, UriKind.Absolute))
                    {
                        if (TreatErrorsAsWarnings)
                        {
                            Log.LogWarning($"Item {item.ItemSpec} Url is not a valid url.");
                            continue;
                        }
                        else
                        {
                            Log.LogError($"Item {item.ItemSpec} Url is not a valid url.");
                            return false;
                        }
                    }

                    Uri downloadUri = new Uri(downloadSource);
                    string fileName = item.GetMetadata("DestinationFile");
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        // we use absolute path in case the url has query string (?param=blah) to remove them before trying to get the file name.
                        fileName = Path.GetFileName(downloadUri.AbsolutePath);

                        if (string.IsNullOrEmpty(fileName))
                        {
                            if (TreatErrorsAsWarnings)
                            {
                                Log.LogWarning($"Item {item.ItemSpec} DestinationFile metadata was empty, tried getting the name from the url but ended up with empty.");
                                continue;
                            }
                            else
                            {
                                Log.LogError($"Item {item.ItemSpec} DestinationFile metadata was empty, tried getting the name from the url but ended up with empty.");
                                return false;
                            }
                        }
                    }

                    string destinationDirectory = item.GetMetadata("DestinationDir");
                    if (string.IsNullOrWhiteSpace(destinationDirectory))
                    {
                        destinationDirectory = DestinationDir;
                    }

                    try
                    {
                        string destinationFullPath = fileName;
                        if (!string.IsNullOrWhiteSpace(destinationDirectory))
                        {
                            Directory.CreateDirectory(destinationDirectory);
                            destinationFullPath = Path.Combine(destinationDirectory, fileName);
                        }

                        Log.LogMessage(MessageImportance.Normal, $"Downloading {downloadSource} -> {destinationFullPath}");

                        using (Stream responseStream = await client.GetStreamAsync(downloadUri))
                        {
                            using (Stream destinationStream = File.OpenWrite(destinationFullPath))
                            {
                                await responseStream.CopyToAsync(destinationStream);
                                TaskItem createdItem = new TaskItem(destinationFullPath);
                                item.CopyMetadataTo(createdItem);
                                filesCreated.Add(createdItem);
                                Log.LogMessage(MessageImportance.Normal, $"Finished downloading: {downloadSource}");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (TreatErrorsAsWarnings)
                        {
                            Log.LogWarning($"Downloading {downloadSource} failed with exception: ");
                            Log.LogWarningFromException(e, showStackTrace: true);
                        }
                        else
                        {
                            Log.LogError($"Downloading {downloadSource} failed with exception: ");
                            Log.LogErrorFromException(e, showStackTrace: true);
                            return false;
                        }
                    }
                }
            }

            FilesCreated = filesCreated.ToArray();

            return !Log.HasLoggedErrors;
        }

        private static HttpClientHandler GetHttpHandler()
        {
            HttpClientHandler handler = new HttpClientHandler();
#if !net45
            handler.DefaultProxyCredentials = CredentialCache.DefaultCredentials;
#else
            handler.Proxy = WebRequest.DefaultWebProxy;

            if (handler.Proxy != null)
                handler.Proxy.Credentials = CredentialCache.DefaultCredentials;
#endif // net45

            return handler;
        }
    }
}
