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
- [ ] Upsertable record sets (capable of taking control of existing resource record sets, including the NS and SOA ones automatically created when the Hosted Zone is.) **Update/Delete not implemented**.
- [x] Certificate with support for automatic DNS validation.  

### GitHub Webhook
Webhook capable of deploying CloudFormation stacks on github repository push events. This would be used for setting up pipeline stacks. 

## Installation
1. Create master (shared/central) and agent (dev/qual/prod) AWS accounts (best left unautomated).
   1. Create AWS CLI profiles for each of these accounts in `~/.aws/credentials`
2. Create a reusable delegation set in the master account
   1. Register a domain (where your webhook will live)
   2.  [Follow this guide](https://docs.aws.amazon.com/Route53/latest/DeveloperGuide/white-label-name-servers.html)
   3.  Take note of the reusable delegation set id and nameservers.
   4.  Update the DNS project with your domain and nameserver addresses.
   5.  Delete the record sets and hosted zone.  
3. Create an OAuth token on a github account that has access to your organization.  It will need to be able to setup webhooks on repositories, and read all repositories. Recommended: don't do this on a personal account, rather a dedicated CI/CD account.
4. Come up with a signing secret.  This will be passed from Github to the CICD webhook.  Requests without the signing secret will get rejected.    
5. Deploy cfn-utilities, cfn-resources, cfn-dns and cfn-core (in that order) to the master account. (Run the below command with the master account as the default/active profile).
    ```bash
    dotnet msbuild tools/deploy.proj -p:GithubOwner=xxx -p:GithubToken=xxx -p:GithubSigningSecret=xxx
    ```
   - GithubOwner should be the name of your GitHub org/team.
6. Install the webhook to your organization (org settings -> webhook).  There will be a field called secret where you can put your signing secret.  The URL will be https://yourdomain/webhooks/github.  The path can be customized in cfn-core. 
7. Deploy cfn-utilities to each of the agent accounts.
    ```bash
    dotnet msbuild src/Utilities/Utilities.proj -p:MasterAccountId=xxxxxx -p:Profile=dev|qual|prod|etc
    ```
## Future
Features to be added in the future:

- [ ] Custom resource that decrypts values with KMS
- [ ] Password generator custom resource
- [ ] Customize which accounts can invoke the custom resources