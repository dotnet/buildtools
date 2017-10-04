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
