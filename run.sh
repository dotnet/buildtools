#!/usr/bin/env bash

__scriptpath=$(cd "$(dirname "$0")"; pwd -P)

"$__scriptpath/bootstrap/bootstrap.sh" -r "$__scriptpath" -t "$__scriptpath/Tools"
__dotnet="$__scriptpath/Tools/dotnetcli/dotnet"
__toolruntime="$__scriptpath/Tools"

"$__dotnet" "$__toolruntime/run.exe" $*
exit $?
