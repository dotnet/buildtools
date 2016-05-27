@if "%_echo%" neq "on" echo off

:: Restore the Tools directory
echo Running init-tools.cmd
call %~dp0init-tools.cmd

set _toolRuntime=%~dp0Tools
set _dotnet=%_toolRuntime%\dotnetcli\dotnet.exe

if [%1]==[] (
  echo Running: %_dotnet% %_toolRuntime%\executor.exe -default
  call %_dotnet% %_toolRuntime%\executor.exe -default
  exit /b 0
)

echo Running: %_dotnet% %_toolRuntime%\executor.exe %*
call %_dotnet% %_toolRuntime%\executor.exe %*
exit /b 0
