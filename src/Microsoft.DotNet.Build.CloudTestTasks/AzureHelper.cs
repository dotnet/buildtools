// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public static class AzureHelper
    {
        /// <summary>
        ///     The storage api version.
        /// </summary>
        public static readonly string StorageApiVersion = "2015-04-05";

        public const string DateHeaderString = "x-ms-date";

        public const string VersionHeaderString = "x-ms-version";

        public const string AuthorizationHeaderString = "Authorization";

        public enum SasAccessType
        {
            Read, 
            Write,
        };

        public static string AuthorizationHeader(
            string storageAccount, 
            string storageKey, 
            string method, 
            DateTime now,
            HttpRequestMessage request, 
            string ifMatch = "", 
            string contentMD5 = "", 
            string size = "", 
            string contentType = "")
        {
            string stringToSign = string.Format(
                "{0}\n\n\n{1}\n{5}\n{6}\n\n\n{2}\n\n\n\n{3}{4}", 
                method, 
                (size == string.Empty) ? string.Empty : size, 
                ifMatch, 
                GetCanonicalizedHeaders(request), 
                GetCanonicalizedResource(request.RequestUri, storageAccount), 
                contentMD5, 
                contentType);

            byte[] signatureBytes = Encoding.UTF8.GetBytes(stringToSign);
            string authorizationHeader;
            using (HMACSHA256 hmacsha256 = new HMACSHA256(Convert.FromBase64String(storageKey)))
            {
                authorizationHeader = "SharedKey " + storageAccount + ":"
                                      + Convert.ToBase64String(hmacsha256.ComputeHash(signatureBytes));
            }

            return authorizationHeader;
        }

        public static string CreateContainerSasToken(
            string accountName, 
            string containerName, 
            string key, 
            SasAccessType accessType, 
            int validityTimeInDays)
        {
            string signedPermissions = string.Empty;
            switch (accessType)
            {
                case SasAccessType.Read:
                    signedPermissions = "r";
                    break;
                case SasAccessType.Write:
                    signedPermissions = "wdl";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(accessType), accessType, "Unrecognized value");
            }

            string signedStart = DateTime.UtcNow.ToString("O");
            string signedExpiry = DateTime.UtcNow.AddDays(validityTimeInDays).ToString("O");
            string canonicalizedResource = "/blob/" + accountName + "/" + containerName;
            string signedIdentifier = string.Empty;
            string signedVersion = StorageApiVersion;

            string stringToSign = ConstructServiceStringToSign(
                signedPermissions, 
                signedVersion, 
                signedExpiry, 
                canonicalizedResource, 
                signedIdentifier, 
                signedStart);

            byte[] signatureBytes = Encoding.UTF8.GetBytes(stringToSign);
            string signature;
            using (HMACSHA256 hmacSha256 = new HMACSHA256(Convert.FromBase64String(key)))
            {
                signature = Convert.ToBase64String(hmacSha256.ComputeHash(signatureBytes));
            }

            string sasToken = string.Format(
                "?sv={0}&sr={1}&sig={2}&st={3}&se={4}&sp={5}", 
                WebUtility.UrlEncode(signedVersion), 
                WebUtility.UrlEncode("c"), 
                WebUtility.UrlEncode(signature), 
                WebUtility.UrlEncode(signedStart), 
                WebUtility.UrlEncode(signedExpiry), 
                WebUtility.UrlEncode(signedPermissions));

            return sasToken;
        }

        public static string GetCanonicalizedHeaders(HttpRequestMessage request)
        {
            StringBuilder sb = new StringBuilder();
            List<string> headerNameList = (from headerName in request.Headers
                                           where
                                               headerName.Key.ToLowerInvariant()
                                               .StartsWith("x-ms-", StringComparison.Ordinal)
                                           select headerName.Key.ToLowerInvariant()).ToList();
            headerNameList.Sort();
            foreach (string headerName in headerNameList)
            {
                StringBuilder builder = new StringBuilder(headerName);
                string separator = ":";
                foreach (string headerValue in GetHeaderValues(request.Headers, headerName))
                {
                    string trimmedValue = headerValue.Replace("\r\n", string.Empty);
                    builder.Append(separator);
                    builder.Append(trimmedValue);
                    separator = ",";
                }

                sb.Append(builder);
                sb.Append("\n");
            }

            return sb.ToString();
        }

        public static string GetCanonicalizedResource(Uri address, string accountName)
        {
            StringBuilder str = new StringBuilder();
            StringBuilder builder = new StringBuilder("/");
            builder.Append(accountName);
            builder.Append(address.AbsolutePath);
            str.Append(builder);
            Dictionary<string, HashSet<string>> queryKeyValues = ExtractQueryKeyValues(address);
            Dictionary<string, HashSet<string>> dictionary = GetCommaSeparatedList(queryKeyValues);

            foreach (KeyValuePair<string, HashSet<string>> pair in dictionary.OrderBy(p => p.Key))
            {
                StringBuilder stringBuilder = new StringBuilder(string.Empty);
                stringBuilder.Append(pair.Key + ":");
                string commaList = string.Join(",", pair.Value);
                stringBuilder.Append(commaList);
                str.Append("\n");
                str.Append(stringBuilder);
            }

            return str.ToString();
        }

        public static List<string> GetHeaderValues(HttpRequestHeaders headers, string headerName)
        {
            List<string> list = new List<string>();
            IEnumerable<string> values;
            headers.TryGetValues(headerName, out values);
            if (values != null)
            {
                list.Add((values.FirstOrDefault() ?? string.Empty).TrimStart(null));
            }

            return list;
        }

        private static string ConstructServiceStringToSign(
            string signedPermissions, 
            string signedVersion, 
            string signedExpiry, 
            string canonicalizedResource, 
            string signedIdentifier, 
            string signedStart, 
            string signedIP = "", 
            string signedProtocol = "", 
            string rscc = "", 
            string rscd = "", 
            string rsce = "", 
            string rscl = "", 
            string rsct = "")
        {
            // constructing string to sign based on spec in https://msdn.microsoft.com/en-us/library/azure/dn140255.aspx
            var stringToSign = string.Join(
                "\n", 
                signedPermissions, 
                signedStart, 
                signedExpiry, 
                canonicalizedResource, 
                signedIdentifier, 
                signedIP, 
                signedProtocol, 
                signedVersion, 
                rscc, 
                rscd, 
                rsce, 
                rscl, 
                rsct);
            return stringToSign;
        }

        private static Dictionary<string, HashSet<string>> ExtractQueryKeyValues(Uri address)
        {
            Dictionary<string, HashSet<string>> values = new Dictionary<string, HashSet<string>>();
            Regex newreg = new Regex(@"\?(\w+)\=([\w|\=]+)|\&(\w+)\=([\w|\=]+)");
            MatchCollection matches = newreg.Matches(address.Query);
            foreach (Match match in matches)
            {
                string key, value;
                if (!string.IsNullOrEmpty(match.Groups[1].Value))
                {
                    key = match.Groups[1].Value;
                    value = match.Groups[2].Value;
                }
                else
                {
                    key = match.Groups[3].Value;
                    value = match.Groups[4].Value;
                }

                HashSet<string> setOfValues;
                if (values.TryGetValue(key, out setOfValues))
                {
                    setOfValues.Add(value);
                }
                else
                {
                    HashSet<string> newSet = new HashSet<string> { value };
                    values.Add(key, newSet);
                }
            }

            return values;
        }

        private static Dictionary<string, HashSet<string>> GetCommaSeparatedList(
            Dictionary<string, HashSet<string>> queryKeyValues)
        {
            Dictionary<string, HashSet<string>> dictionary = new Dictionary<string, HashSet<string>>();

            foreach (string queryKeys in queryKeyValues.Keys)
            {
                HashSet<string> setOfValues;
                queryKeyValues.TryGetValue(queryKeys, out setOfValues);
                List<string> list = new List<string>();
                list.AddRange(setOfValues);
                list.Sort();
                string commaSeparatedValues = string.Join(",", list);
                string key = queryKeys.ToLowerInvariant();
                HashSet<string> setOfValues2;
                if (dictionary.TryGetValue(key, out setOfValues2))
                {
                    setOfValues2.Add(commaSeparatedValues);
                }
                else
                {
                    HashSet<string> newSet = new HashSet<string> { commaSeparatedValues };
                    dictionary.Add(key, newSet);
                }
            }

            return dictionary;
        }
    }
}