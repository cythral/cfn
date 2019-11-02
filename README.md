# CloudFormation (CFN)
Utilities, custom resources, and a GitHub webhook for deploying CloudFormation stacks.  This is currently a work-in-progress.  The plan is to have at least the following in the initial release:

### Utilities
- [x] Shared deployment bucket
- [x] Networking resources (vpc, subnets)
- [x] Encryption resources (artifact/secret encryption keys)

### Custom Resources
Requirements for all Custom Resources:
- All existing properties must be supported if extending a resource already handled by AWS CloudFormation.
- Tags must be supported on resources that support tagging. 
---
- [x] Attribute for generating custom resource code
- [x] HostedZone with support for setting the DelegationSetId
- [ ] Upsertable record sets (capable of taking control of existing resource record sets, including the NS and SOA ones automatically created when the Hosted Zone is.) **Update/Delete not yet implemented**.
- [x] Certificate with support for automatic DNS validation.  

### GitHub Webhook
Webhook capable of deploying CloudFormation stacks on github repository push events. This would be used for setting up pipeline stacks. 

## Installation
An effort to automate most of these steps (save #1, #6) will be made at some point in the future. Once installed, there is no need to manually deploy any of these projects again unless any of the stacks get deleted.  

1. Create master (shared/central) and agent (dev/qual/prod) AWS accounts (best left unautomated).
2. Create AWS CLI profiles for each of these accounts in `~/.aws/credentials`
3. Create a reusable delegation set in the master account
   1. Register a domain (where your webhook will live)
   2.  [Follow this guide](https://docs.aws.amazon.com/Route53/latest/DeveloperGuide/white-label-name-servers.html)
   3.  Take note of the reusable delegation set id and nameservers.
   4.  Update the DNS project with your domain, nameserver addresses and associated vanity names.
   5.  Delete the record sets and hosted zone.  
4. Create an OAuth token on a github account that has access to your organization.  It will need to be able to setup webhooks on repositories, and read all repositories. Recommended: don't do this on a personal account, rather a dedicated CI/CD account.
5. Come up with a signing secret.  This will be passed from Github to the CICD webhook.  Requests without the signing secret will get rejected.    
6. Deploy cfn-metadata, cfn-utilities, cfn-resources, cfn-dns and cfn-core (in that order) to the master account. (Run the below command with the master account as the default/active profile).
    ```bash
    dotnet msbuild tools/deploy.proj \
        -p:DevAccountId=xxx \
        -p:ProdAccountId=xxx \
        -p:GithubOwner=xxx \
        -p:GithubToken=xxx \
        -p:GithubSigningSecret=xxx
    ```
   - GithubOwner should be the name of your GitHub org/team.
7. Install the webhook to your organization (org settings -> webhook).  There will be a field called secret where you can put your signing secret.  The URL will be https://yourdomain/webhooks/github.  The path can be customized in cfn-core.
8. Forget what you set the signing secret to.  If you need to rotate it, run this:
    ```bash
    aws kms encrypt --key-id alias/SecretsKey --plaintext $SIGNING_SECRET --query CiphertextBlob --output text
    ```
    then update Parameters.GithubSigningSecret in src/Core/Core.config.json with the output.

9.  Deploy cfn-utilities to each of the agent accounts.
    ```bash
    dotnet msbuild src/Utilities/Utilities.proj -p:MasterAccountId=xxxxxx -p:Profile=dev|qual|prod|etc
    ```

## Infra Design Decisions
- Avoid use of API Gateway wherever possible, use ALBs instead.  Once the free tier of API Gateway is up, things become very expensive very fast.
- Use reusable delegation sets to setup vanity nameservers.  That way, I can give a client a set of nameservers to point their domain to, before even creating a hosted zone for it.  This also means they don't have to update their nameservers if the hosted zone accidentally gets deleted (plus no ^48 hour wait time for those changes to take effect).  
- Only 3 load balancers will be used.  One for each account. This is approximately $50/mo for the load balancers themselves ($0.0225 * 24 * 30).  Additional charge based on LCU metrics will occur based on traffic.
  - Shared account has a VPC with a CIDR of 10.1.0.0/16. DNS records will get pointed to an internet facing load balancer in this account, which peers with VPCs in the dev and prod accounts.  Routing is chosen based on whether the host header contains a dev subdomain.
  - Dev has a VPC with a CIDR of 10.2.0.0/16 and Prod's is 10.3.0.0/16.  Load balancers in these accounts are internal.  These typically route to targets based on the full host-header. 

## Future
Notable features to be added in the future:

- [ ] Custom resource that decrypts values with KMS
- [ ] Password generator custom resource
- [ ] Customize which accounts can invoke the custom resources