// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
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
        
        public Dictionary<string, string> SettingParameters { get; set; }
        public Dictionary<string, Dictionary<string, string>> CommandParameters { get; set; }
        
        public Executor()
        {
            SettingParameters = new Dictionary<string, string>();
            CommandParameters = new Dictionary<string, Dictionary<string, string>>();
            string executorDirectory = Path.GetDirectoryName(typeof(Executor).GetTypeInfo().Assembly.Location);
            configFilePath = Path.GetFullPath(Path.Combine(executorDirectory, @"..\"));
        }

        public Executor(string configFile=null)
        {
            SettingParameters = new Dictionary<string, string>();
            CommandParameters = new Dictionary<string, Dictionary<string, string>>();

            if (configFile == null)
            {
                string executorDirectory = Path.GetDirectoryName(typeof(Executor).GetTypeInfo().Assembly.Location);
                configFilePath = Path.GetFullPath(Path.Combine(executorDirectory, @"..\"));
            }
            else
            {
                configFilePath = Path.GetDirectoryName(configFile);
            }
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
                        parser.DefineOptionalQualifier(option.Key, ref temp, option.Value.Description, option.Value.DefaultValue, option.Value.Values);
                        SettingParameters[option.Key] = temp;
                    }

                    //Commands
                    foreach (KeyValuePair<string, Command> comm in setupInformation.Commands)
                    {
                        parser.DefineParameterSet(comm.Key, ref userCommand, comm.Key, string.Format("Help for {0}", comm.Key));
                        Dictionary<string, string> param = new Dictionary<string, string>();
                        foreach(KeyValuePair<string, AliasPerCommand> aliasInfo in comm.Value.Alias)
                        {
                            string temp = "";
                            parser.DefineOptionalQualifier(aliasInfo.Key, ref temp, aliasInfo.Value.Description, null, null);
                            if(!string.IsNullOrEmpty(temp) && !temp.Equals("true"))
                            {
                                List<string> keys = new List<string>(aliasInfo.Value.Settings.Keys);
                                if(keys.Count < 2)
                                {
                                    foreach (string key in keys)
                                    {
                                        setupInformation.Commands[comm.Key].Alias[aliasInfo.Key].Settings[key] = temp;
                                    }
                                }
                            }

                            param[aliasInfo.Key] = temp;
                        }
                        CommandParameters[comm.Key] = new Dictionary<string, string>(param);
                    }
                }, args, setupInformation);
                CommandSelectedByUser = userCommand;
                setupInformation.SettingParameters = SettingParameters;
                return result;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error: {0} {1}", e.Message, e.StackTrace);
                return false;
            }
            
        }

        public static int Main(string[] args)
        {
            string[] parseArgs;
            Executor executor;
            if(args.Length > 0 && args[0].Contains("config.json"))
            {
                executor = new Executor(args[0]);
                parseArgs = new string[args.Length - 1];
                Array.Copy(args, 1, parseArgs, 0, args.Length - 1);
            }
            else
            {
                executor = new Executor();
                parseArgs = args;
            }

            Setup jsonSetup = executor.OpenFile();
            if (jsonSetup == null)
            {
                Console.Error.WriteLine("Error: Could not load Json configuration file.");
                return 1;
            }
            else
            {
                string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows": "unix";

                jsonSetup.prepareValues(os, executor.SettingParameters, executor.configFilePath);
                if (executor.DefineParameters(parseArgs, jsonSetup))
                {
                    List<string> paramSelected = new List<string>();
                    foreach(KeyValuePair<string, string> param in executor.CommandParameters[executor.CommandSelectedByUser])
                    {
                        if (!string.IsNullOrEmpty(param.Value))
                        {
                            paramSelected.Add(param.Key);
                        }
                    }
                    return jsonSetup.ExecuteCommand(executor.CommandSelectedByUser, paramSelected);
                }
            }
            return 0;
        }
    }
}
