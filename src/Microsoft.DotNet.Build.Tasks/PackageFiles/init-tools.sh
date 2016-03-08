#!/usr/bin/env bash

__PROJECT_DIR=$1
__DOTNET_CMD=$2
__TOOLRUNTIME_DIR=$3
__TOOLS_DIR=$(cd "$(dirname "$0")"; pwd -P)
__MICROBUILD_VERSION=0.2.0
__PORTABLETARGETS_VERSION=0.1.1-dev
__MSBUILD_CONTENT_JSON="{\"dependencies\": {\"MicroBuild.Core\": \"${__MICROBUILD_VERSION}\", \"Microsoft.Portable.Targets\": \"${__PORTABLETARGETS_VERSION}\"},\"frameworks\": {\"dnxcore50\": {},\"net46\": {}}}"

__BUILDERRORLEVEL=0

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

OSName=$(uname -s)
case $OSName in
    Darwin)
        __PUBLISH_RID=osx.10.10-x64
        ;;

    Linux)
        source /etc/os-release
        if [ "$ID" == "centos" ]; then
            __PUBLISH_RID=centos.7-x64
        elif [ "$ID" == "rhel" ]; then
            __PUBLISH_RID=rhel.7-x64
        elif [ "$ID" == "ubuntu" ]; then
            __PUBLISH_RID=ubuntu.14.04-x64
        elif [ "$ID" == "debian" ]; then
            __PUBLISH_RID=debian.8.2-x64
        else
            echo "Unsupported Linux distribution '$ID' detected. Downloading ubuntu-x64 tools."
            __PUBLISH_RID=ubuntu.14.04-x64
        fi
        ;;

    *)
        echo "Unsupported OS '$OSName' detected. Downloading ubuntu-x64 tools."
        __PUBLISH_RID=ubuntu.14.04-x64
        ;;
esac

cp -R $__TOOLS_DIR/* $__TOOLRUNTIME_DIR

cd $__TOOLS_DIR/tool-runtime/
$__DOTNET_CMD restore --source https://dotnet.myget.org/F/dotnet-core/api/v3/index.json --source https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json --source https://api.nuget.org/v3/index.json
$__DOTNET_CMD publish -f dnxcore50 -r ${__PUBLISH_RID} -o $__TOOLRUNTIME_DIR
chmod a+x $__TOOLRUNTIME_DIR/corerun

if [ -n "$BUILDTOOLS_OVERRIDE_RUNTIME" ]; then
    find $__TOOLRUNTIME_DIR -name *.ni.* | xargs rm 2>/dev/null
    cp -R $BUILDTOOLS_OVERRIDE_RUNTIME/* $__TOOLRUNTIME_DIR
fi

# Copy Portable Targets Over to ToolRuntime
mkdir "$__TOOLS_DIR/portableTargets"
echo $__MSBUILD_CONTENT_JSON > "$__TOOLS_DIR/portableTargets/project.json"
cd "$__TOOLS_DIR/portableTargets"
"$__DOTNET_CMD" restore --source https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json --packages "$__TOOLS_DIR/portableTargets/packages"
cp -R "$__TOOLS_DIR/portableTargets/packages/Microsoft.Portable.Targets/${__PORTABLETARGETS_VERSION}/contentFiles/any/any/." "$__TOOLRUNTIME_DIR/."
cp -R "$__TOOLS_DIR/portableTargets/packages/MicroBuild.Core/${__MICROBUILD_VERSION}/build/." "$__TOOLRUNTIME_DIR/."

# Temporary Hacks to fix couple of issues in the msbuild and roslyn nuget packages
cp "$__TOOLRUNTIME_DIR/corerun" "$__TOOLRUNTIME_DIR/corerun.exe"
mv "$__TOOLRUNTIME_DIR/Microsoft.CSharp.targets" "$__TOOLRUNTIME_DIR/Microsoft.CSharp.Targets"

exit $__BUILDERRORLEVEL
