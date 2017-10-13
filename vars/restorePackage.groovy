/**
 *  Restore a package with NuGet
 */
def call(String nuGetPath, String packageName, String version, String outputDirectory, String source) {
    if(isUnix()) {
        throw new Exception("Not implemented")
    }
    if(!(fileExists(nuGetPath))) {
      throw new Exception("'${nuGetPath}' not found.  Try running the 'getNuGet' shared library funtion first.")
    }
    bat "${nuGetPath} install ${packageName} -OutputDirectory ${outputDirectory} -Version ${version} -Source ${source}"
}
