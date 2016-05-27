#!/usr/bin/env bash

working_tree_root="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo "Running init-tools.sh"
$working_tree_root/init-tools.sh

_toolRuntime=$working_tree_root/Tools
_dotnet=$_toolRuntime/dotnetcli/dotnet

if [ $# == 0 ]; then
  echo Running: $_dotnet $_toolRuntime/.runtime/executor.exe -default
  $_dotnet $_toolRuntime/.runtime/executor.exe -default
  exit 0
fi

echo Running: $_dotnet $_toolRuntime/.runtime/executor.exe $*
$_dotnet $_toolRuntime/.runtime/executor.exe $*
exit 0
