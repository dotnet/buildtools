/**
 *  Download dotnet, extract the contents
 *  Example: getDotNet dotNetUrl: "https://go.microsoft.com/fwlink/?LinkID=809115", dotNetFilename: "dotnet-win-x64"
 */
def call(String downloadFolder = "${WORKSPACE}\\ciTools") {
  def dotNetUrl = "https://go.microsoft.com/fwlink/?LinkID=843454"
  def dotNetPath
  def dotNetFilename
  if(isUnix()) {
      throw new Exception("Not implemented")
  }
  else {
      dotNetPath = "${downloadFolder}\\dotnet.exe"
      dotNetFilename = "dotnet-sdk-win.zip"
  }
  if(!fileExists(dotNetPath)) {
    fetchAndExtract(new URL(dotNetUrl), new File(downloadFolder), dotNetFilename)
  }
  assert fileExists(dotNetPath)
  return dotNetPath
}
