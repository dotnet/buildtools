/* Executes the closure only if in the context of an official build.
 * Example:
 *    officialBuildStep {
 *       echo "this is an official build"
 *    }
 * If 'runOfficialBuildStep' is false, then execute the closure only if NOT
 * in the context of an official build
 * Example:
 *    officialBuildStep(false) {
 *        echo "this is NOT an official build"
 *    }
 *  Use instances:
 *    https://github.com/chcosta/corefx/blob/vsts-to-jenkins/buildpipeline/linux.groovy
 *    https://github.com/chcosta/corefx/blob/vsts-to-jenkins/buildpipeline/osx.groovy
 *    https://github.com/chcosta/corefx/blob/vsts-to-jenkins/buildpipeline/publish.groovy
 *    https://github.com/chcosta/corefx/blob/vsts-to-jenkins/buildpipeline/windows.groovy
 */
def call(Boolean runOfficialBuildStep = true, Closure body) {
    if(isOfficialBuild()) {
        if(runOfficialBuildStep) {
            body()
        }
    }
    else {
        if(!runOfficialBuildStep) {
            body()
        }
    }
}
