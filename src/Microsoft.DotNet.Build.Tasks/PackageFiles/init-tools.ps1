 param (
    [Parameter(Mandatory=$true)][string]$ToolRuntimePath,
    [Parameter(Mandatory=$true)][string]$DotnetCmd,
    [Parameter(Mandatory=$true)][string]$BuildToolsPackageDir
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

# Make a directory in the root of the tools folder that matches the buildtools version, this is done so
# the init-tools.cmd (that is checked into each repository that uses buildtools) can write the semaphore
# marker into this file once tool initialization is complete.
New-Item -Force -Type Directory (Join-Path $ToolRuntimePath (Split-Path -Leaf (Split-Path $BuildToolsPackageDir)))

# Download the package version props file, if  was passed in the environment.
$packageVersionPropsUrl = $env:PACKAGEVERSIONPROPSURL
$packageVersionPropsPath = Join-Path $ToolRuntimePath "DownloadedPackageVersions.props"

if ($packageVersionPropsUrl)
{
    Write-Host "Downloading package version props from '$packageVersionPropsUrl' to '$packageVersionPropsPath'..."

    # Copied from init-tools.cmd in CoreFX
    $retryCount = 0
    $success = $false
    $proxyCredentialsRequired = $false
    do
    {
        try
        {
            $wc = New-Object Net.WebClient
            if ($proxyCredentialsRequired)
            {
                [Net.WebRequest]::DefaultWebProxy.Credentials = [Net.CredentialCache]::DefaultNetworkCredentials
            }
            $wc.DownloadFile($packageVersionPropsUrl, $packageVersionPropsPath)
            $success = $true
        }
        catch
        {
            if ($retryCount -ge 6)
            {
                throw
            }
            else
            {
                $we = $_.Exception.InnerException -as [Net.WebException]
                $proxyCredentialsRequired = ($we -ne $null -and ([Net.HttpWebResponse]$we.Response).StatusCode -eq [Net.HttpStatusCode]::ProxyAuthenticationRequired)
                Start-Sleep -Seconds (5 * $retryCount)
                $retryCount++
            } 
        }
    } while ($success -eq $false);

    Write-Host "Downloaded package version props:"
    Get-Content $packageVersionPropsPath
}
