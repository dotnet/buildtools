#!/usr/bin/env bash
set -euo pipefail

# Restores crossgen and runs it on all tools components.
usage()
{
    echo "crossgen.sh <directory> <rid>"
    echo "    Restores crossgen and runs it on all assemblies in <directory>."
    exit 0
}

restore_crossgen()
{
    __crossgen=$__sharedFxDir/crossgen
    if [ -e $__crossgen ]; then
        return
    fi

    __pjDir=$__toolsDir/crossgen
    mkdir -p $__pjDir
    echo "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><DisableImplicitNuGetFallbackFolder>false</DisableImplicitNuGetFallbackFolder><TreatWarningsAsErrors>false</TreatWarningsAsErrors><NoWarn>\$(NoWarn);NU1605;NU1103</NoWarn><TargetFramework>netcoreapp2.0</TargetFramework><DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences><RuntimeIdentifiers>$__packageRid</RuntimeIdentifiers></PropertyGroup><ItemGroup><PackageReference Include=\"Microsoft.NETCore.App\" Version=\"$__sharedFxVersion\" /></ItemGroup></Project>" > "$__pjDir/crossgen.csproj"
    $__dotnet restore $__pjDir/crossgen.csproj --packages $__packagesDir --source $__MyGetFeed
    __crossgen=$__packagesDir/runtime.$__packageRid.microsoft.netcore.app/$__sharedFxVersion/tools/crossgen
    if [ ! -e $__crossgen ]; then
        echo "The crossgen executable could not be found at "$__crossgen". Aborting crossgen.sh."
        exit 1
    fi
    # Executables restored with .NET Core 2.0 do not have executable permission flags. https://github.com/NuGet/Home/issues/4424
    chmod +x $__crossgen
}

crossgen_everything()
{
    echo "Running crossgen on all assemblies in $__targetDir."
    for file in $__targetDir/*.{dll,exe}
    do
        if [[ ($(basename $file) != "Microsoft.Build.Framework.dll") && ($(basename $file) != "Microsoft.DotNet.Build.Tasks.dll") ]]; then
            crossgen_single $file & pid=$!
            __pids+=" $pid"
        fi
    done

    trap "kill $__pids 2&> /dev/null" SIGINT
    wait $__pids
    echo "Crossgen finished."
}

crossgen_single()
{
    __file=$1
    if [[ $__file != *.ni.dll && $__file != *.ni.exe ]]; then
        if [[ ($__file == *.dll && -e ${__file/.dll/.ni.dll}) || ($__file == *.exe && -e ${__file/.exe/.ni.exe}) ]]; then
            echo "$__file has already been crossgen'd.  Skipping."
        else
            set +e
            $__crossgen /Platform_Assemblies_Paths $__sharedFxDir:$__toolsDir /JitPath $__sharedFxDir/libclrjit.$__libraryExtension /nologo /MissingDependenciesOK /ReadyToRun $__file > /dev/null
            if [ $? -eq 0 ]; then
                __outname="${__file/.dll/.ni.dll}"
                __outname="${__outname/.exe/.ni.exe}"
                echo "$__file -> $__outname"
            else
                echo "Unable to successfully compile $__file"
            fi
            set -e
        fi
    fi
}

if [ ! -z ${BUILDTOOLS_SKIP_CROSSGEN:-} ]; then
    echo "BUILDTOOLS_SKIP_CROSSGEN is set. Skipping crossgen step."
    exit 0
fi

if [[ -z "${1:-}" || "$1" == "-?" || "$1" == "--help" || "$1" == "-h" ]]; then
    usage
fi

__MyGetFeed=${BUILDTOOLS_CROSSGEN_FEED:-https://dotnet.myget.org/F/dotnet-core/api/v3/index.json}
__targetDir=$1
__packageRid=${2:-}
__scriptpath=$(cd "$(dirname "$0")"; pwd -P)
__toolsDir=$__scriptpath/../Tools
__dotnet=$__toolsDir/dotnetcli/dotnet
__packagesDir="${NUGET_PACKAGES:-${__scriptpath}/../packages}"
__mncaFolder=$__toolsDir/dotnetcli/shared/Microsoft.NETCore.App
__sharedFxVersion=`ls $__mncaFolder | sed 'r/\([0-9]\+\).*/\1/g' | sort -n | tail -1`
__sharedFxDir=$__toolsDir/dotnetcli/shared/Microsoft.NETCore.App/$__sharedFxVersion/

if [ -z "$__packageRid" ]; then
    case $(uname -s) in
        Darwin)
            __packageRid=osx-x64
            ;;
        Linux)
            __packageRid=linux-x64
            ;;
        *)
            echo "Unsupported OS $(uname -s) detected. Skipping crossgen of the toolset."
            exit 0
            ;;
    esac
fi

if [ "$__packageRid" == "osx-x64" ]; then
    __libraryExtension=dylib
else
    __libraryExtension=so
fi

restore_crossgen
crossgen_everything
exit 0
