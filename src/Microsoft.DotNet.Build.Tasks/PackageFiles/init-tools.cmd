@echo off
setlocal

set PROJECT_DIR=%~1
set DOTNET_CMD=%~2
set TOOLRUNTIME_DIR=%~3
set PACKAGES_DIR=%4
if [%PACKAGES_DIR%] == [] set PACKAGES_DIR=%TOOLRUNTIME_DIR%
:: Remove quotes to the packages directory
set PACKAGES_DIR=%PACKAGES_DIR:"=%
IF [%BUILDTOOLS_TARGET_RUNTIME%]==[] set BUILDTOOLS_TARGET_RUNTIME=win7-x64
IF [%BUILDTOOLS_NET45_TARGET_RUNTIME%]==[] set BUILDTOOLS_NET45_TARGET_RUNTIME=win7-x86
set BUILDTOOLS_PACKAGE_DIR=%~dp0
set MICROBUILD_VERSION=0.2.0
set PORTABLETARGETS_VERSION=0.1.1-dev
set ROSLYNCOMPILERS_VERSION=2.0.0-beta3
set MSBUILD_CONTENT_JSON={"dependencies": { "MicroBuild.Core": "%MICROBUILD_VERSION%", "Microsoft.Portable.Targets": "%PORTABLETARGETS_VERSION%", "Microsoft.Net.Compilers": "%ROSLYNCOMPILERS_VERSION%"},"frameworks": {"netcoreapp1.0": {},"net46": {}}}
set INIT_TOOLS_RESTORE_ARGS=--source https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json --source https://api.nuget.org/v3/index.json %INIT_TOOLS_RESTORE_ARGS%
set TOOLRUNTIME_RESTORE_ARGS=--source https://dotnet.myget.org/F/dotnet-core/api/v3/index.json %INIT_TOOLS_RESTORE_ARGS%

if not exist "%PROJECT_DIR%" (
  echo ERROR: Cannot find project root path at [%PROJECT_DIR%]. Please pass in the source directory as the 1st parameter.
  exit /b 1
)

if not exist "%DOTNET_CMD%" (
  echo ERROR: Cannot find dotnet cli at [%DOTNET_CMD%]. Please pass in the path to dotnet.exe as the 2nd parameter.
  exit /b 1
)

ROBOCOPY "%BUILDTOOLS_PACKAGE_DIR%\." "%TOOLRUNTIME_DIR%" /E

set TOOLRUNTIME_PROJECTJSON=%BUILDTOOLS_PACKAGE_DIR%\tool-runtime\project.json
@echo on
call "%DOTNET_CMD%" restore "%TOOLRUNTIME_PROJECTJSON%" %TOOLRUNTIME_RESTORE_ARGS%
set RESTORE_ERROR_LEVEL=%ERRORLEVEL%
@echo off
if not [%RESTORE_ERROR_LEVEL%]==[0] (
	echo ERROR: An error occured when running: '"%DOTNET_CMD%" restore "%TOOLRUNTIME_PROJECTJSON%"'. Please check above for more details.
	exit /b %RESTORE_ERROR_LEVEL%
)
@echo on
call "%DOTNET_CMD%" publish "%TOOLRUNTIME_PROJECTJSON%" -f netcoreapp1.0 -r %BUILDTOOLS_TARGET_RUNTIME% -o "%TOOLRUNTIME_DIR%"
set TOOLRUNTIME_PUBLISH_ERROR_LEVEL=%ERRORLEVEL%
@echo off
if not [%TOOLRUNTIME_PUBLISH_ERROR_LEVEL%]==[0] (
	echo ERROR: An error ocurred when running: '"%DOTNET_CMD%" publish "%TOOLRUNTIME_PROJECTJSON%" -f netcoreapp1.0'. Please check above for more details.
	exit /b %TOOLRUNTIME_PUBLISH_ERROR_LEVEL%
)
@echo on
call "%DOTNET_CMD%" publish "%TOOLRUNTIME_PROJECTJSON%" -f net45 -r %BUILDTOOLS_NET45_TARGET_RUNTIME% -o "%TOOLRUNTIME_DIR%\net45"
set NET45_PUBLISH_ERROR_LEVEL=%ERRORLEVEL%
@echo off
if not [%NET45_PUBLISH_ERROR_LEVEL%]==[0] (
	echo ERROR: An error ocurred when running: '"%DOTNET_CMD%" publish "%TOOLRUNTIME_PROJECTJSON%" -f net45'. Please check above for more details.
	exit /b %NET45_PUBLISH_ERROR_LEVEL%
)

:: Copy Portable Targets Over to ToolRuntime
if not exist "%PACKAGES_DIR%\generated" mkdir "%PACKAGES_DIR%\generated"
set PORTABLETARGETS_PROJECTJSON=%PACKAGES_DIR%\generated\project.json
echo %MSBUILD_CONTENT_JSON% > "%PORTABLETARGETS_PROJECTJSON%"
@echo on
call "%DOTNET_CMD%" restore "%PORTABLETARGETS_PROJECTJSON%" %INIT_TOOLS_RESTORE_ARGS% --packages "%PACKAGES_DIR%\."
set RESTORE_PORTABLETARGETS_ERROR_LEVEL=%ERRORLEVEL%
@echo off
if not [%RESTORE_PORTABLETARGETS_ERROR_LEVEL%]==[0] (
	echo ERROR: An error ocurred when running: '"%DOTNET_CMD%" restore "%PORTABLETARGETS_PROJECTJSON%"'. Please check above for more details.
	exit /b %RESTORE_PORTABLETARGETS_ERROR_LEVEL%
)
Robocopy "%PACKAGES_DIR%\Microsoft.Portable.Targets\%PORTABLETARGETS_VERSION%\contentFiles\any\any\." "%TOOLRUNTIME_DIR%\." /E
Robocopy "%PACKAGES_DIR%\MicroBuild.Core\%MICROBUILD_VERSION%\build\." "%TOOLRUNTIME_DIR%\." /E

:: Copy Roslyn Compilers Over to ToolRuntime
Robocopy "%PACKAGES_DIR%\Microsoft.Net.Compilers\%ROSLYNCOMPILERS_VERSION%\." "%TOOLRUNTIME_DIR%\net45\roslyn\." /E

exit /b 0
