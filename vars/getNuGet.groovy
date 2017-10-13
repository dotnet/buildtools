/**
 *  Download the NuGet.CommandLine package, extract the contents, and return the path to NuGet.exe
 *  Retuns: String - the path to NuGet.exe
 */
def call(String downloadFolder ="${WORKSPACE}\\ciTools") {
  if(isUnix()) {
    throw new Exception("Not implemented")
  }
  def nuGetSource = 'https://dotnet.myget.org/F/nuget-build/api/v2/package/NuGet.CommandLine/4.1.0-rtm-2450'
  def nuGetPath = "${downloadFolder}\\tools\\NuGet.exe"
  if(!fileExists(nuGetPath)) {
    def downloadLocalFilename = "NuGet.CommandLine.zip"
    fetchAndExtract(new URL(nuGetSource), new File(downloadFolder), downloadLocalFilename)
  }
  assert fileExists(nuGetPath)
  return nuGetPath.toString()
}
