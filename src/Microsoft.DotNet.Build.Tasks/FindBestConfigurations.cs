// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Microsoft.DotNet.Build.Tasks
{
    public class FindBestConfigurations : ConfigurationTask
    {
        [Required]
        public ITaskItem[] Configurations { get; set; }

        [Required]
        public string[] SupportedConfigurations { get; set; }

        public bool DoNotAllowCompatibleValues { get; set; }

        [Output]
        public ITaskItem[] BestConfigurations { get; set; }

        public override bool Execute()
        {
            LoadConfiguration();

            var supportedProjectConfigurations = new HashSet<Configuration>(
                SupportedConfigurations.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => ConfigurationFactory.ParseConfiguration(c)),
                Configuration.CompatibleComparer);

            var placeHolderBuildConfigurations = new HashSet<string>(SupportedConfigurations.Where(c => c.StartsWith(ConfigurationFactory.NopConfigurationPrefix)));

            var bestConfigurations = new List<ITaskItem>();

            foreach (var configurationItem in Configurations)
            {
                var buildConfiguration = ConfigurationFactory.ParseConfiguration(configurationItem.ItemSpec);

                var compatibleConfigurations = ConfigurationFactory.GetCompatibleConfigurations(buildConfiguration, DoNotAllowCompatibleValues);

                var bestConfiguration = compatibleConfigurations.FirstOrDefault(c => supportedProjectConfigurations.Contains(c));

                if (bestConfiguration == null)
                {
                    Log.LogMessage(MessageImportance.Low, $"Could not find any applicable configuration for '{buildConfiguration}' among projectConfigurations {string.Join(", ", supportedProjectConfigurations.Select(c => c.ToString()))}");
                    Log.LogMessage(MessageImportance.Low, $"Compatible configurations: {string.Join(", ", compatibleConfigurations.Select(c => c.ToString()))}");
                }
                else
                {
                    string targetGroup = bestConfiguration.Values[0].Value; // TargetGroup is in property value index 0
                    string osGroup = bestConfiguration.Values[1].Value; // OSGroup is in property value index 1
                    string buildConfigurationNoConfigurationGroup = osGroup != "AnyOS" ? $"{targetGroup}-{osGroup}" : targetGroup; // BuildConfigurations in configurations.props don't include ConfigurationGroup, so we need to try to find placeholder configurations without it.

                    // placeholder configurations will be in the form of TargetGroup-OSGroup or TargetGroup so we need to fallback to _TargetGroup to ignore it because that means it needs to be ignored in all OSGroups.
                    if (placeHolderBuildConfigurations.Contains($"{ConfigurationFactory.NopConfigurationPrefix}{buildConfigurationNoConfigurationGroup}") || placeHolderBuildConfigurations.Contains($"{ConfigurationFactory.NopConfigurationPrefix}{targetGroup}"))
                    {
                        BestConfigurations = Array.Empty<ITaskItem>();
                        return !Log.HasLoggedErrors;
                    }

                    Log.LogMessage(MessageImportance.Low, $"Chose configuration {bestConfiguration}");
                    var bestConfigurationItem = new TaskItem(bestConfiguration.ToString(), (IDictionary)bestConfiguration.GetProperties());

                    // preserve metadata on the configuration that selected this
                    configurationItem.CopyMetadataTo(bestConfigurationItem);

                    // preserve the configuration that selected this
                    bestConfigurationItem.SetMetadata("BuildConfiguration", configurationItem.ItemSpec);
                    foreach(var additionalProperty in buildConfiguration.GetProperties())
                    {
                        bestConfigurationItem.SetMetadata("BuildConfiguration_" + additionalProperty.Key, additionalProperty.Value);
                    }

                    bestConfigurations.Add(bestConfigurationItem);
                }
            }

            BestConfigurations = bestConfigurations.ToArray();

            return !Log.HasLoggedErrors;
        }
    }
}

