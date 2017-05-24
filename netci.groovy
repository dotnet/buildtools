// Import the utility functionality.

import jobs.generation.ArchivalSettings
import jobs.generation.Utilities;

def project = GithubProject
def branch = GithubBranchName

// Generate the builds for debug and release

["Windows_NT", "Ubuntu14.04"].each { os ->
    [true, false].each { isPR ->
        def newJob = job(Utilities.getFullJobName(project, os, isPR)) {
            steps {
                if (os == 'Windows_NT') {
                    batchFile('''call "C:\\Program Files (x86)\\Microsoft Visual Studio 14.0\\Common7\\Tools\\VsDevCmd.bat" && build.cmd''')
                }
                else {
                    shell('''./build.sh''')
                }
            }
        }

        Utilities.setMachineAffinity(newJob, os, 'latest-or-auto')
        Utilities.standardJobSetup(newJob, project, isPR, "*/${branch}")
        if (isPR) {
            Utilities.addGithubPRTriggerForBranch(newJob, branch, "Innerloop ${os} Debug")
        }
        else {
            Utilities.addGithubPushTrigger(newJob)
        }
    }
}


// Generate a fake job to test ReproBuild functionality
def reproJob = job(Utilities.getFullJobName(project, 'Windows_NT_ReproBuild', true)) {
    steps {
        //def curlCommand = """Invoke-RestMethod https://snapshotter.azurewebsites.net/api/snapshot/vm?code=\$env:SNAPSHOT_TOKEN -Method Post -Body "{ 'group': 'dotnet-ci1-vms', 'name': '\$env:computername', 'targetGroup': 'snapshot-test' }" -ContentType 'application/json' -ErrorAction Continue"""
        def zipWorkspace = '''"C:\\Program Files\\7-Zip\\7z.exe" a -t7z ${GitBranchOrCommit}.7z -mx9 '''
        def uploadToAzure = '''"C:\\Program Files (x86)\\Microsoft SDKs\\Azure\\AzCopy\\AzCopy.exe" /Source:. /Pattern:${GitBranchOrCommit}.7z /Dest:https://cisnapshot.blob.core.windows.net/workspace /DestKey:%SNAPSHOT_STORAGE_KEY% /Y'''
                
        batchFile("echo Renaming launch.cmd")
        powerShell("Rename-Item C:\\Jenkins\\launch.cmd C:\\Jenkins\\launch.cmd.disabled")
        
        batchFile("dir")
        batchFile("echo zip the workspace with ${zipWorkspace}")
        batchFile("${zipWorkspace}")
        
        batchFile("dir")
        batchFile("echo upload to Azure with ${uploadToAzure}")
        batchFile("${uploadToAzure}")
        
        batchFile("echo Renaming launch.cmd.disabled")
        powerShell("Rename-Item C:\\Jenkins\\launch.cmd.disabled C:\\Jenkins\\launch.cmd")
    }
    // Ensure credentials are bound
    wrappers {
        credentialsBinding {
            //string('SNAPSHOT_TOKEN', 'SnapshotToken')
            string('SNAPSHOT_STORAGE_KEY', 'snapshotStorageKey')
        }
    }
}

Utilities.setMachineAffinity(reproJob, 'Windows_NT', 'latest-or-auto')
Utilities.standardJobSetup(reproJob, project, true, "*/${branch}")
Utilities.addGithubPushTrigger(reproJob)

//Process the msbuild log
/*def archiveSettings = new ArchivalSettings()
archiveSettings.addFiles("msbuild.log")
archiveSettings.setFailIfNothingArchived()
archiveSettings.setArchiveOnFailure()
Utilities.addReproBuild (reproJob, archiveSettings)
*/
Utilities.addCROSSCheck(this, project, branch)
