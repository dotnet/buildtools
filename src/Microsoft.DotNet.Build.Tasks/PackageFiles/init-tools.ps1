 param (
    [Parameter(Mandatory=$true)][string]$ToolRuntimePath,
    [Parameter(Mandatory=$true)][string]$DotnetCmd
 )

# Override versions in runtimeconfig.json files with highest available runtime version.
$mncaFolder = (Get-Item $DotnetCmd).Directory.FullName + "\shared\Microsoft.NETCore.App"
$highestVersion = Get-ChildItem $mncaFolder -Name | Sort-Object BaseName | Select-Object -First 1

foreach ($file in Get-ChildItem $ToolRuntimePath *.runtimeconfig.json)
{
    Write-Host "Correcting runtime version of" $file.FullName
    $text = (Get-Content $file.FullName) -replace "1.1.0","$highestVersion"
    Set-Content $file.FullName $text
}