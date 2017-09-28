using Microsoft.Build.Framework;
using System;
using System.IO;
using System.Net.Http;
using System.Net;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class DownloadFileFromUrl : BuildTask
    {
        /// <summary>
        /// The url to download the file from.
        /// </summary>
        [Required]
        public string DownloadSource { get; set; }
        
        /// <summary>
        /// The directory where the file will be downloaded to.
        /// </summary>
        [Required]
        public string DestinationFile { get; set; }

        /// <summary>
        /// The default is to fail on error, set this to true if you want to just warn.
        /// </summary>
        public bool TreatErrorsAsWarnings { get; set; }

        public override bool Execute()
        {
            try
            {
                Log.LogMessage(MessageImportance.High, $"Downloading {DownloadSource} -> {DestinationFile}");
                var directory = Path.GetDirectoryName(DestinationFile);
                Directory.CreateDirectory(directory);
                using (FileStream stream = File.Create(DestinationFile))
                {
                    using (var handler = GetHttpHandler())
                    {
                        using (var client = new HttpClient(handler))
                        {
                            using (var result = client.GetAsync(DownloadSource).GetAwaiter().GetResult())
                            {
                                if (result.IsSuccessStatusCode)
                                {
                                    result.Content.CopyToAsync(stream).GetAwaiter().GetResult();
                                    Log.LogMessage(MessageImportance.High, $"Finished downloading: {DownloadSource}");
                                }
                                else
                                {
                                    if (File.Exists(DestinationFile))
                                    {
                                        // If we fail to download, we want to do cleanup to not leave empty files.
                                        File.Delete(DestinationFile);
                                    }

                                    if (TreatErrorsAsWarnings)
                                    {
                                        Log.LogWarning($"Downloading {DownloadSource} failed with status code: {result.StatusCode}");
                                    }
                                    else
                                    {
                                        Log.LogError($"Downloading {DownloadSource} failed with status code: {result.StatusCode}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (TreatErrorsAsWarnings)
                {
                    Log.LogWarningFromException(e, showStackTrace: true);
                }
                else
                {
                    Log.LogError($"Downloading {DownloadSource} failed with exception: ");
                    Log.LogErrorFromException(e, showStackTrace: true);
                }
            }

            return !Log.HasLoggedErrors;
        }

        public HttpClientHandler GetHttpHandler()
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
