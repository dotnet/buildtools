// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using Microsoft.Fx.CommandLine;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Execute
{
    public class Executor
    {
        public string configFilePath;
        public string configFileName = "config.json";
        public string CommandSelectedByUser { get; set; }
        
        public Dictionary<string, List<string>> DevWorkflowCommands { get; set; }
        public Dictionary<string, string> SettingParameters { get; set; }
        public Dictionary<string, string> CommandParameters { get; set; }
        
        public Executor()
        {
            SettingParameters = new Dictionary<string, string>();
            CommandParameters = new Dictionary<string, string>();
            DevWorkflowCommands = new Dictionary<string, List<string>>();
            string executorDirectory = Path.GetDirectoryName(typeof(Executor).GetTypeInfo().Assembly.Location);
            configFilePath = Path.GetFullPath(Path.Combine(executorDirectory, @"..\"));
        }

        public Setup OpenFile()
        {
            string configFile = Path.Combine(configFilePath, configFileName);
            if (File.Exists(configFile))
            {
                string jsonFile = File.ReadAllText(configFile);
                try
                {
                    Setup jsonSetup = JsonConvert.DeserializeObject<Setup>(jsonFile);
                    return jsonSetup;
                }
                catch (JsonSerializationException e)
                {
                    Console.WriteLine(e.Message);
                }
                catch (JsonReaderException e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            return null;
        }

        public bool DefineParameters(string[] args, Setup setupInformation)
        {
            string userCommand = string.Empty;
            try
            {
                bool result = CommandLineParser.ParseForConsoleApplication(delegate (CommandLineParser parser)
                {
                    //Settings
                    foreach (KeyValuePair<string, Setting> option in setupInformation.Settings)
                    {
                        string temp = "";
                        parser.DefineOptionalQualifier(option.Key, ref temp, option.Value.Description);
                        SettingParameters[option.Key] = temp;
                    }

                    //Commands
                    foreach (KeyValuePair<string, List<string>> comm in DevWorkflowCommands)
                    {
                        parser.DefineParameterSet(comm.Key, ref userCommand, comm.Key, string.Format("Help for {0}", comm.Key));
                        foreach(string devWorkflowOption in comm.Value)
                        {
                            bool temp = false;
                            parser.DefineOptionalQualifier(devWorkflowOption, ref temp, setupInformation.Commands[comm.Key + "-" + devWorkflowOption].Description);
                            CommandParameters[comm.Key + "-" + devWorkflowOption] = temp.ToString();
                        }
                    }

                }, args);
                CommandSelectedByUser = userCommand;
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: {0}", e.Message);
                return false;
            }
            
        }

        public void CreateDevWorkflowCommandStructure(Setup setupInformation)
        {
            foreach (KeyValuePair<string, Setting> option in setupInformation.Settings)
            {
                SettingParameters[option.Key] = string.Empty;
            }

            string devWorkflowStep = string.Empty;
            string devWorkflowOption = string.Empty;
            foreach (KeyValuePair<string, Command> comm in setupInformation.Commands)
            {
                CommandParameters[comm.Key] = "False";

                int delimiter = comm.Key.IndexOf("-");
                if (delimiter != -1)
                {
                    devWorkflowOption = comm.Key.Substring(delimiter+1);
                    devWorkflowStep = comm.Key.Substring(0, delimiter);
                    if (!DevWorkflowCommands.ContainsKey(devWorkflowStep))
                    {
                        DevWorkflowCommands[devWorkflowStep] = new List<string>();
                    }
                    DevWorkflowCommands[devWorkflowStep].Add(devWorkflowOption);
                }
                else
                {
                    if (!DevWorkflowCommands.ContainsKey(comm.Key))
                    {
                        DevWorkflowCommands[comm.Key] = new List<string>();
                    }
                }
            }
        }

        public static int Main(string[] args)
        {
            Executor executor = new Executor();
            Setup jsonSetup = executor.OpenFile();
            if (jsonSetup == null)
            {
                Console.WriteLine("Error: Could not load Json configuration file.");
                return 1;
            }
            else
            {
                string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows": "unix";

                executor.CreateDevWorkflowCommandStructure(jsonSetup);

                if (executor.DefineParameters(args, jsonSetup))
                {
                    foreach (KeyValuePair<string, string> command in executor.CommandParameters)
                    {
                        if (Convert.ToBoolean(command.Value))
                        {
                            executor.CommandSelectedByUser = command.Key;
                        }
                    }
                    return jsonSetup.BuildCommand(executor.CommandSelectedByUser, os, executor.SettingParameters, executor.configFilePath);
                }
            }
            return 0;
        }
    }
}
