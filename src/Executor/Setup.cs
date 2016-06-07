using System;
using System.IO;
using System.Collections.Generic;

namespace Microsoft.DotNet.Execute
{
    public class Setup
    {
        public Dictionary<string, Setting> Settings { get; set; }
        public Dictionary<string, Command> Commands { get; set; }
        public Dictionary<string, Tool> Tools { get; set; }


        public void ProvideHelpSettings()
        {
            Console.WriteLine("========Settings========");

            foreach (KeyValuePair<string, Setting> settingInfo in Settings)
            {
                Console.WriteLine("* {0} - {1}", settingInfo.Key, settingInfo.Value.Description);
                if (settingInfo.Value.Values.Count > 0)
                {
                    Console.WriteLine("    The allowed values are: {0}", string.Join(", ", settingInfo.Value.Values));
                }
                if (!string.IsNullOrEmpty(settingInfo.Value.DefaultValue))
                {
                    Console.WriteLine("    The default value is: {0}", settingInfo.Value.DefaultValue);
                }
            }
        }

        private string FindSettingValue(string valueToFind)
        {
            if (Settings.ContainsKey(valueToFind))
            {
                return Settings[valueToFind].DefaultValue;
            }
            return null;
        }

        private string FindSettingType(string valueToFind)
        {
            if (Settings.ContainsKey(valueToFind))
            {
                return Settings[valueToFind].ValueType;
            }
            return null;
        }
        
        public int BuildCommand(string commandSelectedByUser, string os, Dictionary<string, string> settingParameters, string configPath)
        {
            if (!Commands.ContainsKey(commandSelectedByUser))
            {
                Console.WriteLine("Error: The command {0} is not specified in the Json file.", commandSelectedByUser);
                return 1;
            }

            Command commandToExecute = Commands[commandSelectedByUser];
            string toolName = GetTool(commandToExecute, os, configPath);
            if (string.IsNullOrEmpty(toolName))
            {
                Console.WriteLine("Error: The process {0} is not specified in the Json file.", commandToExecute.ToolName);
                return 1;
            }

            if (BuildRequiredValueSettingsForCommand(commandToExecute.LockedSettings, settingParameters) &&
                    BuildOptionalValueSettingsForCommand(commandToExecute.Settings, settingParameters) &&
                    ValidExtraArgumentsForCommand(settingParameters["ExtraArguments"], settingParameters))
            {
                string commandParameters = BuildParametersForCommand(settingParameters, commandToExecute.ToolName);

                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("Running: {0} {1}", toolName, commandParameters);
                Console.ResetColor();

                Run runCommand = new Run();
                int result =  runCommand.ExecuteProcess(toolName, commandParameters);
                if(result == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Build Succeeded.");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Build Failed.");
                }
                Console.ResetColor();

                return result;
            }
            return 1;
        }
        
        private string BuildParametersForCommand(Dictionary<string, string> settingParameters, string toolName)
        {
            string commandSetting = string.Empty;
            foreach (KeyValuePair<string, string> parameters in settingParameters)
            {
                if (!string.IsNullOrEmpty(parameters.Value))
                {
                    string settingType = FindSettingType(parameters.Key);
                    if (settingType.Equals("passThrough"))
                    {
                        commandSetting += string.Format(" {0}", toolName.Equals("console") ? "": parameters.Value);
                    }
                    else
                    {
                        commandSetting += string.Format(" {0}", FormatSetting(parameters.Key, parameters.Value, FindSettingType(parameters.Key), toolName));
                    }
                }
            }
            return commandSetting;
        }

        private bool BuildRequiredValueSettingsForCommand(Dictionary<string, string> requiredSettings, Dictionary<string, string> commandValues)
        {
            foreach (KeyValuePair<string, string> reqSetting in requiredSettings)
            {
                string value = string.IsNullOrEmpty(reqSetting.Value) || reqSetting.Value.Equals("default") ? FindSettingValue(reqSetting.Key) : reqSetting.Value;
                if (value != null && (string.IsNullOrEmpty(commandValues[reqSetting.Key]) || reqSetting.Key.Equals("Project")))
                {
                    commandValues[reqSetting.Key] = string.IsNullOrEmpty(value) ? "True" : value;
                }
                else
                {
                    if (!string.IsNullOrEmpty(value) && !value.Equals(commandValues[reqSetting.Key]))
                    {
                        Console.WriteLine("Error: The value for setting {0} can't be overwriten.", reqSetting.Key);
                        return false;
                    }
                }
            }
            return true;
        }

        private bool BuildOptionalValueSettingsForCommand(Dictionary<string, string> optionalSettings, Dictionary<string, string> commandValues)
        {
            foreach (KeyValuePair<string, string> optSetting in optionalSettings)
            {
                if (string.IsNullOrEmpty(commandValues[optSetting.Key]))
                {
                    string value = string.IsNullOrEmpty(optSetting.Value) || optSetting.Value.Equals("default") ? FindSettingValue(optSetting.Key) : optSetting.Value;
                    if (value != null)
                    {
                        commandValues[optSetting.Key] = value;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool ValidExtraArgumentsForCommand(string extraArguments, Dictionary<string, string> commandValues)
        {
            int colonPosition;
            int equalPosition;
            string tempParam;

            string[] extraA = extraArguments.Split(' ');
            foreach(string param in extraA)
            {
                colonPosition = 0;
                equalPosition = param.Length;
                tempParam = string.Empty;

                colonPosition = param.IndexOf(":");
                equalPosition = param.IndexOf("=");
                if(colonPosition!=0)
                {
                    if(equalPosition == -1)
                    {
                        tempParam = param.Substring(colonPosition + 1, (param.Length - colonPosition - 1));
                    }
                    else
                    {
                        tempParam = param.Substring(colonPosition + 1, (equalPosition - colonPosition - 1));
                    }

                    if(commandValues.ContainsKey(tempParam) && !string.IsNullOrEmpty(commandValues[tempParam]))
                    {
                        Console.WriteLine("Error: The value for setting {0} can't be overwriten.", tempParam);
                        return false;
                    }
                }
            }
            return true;
        }

        public string GetTool(Command commandToExecute, string os, string configPath)
        {
            if (Tools.ContainsKey(commandToExecute.ToolName))
            {
                if(commandToExecute.ToolName.Equals("msbuild"))
                {
                    return Path.GetFullPath(Path.Combine(configPath, os.Equals("windows") ? Tools[commandToExecute.ToolName].Run["windows"] : Tools[commandToExecute.ToolName].Run["unix"]));
                }
                else if (commandToExecute.ToolName.Equals("console"))
                {
                    string extension = os.Equals("windows") ? Tools[commandToExecute.ToolName].Run["windows"] : Tools[commandToExecute.ToolName].Run["unix"];
                    return Path.GetFullPath(Path.Combine(configPath, string.Format("{0}.{1}", commandToExecute.LockedSettings["Project"],extension)));
                }
            }
            return string.Empty;
        }
            
        public string FormatSetting(string option, string value, string type, string toolName)
        {
            if (Tools.ContainsKey(toolName) && !string.IsNullOrEmpty(type))
            {
                string commandOption = Tools[toolName].ValueTypes[type];
                commandOption = commandOption.Replace("{name}", option).Replace("{value}", value);
                return commandOption;
            }
            return null;
        }
    }

    public class Command
    {
        public string Description { get; set; }
        public string Alias { get; set; }
        public string ToolName { get; set; }
        public Dictionary<string, string> LockedSettings { get; set; }
        public Dictionary<string, string> Settings { get; set; }

    }

    public class Tool
    {
        public Dictionary<string, string> Run { get; set; }
        public Dictionary<string, string> ValueTypes { get; set; }
    }

    public class Setting
    {
        public string Description { get; set; }
        public string ValueType { get; set; }
        public List<string> Values { get; set; }
        public string DefaultValue { get; set; }
    }
}
