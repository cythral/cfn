#!/bin/sh

dotnet pack
file=$(ls bin/Debug | grep nupkg | head -n 1)
rm -rf /tmp/cfn-custom-resource-pack
mkdir -p /tmp/cfn-custom-resource-pack
unzip bin/Debug/$file -d /tmp/cfn-custom-resource-pack
chmod -R 755 /tmp/cfn-custom-resource-pack

dir=$(pwd);
cd /tmp/cfn-custom-resource-pack

mkdir tools
cd lib
cp -r . ../tools
cd ../

rm $dir/bin/Debug/$file
zip -r $dir/bin/Debug/$file .
cd $dir
rm -rf /tmp/cfn-custom-resource-pack

