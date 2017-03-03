using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using System;
using System.IO;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public class ValidateWorkItems : Task
    {
        private List<ITaskItem> processedWorkItems = new List<ITaskItem>();

        /// <summary>
        /// Helix work items to be validated.
        /// Checks include making sure minimum metadata is present and that the path is relative to the supplied root path.
        /// </summary>
        public ITaskItem[] WorkItems { get; set; }

        [Output]
        public ITaskItem [] ProcessedWorkItems { get; set; }

        /// <summary>
        /// While we're validating, may as well calculate the relative blob path
        /// Since we'd also like to be able to check that the path makes sense.
        /// Ignore this check if the item already has this metadata.
        /// </summary>
        public String WorkItemArchiveRoot { get; set; }

        private readonly string[] requiredMetadataFields = { "TimeoutInSeconds", "Command", "PayloadFile" };

        public override bool Execute()
        {
            bool success = true;
            foreach (ITaskItem workItem in WorkItems)
            {
                success &= ProcessWorkItem(workItem);
            }
            ProcessedWorkItems = processedWorkItems.ToArray();
            Log.LogMessage(MessageImportance.Low, $"Examined {WorkItems.Length} workitems.  Success: {success}");

            return success;
        }

        private bool ProcessWorkItem(ITaskItem workItem)
        {
            bool validWorkItem = true;
            // For now, we'll just make sure the required fields are present.  
            // Later, we could prevent extra stuff from going in, but that's 
            // not necessary as we create the processed work items off a specific set.
            foreach (string requiredField in requiredMetadataFields)
            {
                if (string.IsNullOrEmpty(workItem.GetMetadata(requiredField)))
                {
                    Log.LogError($"Work item '{workItem.ItemSpec}' is missing field {requiredField}");
                    validWorkItem = false;
                }
            }

            if (string.IsNullOrEmpty(workItem.GetMetadata("RelativeBlobPath")))
            {
                workItem.SetMetadata("RelativeBlobPath", GetRelativeFilePath(WorkItemArchiveRoot, workItem.GetMetadata("PayloadFile")));
            }
            // We could easily choose to not include invalid work items here, then warn instead of error.
            processedWorkItems.Add(workItem);
            return validWorkItem;
        }

        private string GetRelativeFilePath(string root, string path)
        {
            string fullRoot = Path.GetFullPath(root);
            string fullPath = Path.GetFullPath(path);

            if (fullPath.StartsWith(fullRoot))
            {
                string relativePath = fullPath.Substring(fullRoot.Length);
                relativePath = relativePath.Replace('\\', '/');
                // Trim off the slash, this confuses blob storage and makes a folder named "/"
                if (relativePath.Length > 0 && relativePath.StartsWith("/"))
                {
                    relativePath = relativePath.Substring(1);
                }
                return relativePath;                
            }
            else
            {
                Log.LogWarning($"{path} is not contained under {root}!");
                return Path.GetFileName(path);
            }
        }
    }
}
