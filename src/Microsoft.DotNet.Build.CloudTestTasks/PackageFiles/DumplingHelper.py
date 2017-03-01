import os
import platform
import urllib
import glob
import time
import sys
import subprocess

def get_timestamp():
  print(time.time())

def install_dumpling():
  if (not os.path.isfile(dumplingPath)):
    url = "https://dumpling.azurewebsites.net/api/client/dumpling.py"
    urllib.urlretrieve(url, "dumpling.py")
    execfile("dumpling.py install --update")
  subprocess.call([sys.executable, dumplingPath, "install", "--full"])

def find_latest_dump(folder, startTimeStr):
  startTime = float(startTimeStr)
  allFiles = glob.glob(folder + "/*");
  latestFile = max(allFiles, key=os.path.getctime)
  latestTime = os.path.getctime(latestFile)
  if (latestTime > startTime):
    return latestFile
  else:
    return None

def collect_dump(exitcode, folder, startTimeStr, projectName):
  if (exitcode == "0"):
    return
  print("Trying to find crash dumps for project: " + projectName)
  file = find_latest_dump(folder, startTimeStr)
  if (file is None):
    print("No new dump file was found in " + folder)
  else:
    print("Uploading dump file: " + file)
    subprocess.call(
      [sys.executable, dumplingPath, "upload",
      "--dumppath", file,
      "--noprompt",
      "--triage", "full",
      "--displayname", projectName,
      "--properties", "STRESS_TESTID="+projectName,
      "--verbose" ])

def print_usage():
  print("DumplingHelper.py <command>")
  print("Commands:")
  print("  install_dumpling:")
  print("      - Installs dumpling globally on the machine.")
  print("  get_timestamp:")
  print("      - Prints out the current timestamp of the machine.")
  print("  collect_dump <exitcode> <folder> <starttime> <projectname>:")
  print("      - Collects and uploads the latest dump (after start time) from the folder to the dumpling service.")

# Main
def main(argv):
  if (len(argv) <= 1):
    print_usage()
    return
  if (argv[1] == "install_dumpling"):
    install_dumpling()
  elif (argv[1] == "get_timestamp"):
    get_timestamp()
  elif (argv[1] == "collect_dump"):
    collect_dump(argv[2], argv[3], argv[4], argv[5])
  else:
    print(argv[1] + " is not a valid command.")
    print_usage()
    return

dumplingPath = os.path.expanduser("~/.dumpling/dumpling.py")
if __name__ == '__main__':
  main(sys.argv)
