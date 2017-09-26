/**
 *  Download a file, and extract the contents
 *  If overwrite is true, and the file is present locally, delete it, then acquire a new version
 *  If overwrite is false, and the file is present locally, just use the local version
 *  If the file is not present locally, acquire it
 */
def call(URL url, File downloadFolder, String downloadFilename, String user = '', String pwd = '', Boolean overwrite = false) {
  def dFile = new File("${downloadFolder}\\${downloadFilename}")
  def filePresent = fileExists(dFile.toString())
  if( filePresent && overwrite ) {
      println("${dFile} already exists locally, but overwrite is true, deleting local copy.")
      dFile.delete()
      filePresent = false
  }

  if(!filePresent) {
    if(!(fileExists(downloadFolder.toString()))) {
        fileOperations([folderCreateOperation(folderPath: downloadFolder.toString())])
    }
    println("Downloading ${url}...")
    fileOperations([fileDownloadOperation(password: pwd, targetFileName: downloadFilename, targetLocation: downloadFolder.toString(), url: url.toString(), userName: user)])  
  }
  println("Extracting file ${downloadFolder}\\${downloadFilename} to ${downloadFolder}")
  unzip zipFile: "${downloadFolder}\\${downloadFilename}", dir: downloadFolder.toString()
}
