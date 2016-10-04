// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

            // to maintain compatibility with old behavior
            var legacyConfigPath = Directory.GetParent(Path.GetDirectoryName(typeof(Executor).GetTypeInfo().Assembly.Location)).Name;
            if (File.Exists(Path.Combine(legacyConfigPath, configFileName)))
                configFilePath = legacyConfigPath;
            else
                configFilePath = Directory.GetCurrentDirectory();
        }

        public Executor(string configFile) : this()
        {
            if (string.IsNullOrEmpty(configFile))
                throw new ArgumentException("The configFile parameter cannot be null or empty.");

            configFilePath = Path.GetDirectoryName(configFile);
            configFileName = Path.GetFileName(configFile);
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
            else
            {
                Console.WriteLine($"The specified config file '{configFile}' does not exist.");
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
                        foreach (KeyValuePair<string, AliasPerCommand> aliasInfo in comm.Value.Alias)
                        {
                            string temp = "";
                            parser.DefineOptionalQualifier(aliasInfo.Key, ref temp, aliasInfo.Value.Description, null, null);
                            if (!string.IsNullOrEmpty(temp) && !temp.Equals("true"))
                            {
                                List<string> keys = new List<string>(aliasInfo.Value.Settings.Keys);
                                if (keys.Count < 2)
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
                SettingParameters["ExtraParameters"] = CommandLineParser.ExtraParameters;
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
            if (args.Length > 0 && args[0].EndsWith(".json"))
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
            string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "unix";

            if (jsonSetup.PrepareValues(os, executor.SettingParameters, executor.configFilePath) == 0)
            {
                if (executor.DefineParameters(parseArgs, jsonSetup))
                {
                    if (string.IsNullOrEmpty(executor.CommandSelectedByUser))
                    {
                        Console.Error.WriteLine("Error: No command was passed. Use -? for help.");
                        return 1;
                    }

                    List<string> paramSelected = new List<string>();
                    foreach (KeyValuePair<string, string> param in executor.CommandParameters[executor.CommandSelectedByUser])
                    {
                        if (!string.IsNullOrEmpty(param.Value))
                        {
                            paramSelected.Add(param.Key);
                        }
                    }

                    // If aliases exist, and the user's parameters have no match, we'll end up in this state. 
                    // If a default alias is provided, we can add that; otherwise we should error out as the 
                    // behavior at this point may be unexpected.
                    if (paramSelected.Count == 0 && executor.CommandParameters[executor.CommandSelectedByUser].Count > 0)
                    {
                        string defaultAlias = jsonSetup.Commands[executor.CommandSelectedByUser].DefaultAlias;
                        if (!string.IsNullOrEmpty(defaultAlias))
                        {
                            Console.WriteLine($"No parameter selected, using default alias '{defaultAlias}'");
                            paramSelected.Add(defaultAlias);
                        }
                        // May want to error out here in the future;  Need to find out if there's ever a reason
                        // to allow aliases, but specify none.
                    }

                    return jsonSetup.ExecuteCommand(executor.CommandSelectedByUser, paramSelected);
                }
            }
            //There was an error when parsing the user input, Define Parameters is in charge of printing an error message.
            return 1;
        }
    }
}
