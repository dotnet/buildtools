#!/bin/bash
# Argument = -d destdir -v versionfile
usage()
{
cat << EOF
usage: $0 options

This script installs dotnet cli on the linux machine

OPTIONS:
   -h      Show this message
   -d      Directory where dotnet cli will be installed
   -v      Location of DotNetCliVersion.txt

EOF
}

DESTDIR=
VERFILE=
while getopts “hd:v:” OPTION
do
     case $OPTION in
         h)
             usage
             exit 1
             ;;
         d)
             DESTDIR=$OPTARG
             ;;
         v)
             VERFILE=$OPTARG
             ;;
         ?)
             usage
             exit
             ;;
     esac
done

echo $DESTDIR
echo $VERFILE
if [ -z $DESTDIR ] || [ -z $VERFILE ];
then
     usage
     exit 1
fi

echo "Initiating local dotnet install"
mkdir $DESTDIR
wget https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0-preview2/scripts/obtain/dotnet-install.sh -P $DESTDIR
chmod 777 $DESTDIR/dotnet-install.sh
versionnum=$(cat $VERFILE)
source $DESTDIR/dotnet-install.sh -i $DESTDIR -v $versionnum