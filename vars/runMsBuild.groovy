/**
 * run desktop MSBuild command
 */
def call(String msBuildArgs) {
  call("C:\\Program Files (x86)\\MSBuild\\14.0\\Bin\\msbuild.exe", msBuildArgs)
}

def call(String msBuildPath, String msBuildArgs) {
  if(isUnix()) {
      throw new Exception("not implemented")
  }
  assert fileExists(msBuildPath)
  bat "\"${msBuildPath}\" ${msBuildArgs}"
}
