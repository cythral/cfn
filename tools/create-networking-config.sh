#!/bin/bash

get_export() {
    stack=$1
    name=$2

    echo -n $(aws cloudformation list-exports --query Exports[?Name==\`${stack}:${name}\`].Value --output text);
}

devPeeringConnectionId=$(get_export cfn-core DevPeeringConnectionId)
prodPeeringConnectionId=$(get_export cfn-core ProdPeeringConnectionId)

config={}
config=$(echo $config | jq ".DevPeeringConnectionId=\"$devPeeringConnectionId\"") 
config=$(echo $config | jq ".ProdPeeringConnectionId=\"$prodPeeringConnectionId\"")) 

echo $config