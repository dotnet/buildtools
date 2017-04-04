 param (
    [Parameter(Mandatory=$true)][string]$ToolRuntimePath,
    [Parameter(Mandatory=$true)][string]$DotnetCmd
 )

# Override versions in runtimeconfig.json files with highest available runtime version.
$mncaFolder = (gi $DotnetCmd).Directory.FullName + "\shared\Microsoft.NETCore.App"
$highestVersion = gci $mncaFolder -name | sort BaseName | select -first 1

foreach ($file in gci $ToolRuntimePath *.runtimeconfig.json)
{
    Write-Host "Correcting runtime version of" $file.FullName
    $text = (gc $file.FullName) -replace "1.1.0","$highestVersion"
    sc $file.FullName $text
}