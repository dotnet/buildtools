#!/usr/bin/env bash

__scriptpath=$(cd "$(dirname "$0")"; pwd -P)

"$__scriptpath/bootstrap/bootstrap.sh" -r "$__scriptpath" -t "$__scriptpath/Tools" -DotNetInstallBranch "rel/1.0.0-preview2.1"
__dotnet="$__scriptpath/Tools/dotnetcli/dotnet"
__toolruntime="$__scriptpath/Tools"

"$__dotnet" "$__toolruntime/run.exe" $*
exit $?
