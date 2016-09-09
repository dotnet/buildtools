param
(
    [string]$RepositoryRoot,
    [string]$ToolsLocalPath,
    [switch]$Force = $false
)

$rootToolVersions = Join-Path $RepositoryRoot ".toolversions"
$bootstrapComplete = Join-Path $ToolsLocalPath "bootstrap.complete"

# if the force switch is specified delete the semaphore file if it exists
if ($Force -and (Test-Path $bootstrapComplete))
{
    del $bootstrapComplete
}

# if the semaphore file exists and is identical to the specified version then exit
if ((Test-Path $bootstrapComplete) -and !(Compare-Object (Get-Content $rootToolVersions) (Get-Content $bootstrapComplete)))
{
    exit 0
}

$initCliScript = "dotnet-install.ps1"
$initCliLocalPath = Join-Path $ToolsLocalPath $initCliScript

# blow away the tools directory so we can start from a known state
if (Test-Path $ToolsLocalPath)
{
    rd -recurse -force $ToolsLocalPath
}
mkdir $ToolsLocalPath | Out-Null

# download CLI boot-strapper script
Invoke-WebRequest "https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/dotnet-install.ps1" -OutFile $initCliLocalPath

# load the version of the CLI
$rootCliVersion = Join-Path $RepositoryRoot ".cliversion"
$dotNetCliVersion = Get-Content $rootCliVersion

# now execute the script
$cliLocalPath = Join-Path $ToolsLocalPath "dotnetcli"
Invoke-Expression "$initCliLocalPath -Version $dotNetCliVersion -InstallDir $cliLocalPath"
if ($LastExitCode -ne 0)
{
    Write-Output "The .NET CLI installation failed with exit code $LastExitCode"
    exit $LastExitCode
}

# create a junction to the shared FX version directory. this is
# so we have a stable path to dotnet.exe regardless of version.
$junctionTarget = Join-Path $toolsLocalPath "dotnetcli\shared\Microsoft.NETCore.App\1.0.0"
$junctionName = Join-Path $toolsLocalPath "dotnetcli\shared\Microsoft.NETCore.App\version"
cmd.exe /c mklink /j $junctionName $junctionTarget | Out-Null

# create a project.json for the packages to restore
$projectJson = Join-Path $ToolsLocalPath "project.json"
$pjContent = "{ `"dependencies`": {"

$tools = Get-Content $rootToolVersions
foreach ($tool in $tools)
{
    $name, $version = $tool.split("=")
    $pjContent = $pjContent + "`"$name`": `"$version`","
}
$pjContent = $pjContent + "}, `"frameworks`": { `"netcoreapp1.0`": { } } }"
$pjContent | Out-File $projectJson

# now restore the packages
$buildToolsSource = "https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json"
$nugetOrgSource = "https://api.nuget.org/v3/index.json"
if ($env:buildtools_source -ne $null)
{
    $buildToolsSource = $env:buildtools_source
}
$packagesPath = Join-Path $RepositoryRoot "packages"
$dotNetExe = Join-Path $cliLocalPath "dotnet.exe"
$restoreArgs = "restore $projectJson --packages $packagesPath --source $buildToolsSource --source $nugetOrgSource"
$process = Start-Process -Wait -NoNewWindow -FilePath $dotNetExe -ArgumentList $restoreArgs -PassThru
if ($process.ExitCode -ne 0)
{
    exit $process.ExitCode
}

# now stage the contents to tools directory and run any init scripts
foreach ($tool in $tools)
{
    $name, $version = $tool.split("=")
    $destination = Join-Path $ToolsLocalPath $name
    if (Test-Path (Join-Path $packagesPath "$name\$version\tools\netcoreapp1.0"))
    {
        mkdir $destination | Out-Null
        copy (Join-Path $packagesPath "$name\$version\tools\netcoreapp1.0\*") $destination
    }
    elseif (Test-Path (Join-Path $packagesPath "$name\$version\lib"))
    {
        copy (Join-Path $packagesPath "$name\$version\lib\*") $ToolsLocalPath
    }

    if (Test-Path (Join-Path $packagesPath "$name\$version\lib\init-tools.cmd"))
    {
        cmd.exe /c (Join-Path $packagesPath "$name\$version\lib\init-tools.cmd") $RepositoryRoot $dotNetExe $ToolsLocalPath | Out-File (Join-Path $RepositoryRoot "Init-$name.log")
    }
}

# write semaphore file
copy $rootToolVersions $bootstrapComplete
