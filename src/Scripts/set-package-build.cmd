@echo off
if "%1" == ""   goto :usage
if "%2" == ""   goto :usage
goto :main

:usage
echo Usage: %~n0 [directory] [build]
echo.
echo Recursively replaces all occurrences of 4.0.XX-beta-YYYYY in
echo [directory]\*.config;*.csproj;*.nuspec with 4.0.XX-beta-[build]
goto :eof

:main
pushd %1 && git clean -f packages && call :replace %2 && call :cleanup && popd
goto :eof

:replace
for /r %%f in (*.config *.csproj *.nuspec) do (
   perl -i.tmp_spb -p -e "s/(4\.0\.[0-9]+-beta)-([0-9]+)/$1-%1/" %%f || goto :eof
)
goto :eof


:cleanup
del /s *.tmp_spb
goto :eof