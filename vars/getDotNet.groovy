/**
 *  Download dotnet and extract the contents to the specified folder, return the path to dotnet
 */
def call(String downloadFolder = "${WORKSPACE}\\ciTools") {
  def dotNetUrl = "https://download.microsoft.com/download/1/B/4/1B4DE605-8378-47A5-B01B-2C79D6C55519/dotnet-sdk-2.0.0-win-x64.zip" 
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
