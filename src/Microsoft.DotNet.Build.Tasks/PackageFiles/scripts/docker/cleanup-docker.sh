#!/usr/bin/perl

#
# ./cleanup-docker.sh 
#

# cleanup containers
my $psList = `docker ps -a`;
my @psItems = split /\n/, $psList;
foreach(@psItems) {
  if($_ =~ /.*\s+([^\s]+)$/ig) {
    my $containerName = $1;
    if($containerName !~ /NAME/ig) {
      printf "delete container $containerName\n";
      my $deleteOutput = `docker rm -f $1`;
      print "$deleteOutput\n";
    }
  }
}

#cleanup images
my $imageList = `docker images`;
@imageItems = split /\n/, $imageList;
foreach(@imageItems) {
  if($_ =~ /([^\s]+)\s+([^\s]+)\s+([^\s]+)\s+.*/ig) {
    my $imageId = $3;
    if($imageId !~ /IMAGE/ig) {
      printf "delete image ID $imageId\n";
      my $deleteImageOutput = `docker rmi $imageId`;
      printf "$deleteImageOutput\n";
    }
  }
}
