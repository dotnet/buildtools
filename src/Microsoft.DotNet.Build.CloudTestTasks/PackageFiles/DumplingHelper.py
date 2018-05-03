import os
import platform
import urllib
import urllib2
import glob
import time
import sys
import subprocess
import string
import traceback

def get_timestamp():
  print(time.time())

def install_dumpling():
  try:
    if (not os.path.isfile(dumplingPath)):
      url = "https://dumpling.int-dot.net/api/client/dumpling.py"
      scriptPath = os.path.dirname(os.path.realpath(__file__))
      downloadLocation = scriptPath + "/dumpling.py"
      response = urllib2.urlopen(url)
      if response.getcode() == 200:
        with open(downloadLocation, 'w') as f:
          f.write(response.read())
        subprocess.call([sys.executable, downloadLocation, "install", "--full"])
      else:
        raise urllib2.URLError("HTTP Status Code" + str(response.getcode()))

    dbgPath = "~/.dumpling/dbg/bin/lldb"    
    subprocess.call([sys.executable, dumplingPath, "install"])
    subprocess.call([sys.executable, dumplingPath, "config", "--dbgpath", dbgPath, "save"])
  except urllib2.HTTPError, e:
    print("Dumpling cannot be installed from " + url + " due to: " + str(e).replace(':', '')) # Remove : to avoid looking like error format
  except  urllib2.URLError, e:
    print("Dumpling cannot be installed from " + url + " due to: " + str(e.reason))
  except:
    print("An unexpected error was encountered while installing dumpling.py: " + traceback.format_exc())

def ensure_installed():
  if (not os.path.isfile(dumplingPath)):
    print("Dumpling has not been installed yet. Please run \"DumplingHelper.py install_dumpling\" before collect_dumps.")
    return False
  else:
    return True

def find_latest_dump(folder, startTimeStr):
  startTime = float(startTimeStr)
  globPattern = "/*"

  # Outside of Windows, core files are generally dumped into the executable's directory,
  # so it may have many other files in it. Filter those out.
  if sys.platform != "win32":
    globPattern = "/*core*"

  allFiles = glob.glob(folder + globPattern);
  if allFiles:
    latestFile = max(allFiles, key=os.path.getmtime)
    latestTime = os.path.getmtime(latestFile)
    if (latestTime > startTime):
      return latestFile
  return None

def collect_dump(exitcodeStr, folder, startTimeStr, projectName, incpaths):
  exitcode = int(exitcodeStr)

  if (exitcode == 0):
    sys.exit(exitcode)

  if not ensure_installed():
    sys.exit(exitcode)

  if (not incpaths is None):
    # Normalize incpaths so it can be passed to dumpling.py.
    incpaths = incpaths.split(",")
    incpaths = string.join(incpaths, " ")

  # Find candidate crash dumps in the given folder.
  print("Trying to find crash dumps for project: " + projectName)
  file = find_latest_dump(folder, startTimeStr)
  if (file is None):
    print("No new dump file was found in " + folder)
  else:
    # File was found; upload it.
    print("Uploading dump file: " + file)
    procArgs = string.join([
      sys.executable, dumplingPath, "upload",
      "--dumppath", file,
      "--noprompt",
      "--triage", "full",
      "--displayname", projectName,
      "--properties", "STRESS_TESTID="+projectName
      ], " ")
    if (not incpaths is None):
      procArgs = procArgs + " --incpaths " + incpaths

    subprocess.call(procArgs, shell=True)

  sys.exit(exitcode)

def print_usage():
  print("DumplingHelper.py <command>")
  print("Commands:")
  print("  install_dumpling:")
  print("      - Installs dumpling globally on the machine.")
  print("  get_timestamp:")
  print("      - Prints out the current timestamp of the machine.")
  print("  collect_dump <exitcode> <folder> <starttime> <projectname> <incpaths>:")
  print("      - Collects and uploads the latest dump (after start time) from the folder to the dumpling service.")

# Main
def main(argv):
  if (len(argv) <= 1):
    print_usage()
    sys.exit(1)
  if (argv[1] == "install_dumpling"):
    install_dumpling()
  elif (argv[1] == "get_timestamp"):
    get_timestamp()
  elif (argv[1] == "collect_dump"):
    if (len(argv) == 6):
      collect_dump(argv[2], argv[3], argv[4], argv[5], None)
    elif (len(argv) == 7):
      collect_dump(argv[2], argv[3], argv[4], argv[5], argv[6])
    else:
      print("Invalid number of arguments passed to collect_dump.")
      sys.exit(1)
  else:
    print(argv[1] + " is not a valid command.")
    print_usage()
    sys.exit(1)

dumplingPath = os.path.expanduser("~/.dumpling/dumpling.py")
if __name__ == '__main__':
  main(sys.argv)
