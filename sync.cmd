@if "%_echo%" neq "on" echo off
call executor.cmd -sync -p %*
exit /b %ERRORLEVEL%