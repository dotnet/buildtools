@if "%_echo%" neq "on" echo off
setlocal
echo msbuild.exe %*
call msbuild.exe %*
IF ERRORLEVEL 1 (
  exit /b 1
)
exit /b 0