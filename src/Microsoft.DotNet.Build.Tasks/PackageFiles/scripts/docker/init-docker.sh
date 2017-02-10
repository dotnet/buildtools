#!/usr/bin/env bash

# Stop script on NZEC
set -e
# Stop script if unbound variable found (use ${var:-} if intentional)
set -u

say_err() {
    printf "%b\n" "Error: $1" >&2
}

showHelp() {
    echo "Usage: $scriptName [OPTIONS] [IMAGE_NAME[:TAG|@DIGEST]]"
    echo
    echo "Initializes Docker by:"
    echo "  - Emitting the version of Docker that is being used"
    echo "  - Removing all containers and images that exist on the machine"
    echo "  - Ensuring the latest copy of the specified image exists on the machine"
    echo
    echo "Options:"
    echo "  -r, --retryCount    Number of times to retry pulling image on error"
    echo "  -w, --waitFactor    Time (seconds) to wait between pulls (time is multiplied each iteration)"
}

# Executes a command and retries if it fails.
execute() {
    local count=0
    until "$@"; do
        local exit=$?
        count=$(( $count + 1 ))
        if [ $count -lt $retries ]; then
            local wait=$(( waitFactor ** (( count - 1 )) ))
            echo "Retry $count/$retries exited $exit, retrying in $wait seconds..."
            sleep $wait
        else    
            say_err "Retry $count/$retries exited $exit, no more retries left."
            return $exit
        fi
    done

    return 0
}

scriptName=$0
retries=5
waitFactor=6
image=

while [ $# -ne 0 ]; do
    name=$1
    case $name in
        -h|--help)
            showHelp
            exit 0
            ;;
        -r|--retryCount)
            shift
            retries=$1
            ;;
        -w|--waitFactor)
            shift
            waitFactor=$1
            ;;
        -*)
            say_err "Unknown option: $1"
            exit 1
            ;;
        *)
            if [ ! -z "$image" ]; then
                say_err "Unknown argument: \`$name\`"
                exit 1
            fi

            image="$1"
            ;;
    esac

    shift
done

# Capture Docker version for diagnostic purposes
docker --version
echo

echo "Cleaning Docker Artifacts"
sourceDir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
"$sourceDir/cleanup-docker.sh"
echo

if [ ! -z "$image" ]; then
    echo "Pulling Docker image $image"
    execute docker pull $image
fi
