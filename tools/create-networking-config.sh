#!/bin/bash

get_export() {
    stack=$1
    name=$2

    echo -n $(aws cloudformation list-exports --query Exports[?Name==\`${stack}:${name}\`].Value --output text);
}

devPeeringConnectionId=
devLoadBalancerDnsName=$(get_export cfn-metadata DevLoadBalancerDnsName)
prodPeeringConnectionId=$(get_export cfn-core ProdPeeringConnectionId)
prodLoadBalancerDnsName=$(get_export cfn-metadata ProdLoadBalancerDnsName)

config={}
config=$(echo $config | jq ".DevPeeringConnectionId=\"$devPeeringConnectionId\"") 
config=$(echo $config | jq ".DevLoadBalancerDnsName=\"$devLoadBalancerDnsName\"")
config=$(echo $config | jq ".ProdPeeringConnectionId=\"$prodPeeringConnectionId\"")
config=$(echo $config | jq ".ProdLoadBalancerDnsName=\"$prodLoadBalancerDnsName\"") 

echo $config