#!/usr/bin/env bash

__scriptpath=$(cd "$(dirname "$0")"; pwd -P)

# Disable telemetry, first time experience, and global sdk look for the CLI
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_MULTILEVEL_LOOKUP=0

"$__scriptpath/bootstrap/bootstrap.sh" -r "$__scriptpath" -t "$__scriptpath/Tools" -DotNetInstallBranch "rel/1.0.0-preview2.1"
__dotnet="$__scriptpath/Tools/dotnetcli/dotnet"
__toolruntime="$__scriptpath/Tools"

"$__dotnet" "$__toolruntime/run.exe" $*
exit $?
