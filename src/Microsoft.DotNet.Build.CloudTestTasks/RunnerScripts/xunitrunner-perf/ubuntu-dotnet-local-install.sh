#!/bin/bash
# Argument = -d destdir
usage()
{
cat << EOF
usage: $0 options

This script installs dotnet cli on the linux machine

OPTIONS:
   -h      Show this message
   -d      directory where dotnet cli will be installed

EOF
}

DESTDIR=
while getopts “hd:” OPTION
do
     case $OPTION in
         h)
             usage
             exit 1
             ;;
         d)
             DESTDIR=$OPTARG
             ;;
         ?)
             usage
             exit
             ;;
     esac
done

echo $DESTDIR
if [[ -z $DESTDIR ]]
then
     usage
     exit 1
fi

echo "Initiating local dotnet install"
wget https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/dotnet-install.sh
chmod 777 dotnet-install.sh
wget https://raw.githubusercontent.com/Microsoft/xunit-performance/master/DotNetCliVersion.txt
versionnum=$(cat DotNetCliVersion.txt)
mkdir $DESTDIR
source dotnet-install.sh -i $DESTDIR -v $versionnum