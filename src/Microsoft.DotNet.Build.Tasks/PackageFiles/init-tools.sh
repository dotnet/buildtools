#!/usr/bin/env bash

__PROJECT_DIR=$1
__DOTNET_CMD=$2
__TOOLRUNTIME_DIR=$3
__PACKAGES_DIR=$4
if [ "$__PACKAGES_DIR" == "" ]; then __PACKAGES_DIR=${__TOOLRUNTIME_DIR}; fi
__TOOLS_DIR=$(cd "$(dirname "$0")"; pwd -P)
__MICROBUILD_VERSION=0.2.0
__PORTABLETARGETS_VERSION=0.1.1-dev
__MSBUILD_CONTENT_JSON="{\"dependencies\": {\"MicroBuild.Core\": \"${__MICROBUILD_VERSION}\", \"Microsoft.Portable.Targets\": \"${__PORTABLETARGETS_VERSION}\"},\"frameworks\": {\"netcoreapp1.0\": {},\"net46\": {}}}"
__INIT_TOOLS_RESTORE_ARGS="--source https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json --source https://api.nuget.org/v3/index.json ${__INIT_TOOLS_RESTORE_ARGS}"
__TOOLRUNTIME_RESTORE_ARGS="--source https://dotnet.myget.org/F/dotnet-core/api/v3/index.json ${__INIT_TOOLS_RESTORE_ARGS}"

if [ ! -d "$__PROJECT_DIR" ]; then
    echo "ERROR: Cannot find project root path at '$__PROJECT_DIR'. Please pass in the source directory as the 1st parameter."
    exit 1
fi

if [ ! -e "$__DOTNET_CMD" ]; then
    echo "ERROR: Cannot find dotnet.exe at path '$__DOTNET_CMD'. Please pass in the path to dotnet.exe as the 2nd parameter."
    exit 1
fi

if [ -z "$__TOOLRUNTIME_DIR" ]; then
    echo "ERROR: Please pass in the tools directory as the 3rd parameter."
    exit 1
fi

if [ ! -d "$__TOOLRUNTIME_DIR" ]; then
    mkdir $__TOOLRUNTIME_DIR
fi

if [ -z "$__PUBLISH_RID" ]; then
    OSName=$(uname -s)
    case $OSName in
        Darwin)
            __PUBLISH_RID=osx.10.10-x64
            ;;

        Linux)
            if [ ! -e /etc/os-release ]; then
                echo "Can not determine distribution, assuming Ubuntu 14.04"
                __PUBLISH_RID=ubuntu.14.04-x64
            else
                source /etc/os-release
                if [[ "$ID" == "ubuntu" && "$VERSION_ID" != "14.04" && "$VERSION_ID" != "16.04" ]]; then
                echo "Unsupported Ubuntu version, falling back to Ubuntu 14.04"
                __PUBLISH_RID=ubuntu.14.04-x64
                else
                __PUBLISH_RID=$ID.$VERSION_ID-x64
                fi
            fi

            # RHEL bumps their OS Version with minor releases, but we only put the "rhel.7-x64" RID in our
            # tool runtime, since there's binary compatibility between minor versions.

            if [[ $__PUBLISH_RID == rhel.7*-x64 ]]; then
                __PUBLISH_RID=rhel.7-x64
            fi
            ;;

        *)
            echo "Unsupported OS '$OSName' detected. Downloading ubuntu-x64 tools."
            __PUBLISH_RID=ubuntu.14.04-x64
            ;;
    esac
fi

cp -R $__TOOLS_DIR/* $__TOOLRUNTIME_DIR

__TOOLRUNTIME_PROJECTJSON=$__TOOLS_DIR/tool-runtime/project.json
echo "Running: $__DOTNET_CMD restore \"${__TOOLRUNTIME_PROJECTJSON}\" $__TOOLRUNTIME_RESTORE_ARGS"
$__DOTNET_CMD restore "${__TOOLRUNTIME_PROJECTJSON}" $__TOOLRUNTIME_RESTORE_ARGS
if [ "$?" != "0" ]; then
    echo "ERROR: An error occured when running: '$__DOTNET_CMD restore \"${__TOOLRUNTIME_PROJECTJSON}\"'. Please check above for more details."
    exit 1
fi
echo "Running: $__DOTNET_CMD publish \"${__TOOLRUNTIME_PROJECTJSON}\" -f netcoreapp1.0 -r ${__PUBLISH_RID} -o $__TOOLRUNTIME_DIR"
$__DOTNET_CMD publish "${__TOOLRUNTIME_PROJECTJSON}" -f netcoreapp1.0 -r ${__PUBLISH_RID} -o $__TOOLRUNTIME_DIR
if [ "$?" != "0" ]; then
    echo "ERROR: An error ocurred when running: '$__DOTNET_CMD publish \"${__TOOLRUNTIME_PROJECTJSON}\"'. Please check above for more details."
    exit 1
fi

if [ -n "$BUILDTOOLS_OVERRIDE_RUNTIME" ]; then
    find $__TOOLRUNTIME_DIR -name *.ni.* | xargs rm 2>/dev/null
    cp -R $BUILDTOOLS_OVERRIDE_RUNTIME/* $__TOOLRUNTIME_DIR
fi

# Copy Portable Targets Over to ToolRuntime
if [ ! -d "${__PACKAGES_DIR}/generated" ]; then mkdir "${__PACKAGES_DIR}/generated"; fi
__PORTABLETARGETS_PROJECTJSON=${__PACKAGES_DIR}/generated/project.json
echo $__MSBUILD_CONTENT_JSON > "${__PORTABLETARGETS_PROJECTJSON}"
echo "Running: \"$__DOTNET_CMD\" restore \"${__PORTABLETARGETS_PROJECTJSON}\" $__INIT_TOOLS_RESTORE_ARGS --packages \"${__PACKAGES_DIR}/.\""
$__DOTNET_CMD restore "${__PORTABLETARGETS_PROJECTJSON}" $__INIT_TOOLS_RESTORE_ARGS --packages "${__PACKAGES_DIR}/."
if [ "$?" != "0" ]; then
    echo "ERROR: An error ocurred when running: '$__DOTNET_CMD restore \"${__PORTABLETARGETS_PROJECTJSON}\"'. Please check above for more details."
    exit 1
fi
cp -R "${__PACKAGES_DIR}/Microsoft.Portable.Targets/${__PORTABLETARGETS_VERSION}/contentFiles/any/any/." "$__TOOLRUNTIME_DIR/."
cp -R "${__PACKAGES_DIR}/MicroBuild.Core/${__MICROBUILD_VERSION}/build/." "$__TOOLRUNTIME_DIR/."

# Temporary Hacks to fix couple of issues in the msbuild and roslyn nuget packages
mv "$__TOOLRUNTIME_DIR/Microsoft.CSharp.targets" "$__TOOLRUNTIME_DIR/Microsoft.CSharp.Targets"

exit 0
