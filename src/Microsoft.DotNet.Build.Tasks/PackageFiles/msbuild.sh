#!/usr/bin/env bash

working_tree_root="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
$working_tree_root/dotnetcli/dotnet $working_tree_root/MSBuild.exe $*
exit $?
