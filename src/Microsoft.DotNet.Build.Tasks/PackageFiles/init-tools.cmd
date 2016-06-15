@if "%_echo%" neq "on" echo off
setlocal

set PROJECT_DIR=%~1
set DOTNET_CMD=%~2
set TOOLRUNTIME_DIR=%~3
set PROJECT_JSON_FILE=%4
set PACKAGES_DIR=%5
if [%PACKAGES_DIR%] == [] set PACKAGES_DIR=%TOOLRUNTIME_DIR%
:: Remove quotes to the packages directory
set PACKAGES_DIR=%PACKAGES_DIR:"=%
IF [%BUILDTOOLS_NET45_TARGET_RUNTIME%]==[] set BUILDTOOLS_NET45_TARGET_RUNTIME=win7-x86
set MICROBUILD_VERSION=0.2.0
set PORTABLETARGETS_VERSION=0.1.1-dev
set ROSLYNCOMPILERS_VERSION=1.3.0-beta1-20160429-01
set MSBUILD_CONTENT_JSON={"dependencies": { "MicroBuild.Core": "%MICROBUILD_VERSION%", "Microsoft.Portable.Targets": "%PORTABLETARGETS_VERSION%", "Microsoft.Net.Compilers": "%ROSLYNCOMPILERS_VERSION%"},"frameworks": {"netcoreapp1.0": {},"net46": {}}}

if not exist "%PROJECT_DIR%" (
  echo ERROR: Cannot find project root path at [%PROJECT_DIR%]. Please pass in the source directory as the 1st parameter.
  exit /b 1
)

if not exist "%DOTNET_CMD%" (
  echo ERROR: Cannot find dotnet cli at [%DOTNET_CMD%]. Please pass in the path to dotnet.exe as the 2nd parameter.
  exit /b 1
)

:: Workaround because _._ content file does not get coppied over
echo "" > "%TOOLRUNTIME_DIR%\_._"

@echo on
call "%DOTNET_CMD%" publish "%PROJECT_JSON_FILE%" -f net45 -r %BUILDTOOLS_NET45_TARGET_RUNTIME% -o "%TOOLRUNTIME_DIR%\net45"
set NET45_PUBLISH_ERROR_LEVEL=%ERRORLEVEL%
@if "%_echo%" neq "on" echo off
if not [%NET45_PUBLISH_ERROR_LEVEL%]==[0] (
	echo ERROR: An error ocurred when running: '"%DOTNET_CMD%" publish "%TOOLRUNTIME_PROJECTJSON%" -f net45'. Please check above for more details.
	exit /b %NET45_PUBLISH_ERROR_LEVEL%
)

:: Copy Portable Targets Over to ToolRuntime
if not exist "%PACKAGES_DIR%\generated" mkdir "%PACKAGES_DIR%\generated"
set PORTABLETARGETS_PROJECTJSON=%PACKAGES_DIR%\generated\project.json
echo %MSBUILD_CONTENT_JSON% > "%PORTABLETARGETS_PROJECTJSON%"
@echo on
call "%DOTNET_CMD%" restore "%PORTABLETARGETS_PROJECTJSON%" --source https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json --source https://api.nuget.org/v3/index.json --packages "%PACKAGES_DIR%\."
set RESTORE_PORTABLETARGETS_ERROR_LEVEL=%ERRORLEVEL%
@if "%_echo%" neq "on" echo off
if not [%RESTORE_PORTABLETARGETS_ERROR_LEVEL%]==[0] (
	echo ERROR: An error ocurred when running: '"%DOTNET_CMD%" restore "%PORTABLETARGETS_PROJECTJSON%"'. Please check above for more details.
	exit /b %RESTORE_PORTABLETARGETS_ERROR_LEVEL%
)
Robocopy "%PACKAGES_DIR%\Microsoft.Portable.Targets\%PORTABLETARGETS_VERSION%\contentFiles\any\any\." "%TOOLRUNTIME_DIR%\." /E
Robocopy "%PACKAGES_DIR%\MicroBuild.Core\%MICROBUILD_VERSION%\build\." "%TOOLRUNTIME_DIR%\." /E

:: Copy Roslyn Compilers Over to ToolRuntime
Robocopy "%PACKAGES_DIR%\Microsoft.Net.Compilers\%ROSLYNCOMPILERS_VERSION%\." "%TOOLRUNTIME_DIR%\net45\roslyn\." /E

exit /b 0
