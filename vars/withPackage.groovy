/**
 *  Restore a package with NuGet
 *  Parameters:
 *    packageName - package id to restore
 *    packageVersion - package version to restore
 *    source - public feed to retrieve package from
 *    outputDirectory - directory to install package to
 *    body - Closure of code to execute
 */
def call(String packageName, String packageVersion, String source, String outputDirectory, Closure body) {
    def nuGetPath = getNuGet()
    assert fileExists(nuGetPath)
    _withNuGetCommand(nuGetPath, "install ${packageName} -OutputDirectory ${outputDirectory} -Version ${packageVersion} -Source ${source}", body)
}

/**
 *  Restores a package with NuGet (including packages from a secure feed)
 *  Parameters:
 *    packageName - package id to restore
 *    packageVersion - package version to restore
 *    sources - List of Maps enumerating one or more feed sources
 *      Map keys:
 *        name - a name for the feed source
 *        source - the feed source URI
 *        username - if a secure feed, provide a username 
 *        credentialsId - the name of the jenkins credential id which contains the api key for connecting to the package feed
 *    outputDirectory - directory to install package to
 *    nuGetConfigFolder - directory to generate the NuGet.Config file in which is used for the restore, it is recommended that a unique directory be used
 *    body - Closure of code to execute
 */
def call(String packageName, String packageVersion, List<Map> sources, String outputDirectory, String nuGetConfigFolder = "${WORKSPACE}\\CiTemp", Closure body) {
  def nuGetPath = getNuGet()
  assert fileExists(nuGetPath)
  generateEmptyNuGetConfig(nuGetConfigFolder)
  sources.each { source ->
    assert source['name'] != null
    assert source['source'] != null

    def args = "sources Add -Name ${source['name']} -Source ${source['source']} -ConfigFile ${nuGetConfigFolder}\\NuGet.Config"
    if(source['username'] != null) {
      args += " -UserName ${source['username']}"
    }
    if(source['credentialsId'] != null) {
      withCredentials([string(credentialsId: source['credentialsId'], variable: 'apiKey')]) {
        args += " -Password %apiKey%"
        // Call _withNuGetCommand within 'withCredentials'  so that secrets are kept hidden
        _withNuGetCommand(nuGetPath, args)
      }
    }
    else {
      _withNuGetCommand(nuGetPath, args)
    }
  }
  _withNuGetCommand(nuGetPath, "install ${packageName} -OutputDirectory ${outputDirectory} -Version ${packageVersion} -ConfigFile ${nuGetConfigFolder}\\NuGet.Config", body)
}

def _withNuGetCommand(String nuGetPath, String args, Closure body = null) {
  bat "${nuGetPath} ${args}"
  if(body != null) {
    body()
  }
}