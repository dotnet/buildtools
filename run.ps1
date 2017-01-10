# set the base tools directory
$toolsLocalPath = Join-Path $PSScriptRoot "Tools"
$bootStrapperPath = Join-Path $toolsLocalPath "bootstrap.ps1"

# if the boot-strapper script doesn't exist then download it
if ((Test-Path $bootStrapperPath) -eq 0)
{
    if ((Test-Path $toolsLocalPath) -eq 0)
    {
        mkdir $toolsLocalPath | Out-Null
    }

    # download boot-strapper script
    Invoke-WebRequest "https://raw.githubusercontent.com/dotnet/buildtools/master/bootstrap/bootstrap.ps1" -OutFile $bootStrapperPath
}

# now execute it
& $bootStrapperPath (Get-Location) $toolsLocalPath -DotNetInstallBranch "rel/1.0.0-preview2.1" | Out-File (Join-Path (Get-Location) "bootstrap.log")
if ($LastExitCode -ne 0)
{
    Write-Output "Boot-strapping failed with exit code $LastExitCode, see bootstrap.log for more information."
    exit $LastExitCode
}

# execute the tool using the dotnet.exe host
$dotNetExe = Join-Path $toolsLocalPath "dotnetcli\shared\Microsoft.NETCore.App\version\dotnet.exe"
$runExe = Join-Path $toolsLocalPath "Microsoft.DotNet.BuildTools.Run\netcoreapp1.0\run.exe"
& $dotNetExe $runExe $args
exit $LastExitCode
