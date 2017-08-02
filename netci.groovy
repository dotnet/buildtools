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


// Generate a fake job to test Snapshot functionality
def snapshotJob = job(Utilities.getFullJobName(project, 'Windows_NT_Snapshot', true)) {
    steps {
        def zipWorkspace = "\"C:\\Program Files\\7-Zip\\7z.exe\" a -t7z %COMPUTERNAME%-Workspace.7z -mx9"
        def workspaceDestination = "https://dotnetci1vmstorage2.blob.core.windows.net/workspace"
        def uploadToAzure = "\"C:\\Program Files (x86)\\Microsoft SDKs\\Azure\\AzCopy\\AzCopy.exe\" /Source:. /Pattern:%COMPUTERNAME%-Workspace.7z /Dest:${workspaceDestination} /DestKey:%SNAPSHOT_STORAGE_KEY% /Y"
        def createSnapshot = """Invoke-RestMethod https://snapshotter.azurewebsites.net/api/7c8473db-c6cf-4be2-a0e7-680429fc0c99/snapshot?code=\$env:SNAPSHOT_TOKEN -Method Post -Body "{ 'group': 'dotnet-ci1-vms', 'name': '\$env:computername', 'payload': \'${workspaceDestination}/\$env:computername-Workspace.7z\', 'targetContainer': 'snapshots' }" -ContentType 'application/json' -ErrorAction Continue"""

        batchFile("echo Renaming launch.cmd")
        powerShell("Rename-Item C:\\Jenkins\\launch.cmd C:\\Jenkins\\launch.cmd.disabled")
        
        batchFile("${zipWorkspace}")
        batchFile("${uploadToAzure}")

        powerShell("echo ${createSnapshot}")
        powerShell("${createSnapshot}")
        
        batchFile("echo Renaming launch.cmd.disabled")
        powerShell("Rename-Item C:\\Jenkins\\launch.cmd.disabled C:\\Jenkins\\launch.cmd")
    }
    // Ensure credentials are bound
    wrappers {
        credentialsBinding {
            string('SNAPSHOT_TOKEN', 'SnapshotToken')
            string('SNAPSHOT_STORAGE_KEY', 'snapshotStorageKey')
        }
    }
}

Utilities.setMachineAffinity(snapshotJob, 'Windows_NT', 'latest-or-auto')
Utilities.standardJobSetup(snapshotJob, project, true, "*/${branch}")
Utilities.addGithubPushTrigger(snapshotJob)

// Generate a fake Repro Job
def reproJob = job(Utilities.getFullJobName(project, 'Windows_NT_Repro', true)) {
    steps {
        batchFile('''call "C:\\Program Files (x86)\\Microsoft Visual Studio 14.0\\Common7\\Tools\\VsDevCmd.bat" && build.cmd''')
    }
}
Utilities.setMachineAffinity(reproJob, 'Windows_NT', 'latest-or-auto')
Utilities.standardJobSetup(reproJob, project, true, "*/${branch}")
Utilities.addGithubPushTrigger(reproJob)

//Process the msbuild log
def archiveSettings = new ArchivalSettings()
archiveSettings.addFiles("msbuild.log")
archiveSettings.setFailIfNothingArchived()
archiveSettings.setArchiveOnFailure()
Utilities.addReproBuild (reproJob, archiveSettings)
