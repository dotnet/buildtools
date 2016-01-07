@echo off
setlocal

set PROJECT_DIR=%~1
set DOTNET_CMD=%~2
set TOOLRUNTIME_DIR=%~3
set BUILDTOOLS_PACKAGE_DIR=%~dp0

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
call "%DOTNET_CMD%" restore --source https://www.myget.org/F/dotnet-core/ --source https://www.myget.org/F/dotnet-buildtools/ --source https://www.nuget.org/api/v2/
call "%DOTNET_CMD%" publish -f dnxcore50 -r win7-x64 -o "%TOOLRUNTIME_DIR%"

exit /b %ERRORLEVEL%