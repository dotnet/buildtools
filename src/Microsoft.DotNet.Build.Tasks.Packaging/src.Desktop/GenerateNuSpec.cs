// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;

namespace NuProj.Tasks
{
    public class GenerateNuSpec : Task
    {
        private const string NuSpecXmlNamespace = @"http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd";

        public string InputFileName { get; set; }

        [Required]
        public string OutputFileName { get; set; }

        public string MinClientVersion { get; set; }

        [Required]
        public string Id { get; set; }

        [Required]
        public string Version { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Authors { get; set; }

        [Required]
        public string Owners { get; set; }

        [Required]
        public string Description { get; set; }

        public string ReleaseNotes { get; set; }

        public string Summary { get; set; }

        public string Language { get; set; }

        public string ProjectUrl { get; set; }

        public string IconUrl { get; set; }

        public string LicenseUrl { get; set; }

        public string Copyright { get; set; }

        public bool RequireLicenseAcceptance { get; set; }

        public bool DevelopmentDependency { get; set; }

        public string Tags { get; set; }

        public ITaskItem[] Dependencies { get; set; }

        public ITaskItem[] References { get; set; }

        public ITaskItem[] FrameworkReferences { get; set; }

        public ITaskItem[] Files { get; set; }

        public override bool Execute()
        {
            try
            {
                WriteNuSpecFile();
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
                Log.LogErrorFromException(ex);
            }

            return !Log.HasLoggedErrors;
        }

        private void WriteNuSpecFile()
        {
            var manifest = CreateManifest();

            if (!IsDifferent(manifest))
            {
                Log.LogMessage("Skipping generation of .nuspec because contents are identical.");
                return;
            }

            var directory = Path.GetDirectoryName(OutputFileName);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var file = File.Create(OutputFileName))
            {
                manifest.Save(file, false);
            }
        }

        private bool IsDifferent(Manifest newManifest)
        {
            if (!File.Exists(OutputFileName))
                return true;

            var oldSource = File.ReadAllText(OutputFileName);
            var newSource = "";
            using (var stream = new MemoryStream())
            {
                newManifest.Save(stream);
                stream.Seek(0, SeekOrigin.Begin);
                newSource = Encoding.UTF8.GetString(stream.ToArray());
            }

            return oldSource != newSource;
        }

        private Manifest CreateManifest()
        {
            Manifest manifest;
            ManifestMetadata manifestMetadata;
            if (!string.IsNullOrEmpty(InputFileName))
            {
                using (var stream = File.OpenRead(InputFileName))
                {
                    manifest = Manifest.ReadFrom(stream, false);
                }
            }
            else
            {
                manifest = new Manifest()
                {
                    Metadata = new ManifestMetadata(),
                };
            }

            if (manifest.Metadata == null)
            {
                manifest.Metadata = new ManifestMetadata();
            }

            manifestMetadata = manifest.Metadata;

            manifestMetadata.UpdateMember(x => x.Authors, Authors);
            manifestMetadata.UpdateMember(x => x.Copyright, Copyright);
            manifestMetadata.AddRangeToMember(x => x.DependencySets, GetDependencySets());
            manifestMetadata.UpdateMember(x => x.Description, Description);
            manifestMetadata.DevelopmentDependency |= DevelopmentDependency;
            manifestMetadata.AddRangeToMember(x => x.FrameworkAssemblies, GetFrameworkAssemblies());
            manifestMetadata.UpdateMember(x => x.IconUrl, IconUrl);
            manifestMetadata.UpdateMember(x => x.Id, Id);
            manifestMetadata.UpdateMember(x => x.Language, Language);
            manifestMetadata.UpdateMember(x => x.LicenseUrl, LicenseUrl);
            manifestMetadata.UpdateMember(x => x.MinClientVersionString, MinClientVersion);
            manifestMetadata.UpdateMember(x => x.Owners, Owners);
            manifestMetadata.UpdateMember(x => x.ProjectUrl, ProjectUrl);
            manifestMetadata.AddRangeToMember(x => x.ReferenceSets, GetReferenceSets());
            manifestMetadata.UpdateMember(x => x.ReleaseNotes, ReleaseNotes);
            manifestMetadata.RequireLicenseAcceptance |= RequireLicenseAcceptance;
            manifestMetadata.UpdateMember(x => x.Summary, Summary);
            manifestMetadata.UpdateMember(x => x.Tags, Tags);
            manifestMetadata.UpdateMember(x => x.Title, Title);
            manifestMetadata.UpdateMember(x => x.Version, Version);

            manifest.AddRangeToMember(x => x.Files, GetManifestFiles());

            return manifest;
        }

        private List<ManifestFile> GetManifestFiles()
        {
            return (from f in Files.NullAsEmpty()
                    select new ManifestFile
                    {
                        Source = f.GetMetadata(Metadata.FileSource),
                        Target = f.GetMetadata(Metadata.FileTarget),
                        Exclude = f.GetMetadata(Metadata.FileExclude),
                    }).ToList();
        }

        private List<ManifestFrameworkAssembly> GetFrameworkAssemblies()
        {
            return (from fr in FrameworkReferences.NullAsEmpty()
                    select new ManifestFrameworkAssembly
                    {
                        AssemblyName = fr.ItemSpec,
                        TargetFramework = fr.GetTargetFramework().GetShortFrameworkName(),
                    }).ToList();
        }

        private List<ManifestDependencySet> GetDependencySets()
        {
            var dependencies = from d in Dependencies.NullAsEmpty()
                               select new Dependency
                               {
                                   Id = d.ItemSpec,
                                   Version = d.GetVersion(),
                                   TargetFramework = d.GetTargetFramework()
                               };

            return (from dependency in dependencies
                    group dependency by dependency.TargetFramework into dependenciesByFramework
                    select new ManifestDependencySet
                    {
                        TargetFramework = dependenciesByFramework.Key.GetShortFrameworkName(),
                        Dependencies = (from dependency in dependenciesByFramework
                                        where dependency.Id != "_._"
                                        group dependency by dependency.Id into dependenciesById
                                        select new ManifestDependency
                                        {
                                            Id = dependenciesById.Key,
                                            Version = dependenciesById.Select(x => x.Version)
                                                .Aggregate(AggregateVersions)
                                                .ToStringSafe()
                                        }).ToList()
                    }).ToList();
        }

        private List<ManifestReferenceSet> GetReferenceSets()
        {
            var references = from r in References.NullAsEmpty()
                             select new
                             {
                                 File = r.ItemSpec,
                                 TargetFramework = r.GetTargetFramework(),
                             };

            return (from reference in references
                    group reference by reference.TargetFramework into referencesByFramework
                    select new ManifestReferenceSet
                    {
                        TargetFramework = referencesByFramework.Key.GetShortFrameworkName(),
                        References = (from reference in referencesByFramework
                                      select new ManifestReference
                                      {
                                          File = reference.File
                                      }).ToList()
                    }).ToList();
        }

        private static IVersionSpec AggregateVersions(IVersionSpec aggregate, IVersionSpec next)
        {
            var versionSpec = new VersionSpec();
            SetMinVersion(versionSpec, aggregate);
            SetMinVersion(versionSpec, next);
            SetMaxVersion(versionSpec, aggregate);
            SetMaxVersion(versionSpec, next);

            if (versionSpec.MinVersion == null && versionSpec.MaxVersion == null)
            {
                versionSpec = null;
            }

            return versionSpec;
        }

        private static void SetMinVersion(VersionSpec target, IVersionSpec source)
        {
            if (source == null || source.MinVersion == null)
            {
                return;
            }

            if (target.MinVersion == null)
            {
                target.MinVersion = source.MinVersion;
                target.IsMinInclusive = source.IsMinInclusive;
            }

            if (target.MinVersion < source.MinVersion)
            {
                target.MinVersion = source.MinVersion;
                target.IsMinInclusive = source.IsMinInclusive;
            }

            if (target.MinVersion == source.MinVersion)
            {
                target.IsMinInclusive = target.IsMinInclusive && source.IsMinInclusive;
            }
        }

        private static void SetMaxVersion(VersionSpec target, IVersionSpec source)
        {
            if (source == null || source.MaxVersion == null)
            {
                return;
            }

            if (target.MaxVersion == null)
            {
                target.MaxVersion = source.MaxVersion;
                target.IsMaxInclusive = source.IsMaxInclusive;
            }

            if (target.MaxVersion > source.MaxVersion)
            {
                target.MaxVersion = source.MaxVersion;
                target.IsMaxInclusive = source.IsMaxInclusive;
            }

            if (target.MaxVersion == source.MaxVersion)
            {
                target.IsMaxInclusive = target.IsMaxInclusive && source.IsMaxInclusive;
            }
        }

        private class Dependency
        {
            public string Id { get; set; }

            public FrameworkName TargetFramework { get; set; }

            public IVersionSpec Version { get; set; }
        }
    }
}