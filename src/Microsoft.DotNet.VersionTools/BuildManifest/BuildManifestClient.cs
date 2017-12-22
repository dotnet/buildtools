// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Microsoft.DotNet.VersionTools.BuildManifest
{
    public class BuildManifestClient
    {
        public const string BuildManifestXmlName = "build.xml";

        private readonly IGitHubClient _github;

        public ExponentialRetry Retry { get; set; } = new ExponentialRetry();

        public BuildManifestClient(IGitHubClient githubClient)
        {
            _github = githubClient;
        }

        public async Task<OrchestratedBuildModel> FetchManifestAsync(
            GitHubProject project,
            string @ref,
            string basePath)
        {
            return OrchestratedBuildModel.Parse(await FetchModelXmlAsync(project, @ref, basePath));
        }

        public async Task<SemaphoreModel> FetchSemaphoreAsync(
            GitHubProject project,
            string @ref,
            string basePath,
            string semaphorePath)
        {
            string contents = await _github.GetGitHubFileContentsAsync(
                $"{basePath}/{semaphorePath}",
                project,
                @ref);

            return SemaphoreModel.Parse(semaphorePath, contents);
        }

        public async Task PushNewBuildAsync(
            GitHubProject project,
            string @ref,
            string basePath,
            OrchestratedBuildModel build,
            IEnumerable<SupplementaryUploadRequest> supplementaryUploads,
            string message)
        {
            await Retry.RunAsync(async attempt =>
            {
                string remoteCommit = (await _github.GetReferenceAsync(project, @ref)).Object.Sha;

                Trace.TraceInformation($"Creating update on remote commit: {remoteCommit}");

                IEnumerable<SupplementaryUploadRequest> uploads = supplementaryUploads.NullAsEmpty()
                    .Concat(new[]
                    {
                        new SupplementaryUploadRequest
                        {
                            Path = BuildManifestXmlName,
                            Contents = build.ToXml().ToString()
                        },
                        new SupplementaryUploadRequest
                        {
                            Path = SemaphoreModel.BuildSemaphorePath,
                            Contents = new SemaphoreModel
                            {
                                BuildId = build.Identity.BuildId
                            }.ToFileContent()
                        }
                    })
                    .ToArray();

                return await PushUploadsAsync(project, @ref, basePath, message, remoteCommit, uploads);
            });
        }

        public async Task PushChangeAsync(
            GitHubProject project,
            string @ref,
            string basePath,
            string orchestratedBuildId,
            Action<OrchestratedBuildModel> changeModel,
            IEnumerable<string> semaphorePaths,
            IEnumerable<SupplementaryUploadRequest> supplementaryUploads,
            string message)
        {
            await Retry.RunAsync(async attempt =>
            {
                // Get the current commit. Use this throughout to ensure a clean transaction.
                string remoteCommit = (await _github.GetReferenceAsync(project, @ref)).Object.Sha;

                Trace.TraceInformation($"Creating update on remote commit: {remoteCommit}");

                // This is a subsequent publish step: check to make sure the build id matches.
                XElement remoteModelXml = await FetchModelXmlAsync(project, remoteCommit, basePath);

                OrchestratedBuildModel remoteModel = OrchestratedBuildModel.Parse(remoteModelXml);

                if (orchestratedBuildId != remoteModel.Identity.BuildId)
                {
                    throw new ManifestChangeOutOfDateException(
                        orchestratedBuildId,
                        remoteModel.Identity.BuildId);
                }

                OrchestratedBuildModel modifiedModel = OrchestratedBuildModel.Parse(remoteModelXml);
                changeModel(modifiedModel);

                if (modifiedModel.Identity.BuildId != orchestratedBuildId)
                {
                    throw new ArgumentException(
                        "Change action shouldn't modify BuildId. Changed from " +
                        $"'{orchestratedBuildId}' to '{modifiedModel.Identity.BuildId}'.",
                        nameof(changeModel));
                }

                XElement modifiedModelXml = modifiedModel.ToXml();

                IEnumerable<SupplementaryUploadRequest> uploads = semaphorePaths.NullAsEmpty()
                    .Select(p => new SupplementaryUploadRequest
                    {
                        Path = p,
                        Contents = new SemaphoreModel
                        {
                            BuildId = orchestratedBuildId
                        }.ToFileContent()
                    })
                    .Concat(supplementaryUploads.NullAsEmpty())
                    .ToArray();

                if (!XNode.DeepEquals(modifiedModelXml, remoteModelXml))
                {
                    uploads = uploads.Concat(new[]
                    {
                        new SupplementaryUploadRequest
                        {
                            Path = BuildManifestXmlName,
                            Contents = modifiedModelXml.ToString()
                        }
                    });
                }

                return await PushUploadsAsync(project, @ref, basePath, message, remoteCommit, uploads);
            });
        }

        private async Task<XElement> FetchModelXmlAsync(
            GitHubProject project,
            string @ref,
            string basePath)
        {
            string contents = await _github.GetGitHubFileContentsAsync(
                $"{basePath}/{BuildManifestXmlName}",
                project,
                @ref);

            return XElement.Parse(contents);
        }

        private async Task<bool> PushUploadsAsync(
            GitHubProject project,
            string @ref,
            string basePath,
            string message,
            string remoteCommit,
            IEnumerable<SupplementaryUploadRequest> uploads)
        {
            GitObject[] objects = uploads
                .Select(upload => new GitObject
                {
                    Path = $"{basePath}/{upload.Path}",
                    Mode = GitObject.ModeFile,
                    Type = GitObject.TypeBlob,
                    Content = upload.Contents
                })
                .ToArray();

            GitTree tree = await _github.PostTreeAsync(project, remoteCommit, objects);

            GitCommit commit = await _github.PostCommitAsync(
                project,
                message,
                tree.Sha,
                new[] { remoteCommit });

            try
            {
                // Only fast-forward. Don't overwrite other changes: throw exception instead.
                await _github.PatchReferenceAsync(project, @ref, commit.Sha, force: false);
            }
            catch (NotFastForwardUpdateException e)
            {
                // Retry if there has been a commit since this update attempt started.
                Trace.TraceInformation($"Retrying: {e.Message}");
                return false;
            }

            return true;
        }
    }
}
