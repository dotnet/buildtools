// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.VersionTools.Automation;

namespace Microsoft.DotNet.VersionTools.Produces
{
    public class UpdateProducesTask : Microsoft.Build.Utilities.Task
    {
        private VersionsRepoUpdater _versionsRepoUpdater;

        [Required]
        public string AuthToken { get; set; }
        public string User { get; set; }
        public string Email { get; set; }
        [Required]
        public string ProducesJsonFilename { get; set; }
        [Required]
        public string VersionsRepoPath { get; set; }

        public override bool Execute()
        {
            if (!System.IO.File.Exists(ProducesJsonFilename))
            {
                return false;
            }
            GitHubAuth gitHubAuth = new GitHubAuth(AuthToken, User, Email);
            _versionsRepoUpdater = new VersionsRepoUpdater(gitHubAuth, User ?? "dotnet");
            System.Threading.Tasks.Task task = UpdateProducesAsync(ProducesJsonFilename, VersionsRepoPath);
            task.Wait();
            return true;
        }

        public async System.Threading.Tasks.Task UpdateProducesAsync(string producesJsonFilename, string versionsRepoPath)
        {
            string producesFilePath = $"{versionsRepoPath}/produces.json";
            string message = $"Updating produces.json at {versionsRepoPath}";
            string producesContent = System.IO.File.ReadAllText(producesJsonFilename);
            await _versionsRepoUpdater.UpdateGitHubFileAsync(producesFilePath, producesContent, message);
        }
    }
}
