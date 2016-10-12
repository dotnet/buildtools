Run Command Tool
===========================
The Run Command Tool has a published contract of inputs and outputs to encapsulate the [dev workflow](Dev-workflow.md). It parses arguments and maps properties to tools in order to run the command given by the user. It also provides documentation of the most common settings we use per repo, and the commands we can execute.

The source code of the tool lives in the Build Tools repo and it is included after the Build Tools package version 1.0.26-prerelease-00601-01. 
In order to on board the Run Command Tool, every repo should have a [run.cmd/sh](../run.cmd) script file that is in charge of:
- Running init-tools (to download the Build Tools package) 
- Executing run.exe.

The Run Command Tool uses a config.json file that has all the information needed in order for the tool to work.

Config.json
---------------------------
The config.json file has three major sections:

**Settings**

Properties, variables, settings that are parsed according to the format of the tool that is going to be run. It provides documentation about what the setting is, the possible values it can have and the default value.

The structure of it is:
```
"settings": {
    "SettingName":{
      "description": "Brief description about the function of the Setting.",
      "valueType": "Value type name.",
      "values": ["Array of possible values for the Setting"],
      "defaultValue": "Default value for the Setting."
    }
 }
```
The value type could be per tool used (under the tools section) or run tool specific values like passThrough and runToolSetting. 

- passThrough: No parsing is needed for the value of the Setting. The run tool will pass along the value of the Setting as-is.

- runToolSetting: A specific setting for the run tool, e.g. the '[RunQuiet](https://github.com/dotnet/buildtools/blob/master/src/Run/Setup.cs#L176)' Setting .

Note: The Run Command Tool needs a `Project` setting to specify the project specific commands will apply to. It can be specified per command, per alias or be set by the user.

**Commands**

The set of actions the tool should execute (clean, sync, build …). Each command has a set of `alias` that describe different behaviors that the command can run, a `defaultValues` section which contains the `toolName` and default settings that are always going to be passed to the command, and an optional `defaultAlias` value specifying which alias to call in the case of the tool being invoked with a command without an accompanying alias.

The structure of it is:
```
"commands": {
    "CommandName":{
      "alias":{
        "aliasName":{
          "description": "Brief description about the function of the alias in the given command",
          "settings":{
            "SettingName": "Value for the Setting. The value needs to be part of the values array of the Setting.",
            "SettingName": "If the value is specified as 'default', the command will use the default value defined for the Setting.",
            "Example" : "default"
          }
        },
      },
      "defaultValues":{
        "defaultAlias" : "Optional alias to use if no alias is passed",
        "toolName": "Each command needs only one tool, here we specify the name of the tool.",
        "settings": {
          "SettingName":"These settings are always going to be applied when calling the command CommandName."
        }
      }
    }
 }
```

**Tools** 

Set of tools the run command will run (I.e. msbuild, cmd/sh). 

The structure of it is:
```
"tools": {
    "toolName": {
      "osSpecific":{
        "windows": {
          "defaultParameters": "values we always want to pass when using this tool.",
          "path": "Where we can find the tool.",
          "filesExtension": "Extension of the files that the tool is going to use."
        },
        "unix":{
          "defaultParameters": "values we always want to pass when using this tool.",
          "path": "Where we can find the tool.",
          "filesExtension": "Extension of the files that the tool is going to use."
        }
      },
      "valueTypes": {
        "typeName": "Explains how to format the Setting for the specific tool."
      }
    }
  }
}
```
Currently we have scripts for windows (.cmd) and for unix (.sh) that for discoverability have the same name. The `fileExtension` property is in charge of appending the corresponding extension to the name of the file specified in the `Project` Setting.

For example, using the following config.json:
```
{
 "settings": {
    "Project": {
      "description": "Project where the commands are going to be applied.",
      "valueType": "passThrough",
      "values": [],
      "defaultValue": ""
    }
 },
 "commands": {
    "build-native": {
      "alias": {},
      "defaultValues": {
        "toolName": "terminal",
        "Project": "src/Native/build-native",
        "settings": {}
      }
    },
 },
 "tools": {
    "terminal": {
      "osSpecific": {
        "windows": {
          "filesExtension": "cmd"
        },
        "unix": {
          "filesExtension": "sh"
        }
      },
      "valueTypes": {}
    }
 }
}
```
One could call `run.exe build-native` and if running in Windows, this would execute the Windows-specific `src/Native/build-native.cmd` script, while the same command in Unix would execute the `src/Native/build-native.sh` bash script.

To access the information located in the config.json file, call `run.cmd -?` . This helps the commands and settings to be self-documented.

Build Tools [Config.json](../config.json).
