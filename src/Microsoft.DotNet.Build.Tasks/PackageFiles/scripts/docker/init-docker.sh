#!/usr/bin/env bash
set -e

scriptName=$0

function usage {
    echo "Usage: $scriptName image"
    echo "  image      Name of the Docker image to pull"

    exit 1
}

# Executes a command and retry up to 5 times until if it fails.
function execute {
  local retries=5
  local waitFactor=3

  local count=0
  until "$@"; do  
    count=$(($count + 1))
    if [ $count -lt $retries ]; then
      local wait=$((waitFactor ** count))
      echo "Retry $count/$retries exited $exit, retrying in $wait seconds..."
      sleep $wait
    else
      local exit=$?
      echo "Retry $count/$retries exited $exit, no more retries left."
      return $exit
    fi
  done

  return 0
}

if [ -z "$1" ]; then
  usage
fi

# Capture Docker version for diagnostic purposes
docker --version
echo 

echo "Cleaning Docker Artifacts"
./cleanup-docker.sh
echo

image=$1
echo "Pulling Docker image $image"
execute docker pull $image
