# CloudFormation (CFN)
Utilities, custom resources and infrastructure for rapid delivery of CloudFormation stacks on AWS. This project is in the process of being split up into smaller components, each in their own repository.  Once complete, this project will only provide a few simple utilities and act as a bootstrapper.


## Installation
An effort to automate most of these steps will be made at some point in the future. Once installed, there is no need to manually deploy any of these projects again unless any of the stacks get deleted.  


1. Create master (shared/central) and agent (dev/qual/prod) AWS accounts (best left unautomated).
2. Deploy a stack called cfn-metadata to the master account.  This stack should contain the following outputs:
    - cfn-metadata:DevAccountId - id of the dev account
    - cfn-metadata:ProdAccountId - id of the prod account
    - cfn-metadata:DevAgentRoleArn - arn:aws:iam::{DevAccountId}:role/Agent
    - cfn-metadata:ProdAgentRoleArn - arn:aws:iam::{ProdAccountId}:role/Agent
3. Create AWS CLI profiles for each of these accounts in `~/.aws/credentials`. 
    * Use the shared account credentials as the default profile.
    * Dev and prod should have profiles with their lowercase names
4. Create a reusable delegation set in the master account
   1. Register a domain (where your webhook will live)
   2.  [Follow this guide](https://docs.aws.amazon.com/Route53/latest/DeveloperGuide/white-label-name-servers.html)
   3.  Take note of the reusable delegation set id and nameservers.
   4.  Update the DNS project with your domain, nameserver addresses and associated vanity names.
   5.  Delete the record sets and hosted zone.  
5. Create an OAuth token on a github account that has access to your organization.  It will need to be able to setup webhooks on repositories, and read all repositories. Recommended: don't do this on a personal account, rather a dedicated CI/CD account.
6. Come up with a signing secret.  This will be passed from Github to the CICD webhook.  Requests without the signing secret will get rejected.    
7. Deploy cfn-metadata, cfn-utilities, cfn-resources, cfn-dns and cfn-core (in that order) to the master account. (Run the below command with the master account as the default/active profile).
    ```bash
    dotnet msbuild tools/deploy.proj \
        -p:DevAccountId=xxx \
        -p:ProdAccountId=xxx \
        -p:GithubOwner=xxx \
        -p:GithubToken=xxx \
        -p:GithubSigningSecret=xxx
    ```
   - GithubOwner should be the name of your GitHub org/team.
8. Install the webhook to your organization (org settings -> webhook).  There will be a field called secret where you can put your signing secret.  The URL will be https://yourdomain/webhooks/github.  The path can be customized in cfn-core.
9. Forget what you set the signing secret to.  If you need to rotate it, run this:
    ```bash
    aws kms encrypt --key-id alias/SecretsKey --plaintext $SIGNING_SECRET --query CiphertextBlob --output text
    ```
    then update Parameters.GithubSigningSecret in src/Core/Core.config.json with the output.

10.  Deploy cfn-utilities to each of the agent accounts.
    ```bash
    dotnet msbuild src/Utilities/Utilities.proj -p:MasterAccountId=xxxxxx -p:Profile=dev|qual|prod|etc
    ```

## Infra Design Decisions
- Use reusable delegation sets to setup vanity nameservers.  That way, I can give a client a set of nameservers to point their domain to, before even creating a hosted zone for it.  This also means they don't have to update their nameservers if the hosted zone accidentally gets deleted (plus no ^48 hour wait time for those changes to take effect).  
  - Shared account has a VPC with a CIDR of 10.1.0.0/16. DNS records will get pointed to an internet facing load balancer in this account, which peers with VPCs in the dev and prod accounts. 
  - Dev has a VPC with a CIDR of 10.2.0.0/16 and Prod's is 10.3.0.0/16.  Traffic from the load balancer to ECS Services is routed via AppMesh.
