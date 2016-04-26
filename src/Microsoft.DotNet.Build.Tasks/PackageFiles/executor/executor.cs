using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Executor
{
    public class Executor
    {
        public Setup openFile(string filePath)
        {
            string jsonFile = string.Empty;
            if (File.Exists(filePath))
            {
                jsonFile = File.ReadAllText(filePath);
                Setup jsonSetup = JsonConvert.DeserializeObject<Setup>(jsonFile);
                return jsonSetup;
            }
            return null;
        }

        public void usage()
        {
            Console.WriteLine("Executor tool");
            Console.WriteLine("Usage: executor [config file] [command]");
            Console.WriteLine("  Config file:      json file that contains the properties and the commands to be run. (Required)");
            Console.WriteLine("Command: [executor command]|[config command]");
            Console.WriteLine("  Executor command:   Executor tool commands.");
            Console.WriteLine("         info       Provides information about the executor tool.");
            Console.WriteLine("         help       Provides information about the content of the config file. ");
            Console.WriteLine("  Config command:   Config file specific commands.");
        }
                
        public static void Main(string[] args)
        {
            int argsLength = args.Length;
            if (argsLength > 0)
            {
                Executor executor = new Executor();
                Setup jsonSetup = executor.openFile(args[0]);
                if (jsonSetup != null)
                {
                    //will be passed by executor.cmd/sh? we only need to now if it is in windows or non-windows.
                    string os = "windows";
                    //default right now.
                    string command = "simple";
                    bool end = false;
                    for (int i = 1; i < argsLength && !end; i++)
                    {
                        switch (args[i])
                        {
                            case "info":
                                executor.usage();
                                end = true;
                                break;
                            case "help":
                                Console.WriteLine("Usage: build [command]:");
                                ++i;
                                if (i < argsLength)
                                {
                                    end = true;
                                    if (args[i].Equals("properties"))
                                    {
                                        jsonSetup.ProvideHelpProperties();
                                    }
                                    else if (args[i].Equals("commands"))
                                    {
                                        jsonSetup.ProvideHelpInstructions(os);
                                    }
                                }
                                if (!end)
                                {
                                    jsonSetup.help(os);
                                    end = true;
                                }
                                break;
                            default:
                                command = args[i];
                                break;
                        }
                    }
                    
                    if(!end)
                    {
                        if (jsonSetup.commands.ContainsKey(command))
                        {
                            Console.WriteLine(jsonSetup.buildCommand(jsonSetup.commands[command], os));
                        }
                        else
                        {
                            Console.WriteLine(string.Format("Error: Command {0} not found.", command));
                            jsonSetup.help(os);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Error: Could not load Json configuration file.");
                }
            }
            else
            {
                Console.WriteLine("Error: Json configuration file should be provided.");
            }
        }
    }

    public class Setup
    {
        public Dictionary<string, Dictionary<string, string>> properties { get; set; }
        public Dictionary<string, Command> commands { get; set; }
        public Dictionary<string, Process> processes { get; set; }

        public void ProvideHelpInstructions(string os)
        {
            Console.WriteLine("========Commands========");

            foreach (KeyValuePair<string, Command> instr in commands)
            {
                Console.WriteLine(string.Format("* {0} - {1}", instr.Key, instr.Value.explanation));
                Console.WriteLine(string.Format("    The value is: {0}", buildCommand(instr.Value, os)));
            }
        }

        public void ProvideHelpProperties()
        {
            Console.WriteLine("========Properties========");

            foreach (KeyValuePair<string, Dictionary<string, string> > property in properties)
            {
                Console.WriteLine(string.Format("* {0} - {1}", property.Key, property.Value["explanation"]));
                if(property.Value["values"] != string.Empty)
                {
                    Console.WriteLine(string.Format("    The values for this property are: {0}", property.Value["values"]));
                }
                if (property.Value["default"] != string.Empty)
                {
                    Console.WriteLine(string.Format("    The default value is: {0}", property.Value["default"]));
                }
            }
        }

        public void help(string os)
        {
            ProvideHelpProperties();
            ProvideHelpInstructions(os);
        }

        private string findPropertyValue(string valueToFind)
        {
            if (properties.ContainsKey(valueToFind))
            {
                return properties[valueToFind]["default"];
            }
            return null;
        }

        private string findPropertyType(string valueToFind)
        {
            if (properties.ContainsKey(valueToFind))
            {
                return properties[valueToFind]["type"];
            }
            return null;
        }

        public string buildCommand(Command commandToExecute, string os)
        {
            string completeCommand = string.Empty;
            if(processes.ContainsKey(commandToExecute.processName))
            {
                completeCommand = (os.Equals("windows")) ? processes[commandToExecute.processName].run["windows"] : processes[commandToExecute.processName].run["non-windows"];
                completeCommand += string.Format(" {0}", commandToExecute.project);
                completeCommand += buildPropertiesForCommand(commandToExecute.property, commandToExecute.processName);
            }
            else
            {
                Console.WriteLine("Error: The process {0} is not specified in the Json file.", commandToExecute.processName);
            }
            
            return completeCommand;
        }

        public string buildPropertiesForCommand(Dictionary<string, string> propertiesToParse, string processName)
        {
            string commandProperties = string.Empty;
            foreach(KeyValuePair<string,string> prop in propertiesToParse)
            {
                var value = (prop.Value.Equals(string.Empty)) ? findPropertyValue(prop.Key): prop.Value;
                if (value!=null)
                {
                    commandProperties += string.Format(" {0}",formatProperty(prop.Key, value, findPropertyType(prop.Key), processName));
                }
                else
                {
                    Console.WriteLine("Error: The property {0} is not specified in the Json file.", prop.Key);
                }
            }
            return commandProperties;
        }

        public string formatProperty(string property, string value, string type, string processName)
        {
            string commandProperty = processes[processName].types[type];
            commandProperty = commandProperty.Replace("{name}", property).Replace("{value}", value);
            return commandProperty;
        }

    }

    public class Command
    {
        public string explanation { get; set; }
        public string processName { get; set; }
        public string project { get; set; }
        public string extraCommands { get; set; }
        public Dictionary<string,string> property { get; set; }

    }

    public class Process
    {
        public Dictionary<string, string> run { get; set; }
        public Dictionary<string, string> types { get; set; }
    }
}
