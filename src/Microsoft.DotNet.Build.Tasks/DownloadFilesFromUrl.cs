using Microsoft.Build.Framework;
using System;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Collections.Generic;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class DownloadFilesFromUrl : BuildTask
    {
        private static readonly HttpClient s_client = new HttpClient(GetHttpHandler());

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
            var filesCreated = new List<ITaskItem>();
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
                        return ExitWithError($"Item {item.ItemSpec} Url is not a valid url.");
                    }
                }

                Uri downloadUri = new Uri(downloadSource);
                string fileName = item.GetMetadata("DestinationFile");
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    // starts with file://
                    if (downloadUri.IsFile)
                    {
                        fileName = Path.GetFileName(downloadUri.LocalPath);
                    }
                    else
                    {
                        while (downloadSource.EndsWith("/"))
                        {
                            downloadSource = downloadSource.Substring(0, downloadSource.Length - 1);
                        }

                        string[] urlContents = downloadSource.Split('/');

                        // we set the filename to whatever is at the end of the url.
                        // with this we ensure that if the metadata doesn't contain a file name, we always get one.
                        fileName = urlContents[urlContents.Length - 1];

                        if (string.IsNullOrEmpty(fileName))
                        {
                            if (TreatErrorsAsWarnings)
                            {
                                Log.LogWarning($"Item {item.ItemSpec} DestinationFile metadata was empty, we tried getting the name from the url but ended up with empty.");
                                continue;
                            }
                            else
                            {
                                return ExitWithError($"Item {item.ItemSpec} DestinationFile metadata was empty, we tried getting the name from the url but ended up with empty.");
                            }
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

                    using (Stream responseStream = s_client.GetStreamAsync(downloadUri).GetAwaiter().GetResult())
                    {
                        using (Stream destinationStream = File.OpenWrite(destinationFullPath))
                        {
                            responseStream.CopyToAsync(destinationStream).GetAwaiter().GetResult();
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
                        return ExitWithError($"Downloading {downloadSource} failed with exception: ", e);
                    }
                }
            }

            FilesCreated = filesCreated.ToArray();
            s_client.Dispose();

            return !Log.HasLoggedErrors;
        }

        private bool ExitWithError(string errorMessage, Exception e = null)
        {
            s_client.Dispose();
            Log.LogError(errorMessage);

            if (e != null)
            {
                Log.LogErrorFromException(e, showStackTrace: true);
            }

            return false;
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
