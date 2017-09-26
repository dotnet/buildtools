/**
 *  Determine the name of the Azure container used by official builds to collect build leg artifacts
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
