/**
 *  Determine the name of the Azure container used by official builds to collect build leg artifacts
 *  Use instances:
 *    https://github.com/chcosta/corefx/blob/vsts-to-jenkins/buildpipeline/windows.groovy#L161
 *    https://github.com/chcosta/corefx/blob/vsts-to-jenkins/buildpipeline/osx.groovy#L100
 *    https://github.com/chcosta/corefx/blob/vsts-to-jenkins/buildpipeline/linux.groovy#L143
 *    https://github.com/chcosta/corefx/blob/vsts-to-jenkins/buildpipeline/publish.groovy#L60
 */
def call(String repo, String buildNumber) {
  if(repo == '') {
    throw new Exception("'repo' parameter not specified in call to getOfficialBuildAzureContainerName")
  }
  if(buildNumber == '') {
    throw new Exception("'buildNumber' parameter not specified in call to getOfficialBuildAzureContainerName")
  }
  return "jenkins-${repo}-${buildNumber}".toLowerCase()
}
