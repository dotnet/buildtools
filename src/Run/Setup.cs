// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        private string ParseSettingValue(string inputValue)
        {
            string value = string.Empty;
            int length = inputValue.Length;
            for (int i = 0; i < length; i++)
            {
                if (i != length - 1 && inputValue[i] == '$')
                {
                    if (inputValue[i + 1] == '{')
                    {
                        int j;
                        string memberName = string.Empty;
                        for (j = i + 2; inputValue[j] != '}' && j < length; j++)
                            memberName += inputValue[j];

                        // The string is not of format ${}, just add the chars to the value.
                        if (j == length)
                            value += "${" + memberName;
                        else
                            value += SettingValueProvider.Get(memberName);

                        // Put i to j counter.
                        i = j;
                    }
                    else
                    {
                        // If next char is not { then add $ to the value.
                        value += inputValue[i];
                    }
                }
                else
                {
                    value += inputValue[i];
                }
            }

            return value;
        }

        private string FindSettingValue(string valueToFind)
        {
            Setting value;
            if (Settings.TryGetValue(valueToFind, out value))
            {
                return ParseSettingValue(value.DefaultValue);
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

        public int ExecuteCommand(string commandSelectedByUser, List<string> parametersSelectedByUser)
        {
            CompleteCommand commandToRun = BuildCommand(commandSelectedByUser, parametersSelectedByUser);
            if (commandToRun != null)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("Running: {0} {1}", commandToRun.ToolCommand, commandToRun.ParametersCommand);
                Console.ResetColor();

                int result = RunProcess.ExecuteProcess(commandToRun.ToolCommand, commandToRun.ParametersCommand);
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

        private CompleteCommand BuildCommand(string commandSelectedByUser, List<string> parametersSelectedByUser, Dictionary<string, string> parameters = null)
        {
            Command commandToExecute;
            if (!Commands.TryGetValue(commandSelectedByUser, out commandToExecute))
            {
                Console.Error.WriteLine("Error: The command {0} is not specified in the Json file.", commandSelectedByUser);
                return null;
            }

            string commandTool = GetTool(commandToExecute, Os, ConfigurationFilePath, parametersSelectedByUser);
            if (string.IsNullOrEmpty(commandTool))
            {
                return null;
            }

            if (parameters == null)
            {
                if (BuildRequiredValueSettingsForCommand(commandToExecute, parametersSelectedByUser, SettingParameters) &&
                    BuildDefaultValueSettingsForCommand(commandToExecute, SettingParameters) &&
                    ValidExtraParametersForCommand(SettingParameters["ExtraParameters"], SettingParameters))
                {
                    string commandParameters = BuildParametersForCommand(SettingParameters, SettingParameters["toolName"]);
                    CompleteCommand completeCommand = new CompleteCommand(commandTool, commandParameters);
                    return completeCommand;
                }
                return null;
            }
            else
            {
                string commandParameters = BuildParametersForCommand(parameters, SettingParameters["toolName"]);
                CompleteCommand completeCommand = new CompleteCommand(commandTool, commandParameters);
                return completeCommand;
            }
        }

        private string BuildParametersForCommand(Dictionary<string, string> commandParameters, string toolName)
        {
            string commandSetting = string.Empty;

            Tools[toolName].osSpecific[Os].TryGetValue("defaultParameters", out commandSetting);            

            foreach (KeyValuePair<string, string> parameters in commandParameters)
            {
                if (!parameters.Key.Equals("toolName") && !string.IsNullOrEmpty(parameters.Value))
                {
                    string value = parameters.Value.Equals("default") ? FindSettingValue(parameters.Key) : ParseSettingValue(parameters.Value);
                    commandSetting += string.Format(" {0}", FormatSetting(parameters.Key, value, FindSettingType(parameters.Key), toolName));
                }
            }
            return commandSetting;
        }

        private bool BuildRequiredValueSettingsForCommand(Command commandToExecute, List<string> requiredSettings, Dictionary<string, string> commandValues)
        {
            foreach (string reqSetting in requiredSettings)
            {
                foreach (KeyValuePair<string, string> sett in commandToExecute.Alias[reqSetting].Settings)
                {
                    string value = sett.Value;
                    string currentValue;
                    if (commandValues.TryGetValue(sett.Key, out currentValue))
                    {
                        if (string.IsNullOrEmpty(currentValue) || currentValue.Equals("default"))
                        {
                            commandValues[sett.Key] = value;
                        }
                        else if (!value.Equals("default") && !value.Equals(currentValue))
                        {
                            Console.Error.WriteLine("Error: The value for setting {0} can't be overwriten.", sett.Key);
                            return false;
                        }
                    }
                    else if (!sett.Key.Equals("toolName"))
                    {
                        Console.Error.WriteLine("Error: The setting {0} is not specified in the Json file.", sett.Key);
                        return false;
                    }
                }
            }
            return true;
        }

        private bool BuildDefaultValueSettingsForCommand(Command commandToExecute, Dictionary<string, string> commandValues)
        {
            foreach (KeyValuePair<string, string> optSetting in commandToExecute.DefaultValues.Settings)
            {
                string currentValue;
                if (commandValues.TryGetValue(optSetting.Key, out currentValue))
                {
                    if (string.IsNullOrEmpty(currentValue))
                    {
                        commandValues[optSetting.Key] = optSetting.Value;
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

        private bool ValidExtraParametersForCommand(string extraParameters, Dictionary<string, string> commandValues)
        {
            int namePos, valuePos;
            string tempParam, name, value;
            if (string.IsNullOrEmpty(extraParameters))
            {
                return true;
            }

            string[] extraA = extraParameters.Split(' ');
            foreach (string param in extraA)
            {
                namePos = 0;
                valuePos = param.Length;
                tempParam = param;

                namePos = param.IndexOf(":");
                if (namePos != -1)
                {
                    tempParam = param.Substring(namePos + 1);
                }

                valuePos = tempParam.IndexOf("=");
                if (valuePos != -1)
                {
                    name = tempParam.Substring(0, valuePos);
                    value = tempParam.Substring(valuePos + 1);
                }
                else
                {
                    name = tempParam;
                    value = string.Empty;
                }

                string paramValue;
                if (commandValues.TryGetValue(name, out paramValue) && !string.IsNullOrEmpty(paramValue) && !paramValue.Equals("default") && !value.Equals(paramValue))
                {
                    Console.Error.WriteLine("Error: The value for setting {0} can't be overwriten.", name);
                    return false;
                }

            }
            return true;
        }

        private string GetTool(Command commandToExecute, string os, string configPath, List<string> parametersSelectedByUser)
        {
            string toolname = commandToExecute.DefaultValues.ToolName;

            string project = GetProject(commandToExecute, parametersSelectedByUser);

            if (Tools.ContainsKey(toolname))
            {
                SettingParameters["toolName"] = toolname;
                string value = string.Empty;
                if(Tools[toolname].osSpecific[os].TryGetValue("path", out value) && !string.IsNullOrEmpty(value))
                {
                    return Path.GetFullPath(Path.Combine(configPath, value));
                }
                else if (Tools[toolname].osSpecific[os].TryGetValue("filesExtension", out value) && !string.IsNullOrEmpty(value))
                {
                    string extension = value;
                    return Path.GetFullPath(Path.Combine(configPath, string.Format("{0}.{1}", project, extension)));
                }
                else
                {
                    Console.Error.WriteLine("Error: The process {0} has empty values for path and filesExtension properties. It is mandatory that one of the two has a value.", toolname);
                    return string.Empty;
                }
            }
            Console.Error.WriteLine("Error: The process {0} is not specified in the Json file.", toolname);
            return string.Empty;
        }

        private string GetProject(Command commandToExecute, List<string> parametersSelectedByUser)
        {
            string project = string.Empty;
            bool moreThanOneProject = false;
            foreach (string param in parametersSelectedByUser)
            {
                if (commandToExecute.Alias[param].Settings.TryGetValue("Project", out project))
                {
                    if (moreThanOneProject)
                    {
                        Console.Error.WriteLine("Error: There can only be one project execution per command.");
                        return string.Empty;
                    }
                    moreThanOneProject = true;
                }
            }

            if (string.IsNullOrEmpty(project))
            {
                project = commandToExecute.DefaultValues.Project;
            }

            return project;
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
                    if (toolFormat.ValueTypes.TryGetValue(type, out commandOption))
                    {
                        commandOption = commandOption.Replace("{name}", option).Replace("{value}", value);
                    }
                    else
                    {
                        Console.Error.WriteLine("The type \"{0}\" is not defined as a Value Type of the tool \"{1}\". Parameter ignored", type, toolName);
                        return null;
                    }
                }
            }
            return commandOption;
        }

        public string GetHelpCommand(string commandName, string alias = null)
        {
            Command commandToPrint;
            if (Commands.TryGetValue(commandName, out commandToPrint))
            {
                StringBuilder sb = new StringBuilder();
                Dictionary<string, string> commandParametersToPrint = new Dictionary<string, string>();

                sb.AppendLine().Append("Settings: ").AppendLine();
                sb.Append(GetHelpAlias(commandToPrint.Alias[alias].Settings, commandParametersToPrint));

                //sb.AppendLine().Append("Default Settings for action (values can be overwritten): ").AppendLine();
                sb.Append(GetHelpAlias(commandToPrint.DefaultValues.Settings, commandParametersToPrint));

                CompleteCommand completeCommand = BuildCommand(commandName, new List<string>(alias.Split(' ')), commandParametersToPrint);

                sb.AppendLine().Append("It will run: ").AppendLine();
                sb.Append(string.Format("{0} {1}", completeCommand.ToolCommand, completeCommand.ParametersCommand));
                return sb.ToString();
            }
            return null;
        }

        private string GetHelpAlias(Dictionary<string, string> settings, Dictionary<string, string> commandParametersToPrint)
        {
            StringBuilder sb = new StringBuilder();

            foreach (KeyValuePair<string, string> setting in settings)
            {
                string value = setting.Value.Equals("default") ? FindSettingValue(setting.Key) : setting.Value;
                sb.Append(string.Format("    {0} = {2}", setting.Key, FindSettingType(setting.Key), value)).AppendLine();
                commandParametersToPrint[setting.Key] = string.IsNullOrEmpty(value) ? "True" : value;
            }
            return sb.ToString();
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

    public class AliasPerCommand
    {
        public string Description { get; set; }
        public Dictionary<string, string> Settings { get; set; }
    }

    public class DefaultValuesPerCommand
    {
        public string Project { get; set; }
        public string ToolName { get; set; }
        public Dictionary<string, string> Settings { get; set; }
    }

    public class Command
    {
        public Dictionary<string, AliasPerCommand> Alias { get; set; }
        public DefaultValuesPerCommand DefaultValues { get; set; }
    }

    public class Tool
    {
        public Dictionary<string, Dictionary<string, string>> osSpecific { get; set; }
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
