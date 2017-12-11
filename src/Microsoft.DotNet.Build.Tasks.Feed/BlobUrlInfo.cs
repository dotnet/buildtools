using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Auth;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    class BlobUrlInfo
    {
        private const string accountNameandEndpointRegex = @"(?<accountname>[a-z0-9]+)\.(?<endpoint>.+?)";
        private const string containerAndBlobRegex = @"/(?<containername>[^\/]+)/(?<blobpath>.*)";
        public string AccountName { get; set; }

        public string Endpoint { get; set; }

        public string ContainerName { get; set; }

        public string BlobPath { get; set; }

        public bool HasToken { get; set; }

        public Uri Uri
        {
            get
            {
                return new Uri($"https://{AccountName}.{Endpoint}/{ContainerName}/{BlobPath}");
            }
        }

        public BlobUrlInfo(string url)
            : this(new Uri(url))
        { }

        public BlobUrlInfo(Uri uri)
        {
            // Account name is the first element of the hostname.
            string hostName = uri.Host;
            Match hostNameMatch = Regex.Match(hostName, accountNameandEndpointRegex);

            if (hostNameMatch.Success)
            {
                AccountName = hostNameMatch.Groups["accountname"].Value;
                Endpoint = hostNameMatch.Groups["endpoint"].Value;
            }
            else
            {
                throw new ArgumentException(string.Format("Blob URL host name {0} should be of the form <account name>.<endpoint>", hostName));
            }

            String path = uri.AbsolutePath;
            Match containerAndBlobMatch = Regex.Match(path, containerAndBlobRegex);

            if (containerAndBlobMatch.Success)
            {
                ContainerName = containerAndBlobMatch.Groups["containername"].Value;
                BlobPath = containerAndBlobMatch.Groups["blobpath"].Value;
            }
            else
            {
                throw new ArgumentException(string.Format("Blob URL path {0} should have a container and blob path", path));
            }

            // TODO, for authenticated nuget feeds using traditional query strings, we should change this
            // to support recognition of the SAS token
            if (!String.IsNullOrEmpty(uri.Query))
            {
                HasToken = true;
                throw new NotImplementedException("Authenticated SAS token blob URIs is not yet implemented");
            }
        }

        public BlobUrlInfo(string accountName, string containerName, string blobPath)
        {
            AccountName = accountName;
            ContainerName = containerName;
            BlobPath = blobPath;
        }

        public string GetConnectionString(string accountKey)
        {
            return $"DefaultEndpointsProtocol=https;AccountName={AccountName};AccountKey={accountKey};EndpointSuffix=core.windows.net";
        }
    }
}
