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
        public Dictionary<string, string> SettingParameters { get; set; }
        public Dictionary<string, string> CommandParameters { get; set; }
        
        public Executor()
        {
            SettingParameters = new Dictionary<string, string>();
            CommandParameters = new Dictionary<string, string>();
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
            }
            return null;
        }

        public void DefineParameters(string[] args, Setup setupInformation)
        {
            try
            {
                CommandLineParser.ParseForConsoleApplication(delegate (CommandLineParser parser)
                {
                    //Settings
                    foreach (KeyValuePair<string, Setting> option in setupInformation.Settings)
                    {
                        if (!option.Key.Equals("ExtraArguments"))
                        {
                            string temp = "";
                            parser.DefineOptionalQualifier(option.Key, ref temp, option.Value.Description);
                            SettingParameters[option.Key] = temp;
                        }
                    }

                    //Commands
                    foreach (KeyValuePair<string, Command> comm in setupInformation.Commands)
                    {
                        bool temp = false;
                        parser.DefineOptionalQualifier(comm.Key, ref temp, comm.Value.Description);
                        CommandParameters[comm.Key] = temp.ToString();
                    }

                    //extra arguments
                    if (!args[0].Equals("-?"))
                    {
                        string[] extraArguments = null;
                        parser.DefineOptionalParameter("ExtraArguments", ref extraArguments, "Extra parameters will be passed to the selected command.");
                        if (extraArguments.Length > 0)
                        {
                            string[] temp = new string[extraArguments.Length - 1];
                            Array.Copy(extraArguments, 1, temp, 0, extraArguments.Length - 1);
                            SettingParameters["ExtraArguments"] = string.Join(" ", temp);
                        }
                        else
                        {
                            SettingParameters["ExtraArguments"] = string.Join(" ", extraArguments);
                        }
                    }
                }, args);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: Please provide at least one parameter. {0}", e.Message);
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
                
                executor.DefineParameters(args, jsonSetup);
                
                foreach (KeyValuePair<string, string> command in executor.CommandParameters)
                {
                    //activated by the user
                    if (Convert.ToBoolean(command.Value))
                    {
                        if(!jsonSetup.BuildCommand(jsonSetup.Commands[command.Key], os, executor.SettingParameters, executor.configFilePath))
                        {
                            return 1;
                        }
                    }
                }
            }
            return 0;
        }
    }
}
