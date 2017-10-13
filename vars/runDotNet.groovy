/**
 * Acquires dotnet if not present already, and then
 * runs dotnet with standard environment settings
 *
 *  Use instances:
 *    https://github.com/chcosta/corefx/blob/vsts-to-jenkins/buildpipeline/windows.groovy#L169
 *    throught https://github.com/chcosta/corefx/blob/vsts-to-jenkins/buildpipeline/publish.groovy 
 */
def _runDotNet(String command) {
    withEnv( ['DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1'] ) {
        if(isUnix()) {
            sh "${command}" 
        }
        else {
            bat "${command}" 
        }
    }
}

def call(String args, String workingDirectory = null)
{
    if(isUnix()) {
        throw new Exception("Not implemented")
    }
    def dotNetPath = getDotNet()
    if(isUnix()) {
        //_runDotNet("cd ${workingDirectory} ; ${dotNetPath} ${args}")
    }
    else {
        if(workingDirectory != null) {
            _runDotNet("cd ${workingDirectory} &${dotNetPath} ${args}")
        }
        else {
            _runDotNet("${dotNetPath} ${args}")
        }
    }
}