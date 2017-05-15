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

Utilities.addCROSSCheck(this, project, branch)
