// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using MSBuild = Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.Versioning;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using Microsoft.DotNet.Build.CloudTestTasks;
using System;
using NuGet.Packaging;
using System.Text.RegularExpressions;
using System.Threading;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public sealed class BlobFeed
    {
        private MSBuild.TaskLoggingHelper Log;

        public string AccountName { get; set; }

        public string AccountKey { get; set; }

        public string ContainerName { get; set; }

        public string RelativePath { get; set; }

        public string IndexDirectory { get; set; }

        bool IsPublic { get; set; }

        string FeedUrl { get; set; }

        private static readonly CancellationTokenSource TokenSource = new CancellationTokenSource();
        private static readonly CancellationToken CancellationToken = TokenSource.Token;

        public BlobFeed(string accountName, string accountKey, string containerName, string relativePath, string feedUrl, string indexDirectory, MSBuild.TaskLoggingHelper loggingHelper, bool isPublic)
        {
            AccountName = accountName;
            AccountKey = accountKey;
            ContainerName = containerName;
            IndexDirectory = indexDirectory;
            RelativePath = relativePath;
            FeedUrl = feedUrl;
            IsPublic = isPublic;
            Log = loggingHelper;
        }

        public string FeedContainerUrl
        {
            get
            {
                return AzureHelper.GetContainerRestUrl(AccountName, ContainerName);
            }
        }

        public async Task<bool> CheckIfFeedExists()
        {
            string url = $"{FeedContainerUrl}?restype=container";
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Clear();
                var request = AzureHelper.RequestMessage("GET", url, AccountName, AccountKey).Invoke();
                using (HttpResponseMessage response = await client.SendAsync(request))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        Log.LogMessage(
                            MessageImportance.Low,
                            $"Container {ContainerName} exists for {AccountName}: Status Code:{response.StatusCode} Status Desc: {await response.Content.ReadAsStringAsync()}");
                    }
                    else
                    {
                        Log.LogMessage(
                            MessageImportance.Low,
                            $"Container {ContainerName} does not exist for {AccountName}: Status Code:{response.StatusCode} Status Desc: {await response.Content.ReadAsStringAsync()}");
                    }
                    return response.IsSuccessStatusCode;
                }
            }
        }

        public async Task<bool> CheckIfBlobExists(string blobPath)
        {
            string url = $"{FeedContainerUrl}/{blobPath}?comp=metadata";
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Clear();
                var request = AzureHelper.RequestMessage("GET", url, AccountName, AccountKey).Invoke();
                using (HttpResponseMessage response = await client.SendAsync(request))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        Log.LogMessage(
                            MessageImportance.Low,
                            $"Blob {blobPath} exists for {AccountName}: Status Code:{response.StatusCode} Status Desc: {await response.Content.ReadAsStringAsync()}");
                    }
                    else
                    {
                        Log.LogMessage(
                            MessageImportance.Low,
                            $"Blob {blobPath} does not exist for {AccountName}: Status Code:{response.StatusCode} Status Desc: {await response.Content.ReadAsStringAsync()}");
                    }
                    return response.IsSuccessStatusCode;
                }
            }
        }

        public string GenerateIndexes(IEnumerable<string> items, string relativePath)
        {
            Log.LogMessage(MessageImportance.Low, $"START generating indexes for {relativePath}");
            foreach (var package in items)
            {
                Tuple<string, NuGetVersion> blobPath = GetNugetPackageVersion(package);
                GeneratePackageServiceIndex(Path.Combine(relativePath, blobPath.Item1), new List<string>() { blobPath.Item2.ToFullString() });
            }
            Log.LogMessage(MessageImportance.Low, $"DONE generating indexes for {relativePath}");
            return IndexDirectory;
        }

        public Tuple<string, NuGetVersion> GetNugetPackageVersion(string package)
        {
            using (var reader = new PackageArchiveReader(package))
            using (var nuspecStream = reader.GetNuspec())
            {
                NuspecReader nuspec = new NuspecReader(nuspecStream);
                return new Tuple<string, NuGetVersion>(nuspec.GetId(), nuspec.GetVersion());
            }
        }
        public string CalculateRelativeUploadPath(string item, string relativePath)
        {
            string relativeBlobPath = CalculateBlobPath(item, relativePath);
            if (Path.GetExtension(item) == ".nupkg")
            {
                Tuple<string, NuGetVersion> nugetPackage = GetNugetPackageVersion(item);
                return relativeBlobPath.Substring(0, relativeBlobPath.IndexOf(nugetPackage.Item2.ToFullString()) - 1);
            }
            else
            {
                return relativeBlobPath.Substring(0, relativeBlobPath.IndexOf(Path.GetFileName(item)) - 1);
            }
        }

        public string CalculateBlobPath(string item, string relativePath)
        {
            string blobPath = string.Empty;
            if (Path.GetExtension(item) == ".nupkg")
            {
                Tuple<string, NuGetVersion> nugetPackage = GetNugetPackageVersion(item);
                blobPath = Path.Combine(Path.Combine(relativePath, Path.Combine(nugetPackage.Item1, nugetPackage.Item2.ToFullString())), Path.GetFileName(item)).ToLowerInvariant();
            }
            else
            {
                blobPath = Path.Combine(relativePath.ToLowerInvariant(), Path.GetFileName(item));
            }
            return blobPath.Replace("\\", "/");
        }

        public string GeneratePackageServiceIndex(string redirectUrl, IEnumerable<string> values)
        {
            string pathToTempFolder = Path.Combine(IndexDirectory, redirectUrl);
            if (Directory.Exists(pathToTempFolder))
            {
                Directory.Delete(pathToTempFolder, true);
            }
            Directory.CreateDirectory(pathToTempFolder);
            string packageIndexJsonLocation = Path.Combine(pathToTempFolder, "index.json");
            using (var streamWriter = new StreamWriter(File.OpenWrite(packageIndexJsonLocation)))
            using (JsonTextWriter writer = new JsonTextWriter(streamWriter))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("versions");
                writer.WriteStartArray();
                foreach (string version in values)
                {
                    writer.WriteRawValue($"\"{version}\"");
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            return packageIndexJsonLocation;
        }

        private string GenerateRootServiceIndex(string redirectUrl)
        {
            string pathToTempFolder = Path.Combine(IndexDirectory, redirectUrl);
            if (Directory.Exists(pathToTempFolder))
            {
                Directory.Delete(pathToTempFolder, true);
            }
            Directory.CreateDirectory(pathToTempFolder);
            string rootIndexJsonLocation = Path.Combine(pathToTempFolder, "index.json");
            using (FileStream fs = File.OpenWrite(rootIndexJsonLocation))
            using (var streamWriter = new StreamWriter(fs))
            using (JsonTextWriter writer = new JsonTextWriter(streamWriter))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("version");
                writer.WriteRawValue("\"3.0.0\"");
                writer.WritePropertyName("resources");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WritePropertyName("@id");
                writer.WriteRawValue($"\"{FeedUrl}\"");
                writer.WritePropertyName("@type");
                writer.WriteRawValue("\"PackageBaseAddress/3.0.0\"");
                writer.WritePropertyName("comment");
                writer.WriteRawValue("\"Base URL of Azure storage where NuGet package registration info for intermediaries is stored.\"");
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WritePropertyName("@context");
                writer.WriteStartObject();
                writer.WritePropertyName("@vocab");
                writer.WriteRawValue("\"http://schema.nuget.org/schema#\"");
                writer.WritePropertyName("comment");
                writer.WriteRawValue("\"http://www.w3.org/2000/01/rdf-schema#comment\"");
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            return rootIndexJsonLocation;
        }

        public bool IsSanityChecked(IEnumerable<string> items)
        {
            Log.LogMessage(MessageImportance.Low, $"START checking sanitized items for feed");
            foreach (var item in items)
            {
                if (items.Any(s => Path.GetExtension(item) != ".nupkg"))
                {
                    Log.LogError($"{item} is not a nupkg");
                    return false;
                }
            }
            List<string> duplicates = items.GroupBy(x => x)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key).ToList();
            if (duplicates.Count > 0)
            {
                Log.LogError($"Duplicates found: {duplicates}");
                return false;
            }
            Log.LogMessage(MessageImportance.Low, $"DONE checking for sanitized items for feed");
            return true;
        }

        public async Task<bool> CreateFeedContainer()
        {
            ValidateContainerName(ContainerName);
            string url = $"{FeedContainerUrl}?restype=container";
            using (HttpClient client = new HttpClient())
            {
                List<Tuple<string, string>> additionalHeaders = null;
                if (IsPublic)
                {
                    Tuple<string, string> headerBlobType = new Tuple<string, string>("x-ms-blob-public-access", "blob");
                    additionalHeaders = new List<Tuple<string, string>>() { headerBlobType };
                }
                var createRequest = AzureHelper.RequestMessage("PUT", url, AccountName, AccountKey, additionalHeaders);

                using (HttpResponseMessage response = await AzureHelper.RequestWithRetry(Log, client, createRequest))
                {
                    try
                    {
                        Log.LogMessage(
                            MessageImportance.Low,
                            "Received response to create Container {0}: Status Code: {1} {2}",
                            ContainerName, response.StatusCode, response.Content.ToString());
                    }
                    catch (Exception e)
                    {
                        Log.LogErrorFromException(e, true);
                    }
                }
            }
            string item = GenerateRootServiceIndex(RelativePath);
            string uploadPath = CalculateBlobPath(item, RelativePath);
            Log.LogMessage($"Uploading root index.json to {uploadPath}.");
            try
            {
                UploadClient uploadClient = new UploadClient(Log);
                await
                    uploadClient.UploadBlockBlobAsync(
                        CancellationToken,
                        AccountName,
                        AccountKey,
                        ContainerName,
                        item,
                        uploadPath);
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
            }
            return !Log.HasLoggedErrors;
        }

        private void ValidateContainerName(string container)
        {
            if (container.Length < 3 || container.Length > 63 || !Regex.IsMatch(container, @"^[a-z0-9]+(-[a-z0-9]+)*$"))
                throw new Exception("Container Name is invalid");
        }
    }
}
