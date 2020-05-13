#!/bin/bash

get_export() {
    stack=$1
    name=$2

    echo -n $(aws cloudformation list-exports --query Exports[?Name==\`${stack}:${name}\`].Value --output text);
}

export DEV_PEERING_ID=$(get_export cfn-core DevPeeringConnectionId)
export PROD_PEERING_ID=$(get_export cfn-core ProdPeeringConnectionId)