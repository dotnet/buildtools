/* Create an empty NuGet.Config file
 */
def call(String nuGetConfigFolder = "${WORKSPACE}\\CiTemp") {
  def nuGetConfigPath = "${nuGetConfigFolder}\\NuGet.Config"

  def nuGetfileContents = '''\
    <?xml version="1.0" encoding="utf-8"?>
    <configuration />'''.stripIndent()
  
  dir(nuGetConfigFolder) {
    writeFile encoding: 'utf-8', file: "NuGet.Config", text: nuGetfileContents
  }
  assert fileExists(nuGetConfigPath)
  return nuGetConfigPath
}
