#!/usr/bin/env bash

working_tree_root="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
echo $working_tree_root/corerun $working_tree_root/MSBuild.exe $*
$working_tree_root/corerun $working_tree_root/MSBuild.exe $*
if [ $? -ne 0 ]; then
    echo Error!
    exit 1 
fi
exit 0
