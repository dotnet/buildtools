@echo off
setlocal

set PROJECT_DIR=%~1
set DOTNET_CMD=%~2
set TOOLRUNTIME_DIR=%~3
IF [%BUILDTOOLS_TARGET_RUNTIME%]==[] set BUILDTOOLS_TARGET_RUNTIME=win7-x64
IF [%BUILDTOOLS_NET45_TARGET_RUNTIME%]==[] set BUILDTOOLS_NET45_TARGET_RUNTIME=win7-x86
set BUILDTOOLS_PACKAGE_DIR=%~dp0
set MICROBUILD_VERSION=0.2.0
set PORTABLETARGETS_VERSION=0.1.1-dev
set ROSLYNCOMPILERS_VERSION=1.2.0-beta1-20160202-02
set MSBUILD_CONTENT_JSON={"dependencies": { "MicroBuild.Core": "%MICROBUILD_VERSION%", "Microsoft.Portable.Targets": "%PORTABLETARGETS_VERSION%", "Microsoft.Net.Compilers": "%ROSLYNCOMPILERS_VERSION%"},"frameworks": {"dnxcore50": {},"net46": {}}}

if not exist "%PROJECT_DIR%" (
  echo ERROR: Cannot find project root path at [%PROJECT_DIR%]. Please pass in the source directory as the 1st parameter.
  exit /b 1
)

if not exist "%DOTNET_CMD%" (
  echo ERROR: Cannot find dotnet cli at [%DOTNET_CMD%]. Please pass in the path to dotnet.exe as the 2nd parameter.
  exit /b 1
)

ROBOCOPY "%BUILDTOOLS_PACKAGE_DIR%\." "%TOOLRUNTIME_DIR%" /E

cd "%BUILDTOOLS_PACKAGE_DIR%\tool-runtime\"
call "%DOTNET_CMD%" restore --source https://dotnet.myget.org/F/dotnet-core/api/v3/index.json --source https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json --source https://api.nuget.org/v3/index.json
call "%DOTNET_CMD%" publish -f dnxcore50 -r %BUILDTOOLS_TARGET_RUNTIME% -o "%TOOLRUNTIME_DIR%"
call "%DOTNET_CMD%" publish -f net45 -r %BUILDTOOLS_NET45_TARGET_RUNTIME% -o "%TOOLRUNTIME_DIR%\net45"

:: Copy Portable Targets Over to ToolRuntime
if not exist "%BUILDTOOLS_PACKAGE_DIR%\portableTargets" mkdir "%BUILDTOOLS_PACKAGE_DIR%\portableTargets"
echo %MSBUILD_CONTENT_JSON% > "%BUILDTOOLS_PACKAGE_DIR%\portableTargets\project.json"
cd "%BUILDTOOLS_PACKAGE_DIR%\portableTargets\"
call "%DOTNET_CMD%" restore --source https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json --source https://api.nuget.org/v3/index.json --packages "%BUILDTOOLS_PACKAGE_DIR%\portableTargets\packages"
Robocopy "%BUILDTOOLS_PACKAGE_DIR%\portableTargets\packages\Microsoft.Portable.Targets\%PORTABLETARGETS_VERSION%\contentFiles\any\any\." "%TOOLRUNTIME_DIR%\." /E
Robocopy "%BUILDTOOLS_PACKAGE_DIR%\portableTargets\packages\MicroBuild.Core\%MICROBUILD_VERSION%\build\." "%TOOLRUNTIME_DIR%\." /E

:: Copy Roslyn Compilers Over to ToolRuntime
Robocopy "%BUILDTOOLS_PACKAGE_DIR%\portableTargets\packages\Microsoft.Net.Compilers\%ROSLYNCOMPILERS_VERSION%\." "%TOOLRUNTIME_DIR%\net45\roslyn\." /E

exit /b %ERRORLEVEL%
