Run Command Tool
===========================
The Run Command Tool has a published contract of inputs and outputs to encapsulate the dev workflow. It parses arguments and maps properties to tools in order to run the command given by the user. It also provides documentation of the most common settings we use per repo, and the commands we can execute.

The source code of the tool lives in the Build Tools repo and it is included after the Build Tools package version 1.0.26-prerelease-00601-01. 
In order to on board the Run Command Tool, every repo should have a [run.cmd/sh](../run.cmd) script file that is in charge of:
- Running init-tools (to download the Build Tools package) 
- Execute run.exe.

The Run Command Tool uses a config.json file that has all the information needed in other for the tool to work.

Config.json
---------------------------
The config.json file has three major sections:
- Settings: Properties, variables, settings that are parsed according to the format of the tool that is going to be run. It provides documentation about what the setting is, the possible values it can have and the default value.
- Commands: The set of actions the tool should execute (clean, sync, build …). Each command has a set of Alias that describe different behaviors that the command can run. For example: run.exe build tests and run.exe build packages will produce different output but both are part of the build command.
- Tools: Set of tools the run command will run (I.e. msbuild, cmd/sh). 

To access the information located in the config.json file, do run.cmd -? . This helps the commands and settings to be self-documented.

[Config.json](/configTemplate.json) template.


The Build Tools repo is now using the Run Command Tool as the default dev workflow.
