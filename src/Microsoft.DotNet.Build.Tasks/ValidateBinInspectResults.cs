// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    // The BinInspect tool we use for signing validation does not support
    // baselining, this task is used to inspect the results and determine
    // if there is an actual failure we care about.
    public class ValidateBinInspectResults : BuildTask
    {
        private const string s_RootElement = "DATA";
        private const string s_RowElement = "ROW";
        private const string s_ErrorElement = "Err";
        private const string s_NameElement = "Full";
        private const string s_ResultElement = "Pass";

        private const string SpecificErrorMetadataName = "SpecificError";

        [Required]
        public string ResultsXml { get; set; }

        // list of Regex's to ignore 
        public ITaskItem[] BaselineFiles { get; set; }

        [Output]
        public ITaskItem[] ErrorResults { get; set; }

        public override bool Execute()
        {
            XDocument xdoc = XDocument.Load(ResultsXml);

            var rows = xdoc.Descendants(s_RootElement).Descendants(s_RowElement);

            List<ITaskItem> reportedFailures = new List<ITaskItem>();

            // Gather all of the rows with error results
            IEnumerable<XElement> errorRows = rows
                .Where(descendant => descendant
                    .Descendants()
                    .FirstOrDefault(f => f.Name == s_ResultElement)?.Value == "False")
                .ToArray();

            // Filter out baselined files which are stored as regex patterns
            HashSet<XElement> baselineExcludeElements = new HashSet<XElement>();
            if (BaselineFiles != null)
            {
                foreach (var baselineFile in BaselineFiles)
                {
                    string regex = baselineFile.ItemSpec;
                    string specificError = baselineFile.GetMetadata(SpecificErrorMetadataName);

                    var baselineExcluded = errorRows
                        .Select(row => new
                        {
                            Element = row,
                            FullFile = row.Descendants(s_NameElement).First().Value,
                            Error = row.Descendants(s_ErrorElement).FirstOrDefault()?.Value,
                        })
                        .Where(f => Regex.IsMatch(f.FullFile, regex, RegexOptions.IgnoreCase))
                        .Where(f =>
                            string.IsNullOrEmpty(specificError) ||
                            f.Error.Equals(specificError, StringComparison.OrdinalIgnoreCase));

                    foreach (var baselineExclude in baselineExcluded)
                    {
                        baselineExcludeElements.Add(baselineExclude.Element);
                    }
                }
            }
            // Gather the results with baselined files filtered out
            IEnumerable<XElement> baselinedRows = errorRows.Except(baselineExcludeElements);

            foreach (var filteredRow in baselinedRows)
            {
                ITaskItem item = new TaskItem(filteredRow.Descendants(s_NameElement).First().Value);
                item.SetMetadata("Error", filteredRow.Descendants(s_ErrorElement).First().Value);
                reportedFailures.Add(item);
            }

            ErrorResults = reportedFailures.ToArray();
            foreach (var result in ErrorResults)
            {
                Log.LogError($"{result.ItemSpec} failed with error {result.GetMetadata("Error")}");
            }
            return !Log.HasLoggedErrors;
        }
    }
}
