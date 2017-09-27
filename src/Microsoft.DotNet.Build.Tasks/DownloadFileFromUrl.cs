using Microsoft.Build.Framework;
using System;
using System.IO;
using System.Net.Http;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class DownloadFileFromUrl : BuildTask
    {
        
        /// <summary>
        /// The url to download the file from.
        /// </summary>
        [Required]
        public string DownloadSource { get; set;}
        
        /// <summary>
        /// The directory where the file will be downloaded to.
        /// </summary>
        [Required]
        public string DestinationDirectory { get; set; }

        /// <summary>
        /// The file where the source will be downloaded to.
        /// </summary>
        [Required]
        public string DestinationFileName { get; set; }

        /// <summary>
        /// The default is to emit warnings and not fail, the caller can fail if output parameter DownloadedFile is empty.
        /// Set this to true to fail instead of just warn.
        /// </summary>
        public bool FailOnError { get; set; }

        /// <summary>
        /// The file that was downloaded, if there is an error this will be empty.
        /// </summary>
        [Output]
        public string DownloadedFile { get; set; }

        public override bool Execute()
        {
            try
            {
                Log.LogMessage(MessageImportance.High, $"Downloading {DownloadSource} -> {DestinationFileName}");
                Directory.CreateDirectory(DestinationDirectory);
                string destinationPath = Path.Combine(DestinationDirectory, DestinationFileName);
                using (FileStream stream = File.Create(destinationPath))
                {
                    using (var client = new HttpClient())
                    {
                        using (var result = client.GetAsync(DownloadSource).GetAwaiter().GetResult())
                        {
                            if (result.IsSuccessStatusCode)
                            {
                                var bytes = result.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                                stream.Write(bytes, 0, bytes.Length);
                                DownloadedFile = destinationPath;
                                Log.LogMessage(MessageImportance.High, $"Finished downloading: {DownloadSource}");
                            }
                            else
                            {
                                if (FailOnError)
                                {
                                    Log.LogError($"Downloading {DownloadSource} failed with status code: {result.StatusCode}");                                    
                                }
                                else
                                {
                                    Log.LogWarning($"Downloading {DownloadSource} failed with status code: {result.StatusCode}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (FailOnError)
                {
                    Log.LogError($"Downloading {DownloadSource} failed with exception: ");
                    Log.LogErrorFromException(e, showStackTrace: true);
                }
                else
                {
                    Log.LogWarningFromException(e, showStackTrace: true);
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
