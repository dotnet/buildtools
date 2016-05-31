@if "%_echo%" neq "on" echo off
setlocal

set _args=%*

echo Running executor.cmd -build %_args%
call executor.cmd -build %_args%
if NOT [%ERRORLEVEL%]==[0] (
  echo ERROR: An error occurred. Please look at the log file msbuild.log for more information.
  exit /b 1
)

echo Done Building.
exit /b 0