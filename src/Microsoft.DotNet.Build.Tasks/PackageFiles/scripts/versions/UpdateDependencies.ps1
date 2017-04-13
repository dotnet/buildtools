#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# This script updates the dir.props file with the latest build version,
# optionally runs the project.json auto-upgrade target, and then optionally
# creates a GitHub Pull Request for the change.

param(
    [Parameter(Mandatory=$true)][string]$VersionFileUrl,
    [Parameter(Mandatory=$true)][string[]]$DirPropsVersionElements,
    [string]$RepositoryRoot = '.',

    [string]$GitHubUser,
    [string]$GitHubEmail,
    [string]$GitHubPassword,
    [string]$GitHubOriginOwner=$GitHubUser,
    [string]$GitHubUpstreamOwner='dotnet', 
    [string]$GitHubUpstreamBranch='master',
    [string]$GitHubProject,
    # a semi-colon delimited list of GitHub users to notify on the PR
    [string]$GitHubPullRequestNotifications='',

    # Run build.cmd to apply the version upgrade to project.json files.
    [switch]$UpdateInvalidPackageVersions,
    # Create a pull request based on the GitHub parameters.
    [switch]$SubmitPullRequest)

$LatestVersion = Invoke-WebRequest $VersionFileUrl -UseBasicParsing
$LatestVersion = $LatestVersion.ToString().Trim()

# Make a nicely formatted string of the dir props version elements. Short names, joined by commas.
$DirPropsVersionNames = ($DirPropsVersionElements | %{ $_ -replace 'ExpectedPrerelease', '' }) -join ', '

# Updates the dir.props file with the latest build number
function UpdateValidDependencyVersionsFile
{
    if (!$LatestVersion)
    {
        Write-Error "Unable to find latest dependency version at $VersionFileUrl ($DirPropsVersionNames)"
        return $false
    }

    $DirPropsContent = Get-Content $RepositoryRoot\dir.props | % {
        $line = $_
        $DirPropsVersionElements | % {
            $line = $line -replace `
                "<$_>.*</$_>", `
                "<$_>$LatestVersion</$_>"
        }
        $line
    }
    Set-Content $RepositoryRoot\dir.props $DirPropsContent

    return $true
}

# Updates all the project.json files with out of date version numbers
function RunUpdatePackageDependencyVersions
{
    cmd /c $RepositoryRoot\build.cmd managed /t:UpdateInvalidPackageVersions | Out-Host

    return $LASTEXITCODE -eq 0
}

# Creates a Pull Request for the updated version numbers
function CreatePullRequest
{
    $GitStatus = git status --porcelain
    if ([string]::IsNullOrWhiteSpace($GitStatus))
    {
        Write-Warning "Dependencies are currently up to date"
        return $true
    }

    $CommitMessage = "Updating $DirPropsVersionNames dependencies to $LatestVersion"

    $env:GIT_COMMITTER_NAME = $GitHubUser
    $env:GIT_COMMITTER_EMAIL = $GitHubEmail
    git commit -a -m "$CommitMessage" --author "$GitHubUser <$GitHubEmail>" | Out-Host

    $RemoteUrl = "github.com/$GitHubOriginOwner/$GitHubProject.git"
    $RemoteBranchName = "UpdateDependencies$([DateTime]::UtcNow.ToString('yyyyMMddhhmmss'))"
    $RefSpec = "HEAD:refs/heads/$RemoteBranchName"

    Write-Host "git push https://$RemoteUrl $RefSpec"
    # pipe this to null so the password secret isn't in the logs
    git push "https://$($GitHubUser):$GitHubPassword@$RemoteUrl" $RefSpec 2>&1 | Out-Null

    if ($GitHubPullRequestNotifications)
    {
        $PRNotifications = $GitHubPullRequestNotifications.Split(';', [StringSplitOptions]::RemoveEmptyEntries) -join ' @'
        $PRBody = "/cc @$PRNotifications"
    }
    else
    {
        $PRBody = ''
    }

    $CreatePRBody = @"
    {
        "title": "$CommitMessage",
        "body": "$PRBody",
        "head": "$($GitHubOriginOwner):$RemoteBranchName",
        "base": "$GitHubUpstreamBranch"
    }
"@

    $CreatePRHeaders = @{'Accept'='application/vnd.github.v3+json'; 'Authorization'="token $GitHubPassword"}

    try
    {
        Invoke-WebRequest https://api.github.com/repos/$GitHubUpstreamOwner/$GitHubProject/pulls -UseBasicParsing -Method Post -Body $CreatePRBody -Headers $CreatePRHeaders
    }
    catch
    {
        Write-Error $_.ToString()
        return $false
    }

    return $true
}

if (!(UpdateValidDependencyVersionsFile))
{
    Exit -1
}

if ($UpdateInvalidPackageVersions -and !(RunUpdatePackageDependencyVersions))
{
    Exit -1
}

if ($SubmitPullRequest -and !(CreatePullRequest))
{
    Exit -1
}

Write-Host -ForegroundColor Green "Successfully updated dependencies from the latest build numbers"

exit $LastExitCode
