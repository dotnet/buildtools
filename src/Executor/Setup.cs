using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.Execute
{
    public class Setup
    {
        public Dictionary<string, Setting> Settings { get; set; }
        public Dictionary<string, Command> Commands { get; set; }
        public Dictionary<string, Tool> Tools { get; set; }
        public Dictionary<string, string> SettingParameters { get; set; }
        public string Os { get; set; }
        public string ConfigurationFilePath { get; set; }


        private string FindSettingValue(string valueToFind)
        {
            Setting value;
            if(Settings.TryGetValue(valueToFind, out value))
            {
                return value.DefaultValue;
            }
            return null;
        }

        private string FindSettingType(string valueToFind)
        {
            Setting value;
            if (Settings.TryGetValue(valueToFind, out value))
            {
                return value.ValueType;
            }
            return null;
        }
        
        public void prepareValues(string os, Dictionary<string, string> parameters, string configFile)
        {
            SettingParameters = new Dictionary<string, string>(parameters);
            Os = os;
            ConfigurationFilePath = configFile;
        }

        public int ExecuteCommand(string commandSelectedByUser)
        {
            CompleteCommand commandToRun = BuildCommand(commandSelectedByUser);
            if(commandToRun != null)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("Running: {0} {1}", commandToRun.ToolCommand, commandToRun.ParametersCommand);
                Console.ResetColor();

                int result = Run.ExecuteProcess(commandToRun.ToolCommand, commandToRun.ParametersCommand);
                if (result == 0)
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

        private CompleteCommand BuildCommand(string commandSelectedByUser, Dictionary<string, string> parameters = null)
        {
            Command commandToExecute;
            if (!Commands.TryGetValue(commandSelectedByUser, out commandToExecute))
            {
                Console.Error.WriteLine("Error: The command {0} is not specified in the Json file.", commandSelectedByUser);
                return null;
            }

            string toolName = GetTool(commandToExecute, Os, ConfigurationFilePath);
            if (string.IsNullOrEmpty(toolName))
            {
                Console.Error.WriteLine("Error: The process {0} is not specified in the Json file.", commandToExecute.ToolName);
                return null;
            }

            if (parameters == null)
            {
                if (BuildRequiredValueSettingsForCommand(commandToExecute.LockedSettings, SettingParameters) &&
                    BuildOptionalValueSettingsForCommand(commandToExecute.Settings, SettingParameters) &&
                    ValidExtraArgumentsForCommand(SettingParameters["ExtraArguments"], SettingParameters))
                {
                    string commandParameters = BuildParametersForCommand(SettingParameters, commandToExecute.ToolName);
                    CompleteCommand completeCommand = new CompleteCommand(toolName, commandParameters);
                    return completeCommand;
                }
                return null;
            }
            else
            {
                string commandParameters = BuildParametersForCommand(parameters, commandToExecute.ToolName);
                CompleteCommand completeCommand = new CompleteCommand(toolName, commandParameters);
                return completeCommand;
            }
        }
        
        private string BuildParametersForCommand(Dictionary<string, string> settingParameters, string toolName)
        {
            string commandSetting = string.Empty;
            foreach (KeyValuePair<string, string> parameters in settingParameters)
            {
                if (!string.IsNullOrEmpty(parameters.Value))
                {
                    commandSetting += string.Format(" {0}", FormatSetting(parameters.Key, parameters.Value, FindSettingType(parameters.Key), toolName));
                }
            }
            return commandSetting;
        }

        private bool BuildRequiredValueSettingsForCommand(Dictionary<string, string> requiredSettings, Dictionary<string, string> commandValues)
        {
            foreach (KeyValuePair<string, string> reqSetting in requiredSettings)
            {
                string value = string.IsNullOrEmpty(reqSetting.Value) || reqSetting.Value.Equals("default") ? FindSettingValue(reqSetting.Key) : reqSetting.Value;
                string currentValue;
                if(commandValues.TryGetValue(reqSetting.Key, out currentValue))
                {
                    if (value != null && (string.IsNullOrEmpty(currentValue) || reqSetting.Key.Equals("Project")))
                    {
                        commandValues[reqSetting.Key] = string.IsNullOrEmpty(value) ? "True" : value;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(value) && !value.Equals(currentValue))
                        {
                            Console.Error.WriteLine("Error: The value for setting {0} can't be overwriten.", reqSetting.Key);
                            return false;
                        }
                    }
                }
                else
                {
                    Console.Error.WriteLine("Error: The setting {0} is not specified in the Json file.", reqSetting.Key);
                    return false;
                }
            }
            return true;
        }

        private bool BuildOptionalValueSettingsForCommand(Dictionary<string, string> optionalSettings, Dictionary<string, string> commandValues)
        {
            foreach (KeyValuePair<string, string> optSetting in optionalSettings)
            {
                string currentValue;
                if(commandValues.TryGetValue(optSetting.Key, out currentValue))
                {
                    if (string.IsNullOrEmpty(currentValue))
                    {
                        string value = string.IsNullOrEmpty(optSetting.Value) || optSetting.Value.Equals("default") ? FindSettingValue(optSetting.Key) : optSetting.Value;
                        if (value != null)
                        {
                            commandValues[optSetting.Key] = value;
                        }
                    }
                }
                else
                {
                    Console.Error.WriteLine("Error: The setting {0} is not specified in the Json file.", optSetting.Key);
                    return false;
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

                    string value;
                    if(commandValues.TryGetValue(tempParam, out value) && !string.IsNullOrEmpty(value))
                    {
                        Console.Error.WriteLine("Error: The value for setting {0} can't be overwriten.", tempParam);
                        return false;
                    }
                }
            }
            return true;
        }

        private string GetTool(Command commandToExecute, string os, string configPath)
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
            string commandOption = null;
            if (type.Equals("passThrough"))
            {
                commandOption = string.Format(" {0}", toolName.Equals("console") ? "" : value);
            }
            else
            {
                Tool toolFormat;
                if (Tools.TryGetValue(toolName, out toolFormat) && !string.IsNullOrEmpty(type))
                {
                    commandOption = toolFormat.ValueTypes[type];
                    commandOption = commandOption.Replace("{name}", option).Replace("{value}", value);
                }
            }
            return commandOption;
        }

        public string GetHelpCommand(string commandName)
        {
            Command commandToPrint;
            if (Commands.TryGetValue(commandName, out commandToPrint))
            {
                StringBuilder sb = new StringBuilder();
                Dictionary<string, string> commandParametersToPrint = new Dictionary<string, string>();
                string value;
                sb.Append("  Locked Settings (values can't be overwritten): ").AppendLine();
                foreach(KeyValuePair<string, string> lockedSetting in commandToPrint.LockedSettings)
                {
                    value = lockedSetting.Value.Equals("default") ? FindSettingValue(lockedSetting.Key) : lockedSetting.Value;
                    sb.Append(string.Format("    {0} ({1})= {2}", lockedSetting.Key, FindSettingType(lockedSetting.Key), value)).AppendLine();
                    commandParametersToPrint[lockedSetting.Key] = value;
                }

                sb.AppendLine().Append("  Other Settings (values can be overwritten): ").AppendLine();
                foreach (KeyValuePair<string, string> optionalSettings in commandToPrint.Settings)
                {
                    value = optionalSettings.Value.Equals("default") ? FindSettingValue(optionalSettings.Key) : optionalSettings.Value;
                    sb.Append(string.Format("    {0} ({1})= {2}", optionalSettings.Key, FindSettingType(optionalSettings.Key), value)).AppendLine();
                    commandParametersToPrint[optionalSettings.Key] = value;
                }

                CompleteCommand completeCommand = BuildCommand(commandName, commandParametersToPrint);

                sb.AppendLine().Append("  It will run: ").AppendLine();
                sb.Append(string.Format("{0} {1} (global Settings)",completeCommand.ToolCommand, completeCommand.ParametersCommand));
                return sb.ToString();
            }
            return null;
        }

        private class CompleteCommand
        {
            public string ToolCommand { get { return _toolCommand; } }
            public string ParametersCommand { get { return _parametersCommand; } }

            internal CompleteCommand(string tool, string parameters)
            {
                _toolCommand = tool;
                _parametersCommand = parameters;
            }

            private string _toolCommand;
            private string _parametersCommand;
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
