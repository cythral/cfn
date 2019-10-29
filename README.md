# CloudFormation (CFN)
Utilities, custom resources, and a GitHub webhook for deploying CloudFormation stacks.  This is currently a work-in-progress.  The plan is to have at least the following in the initial release:

### Utilities
- [x] Shared deployment bucket

### Custom Resources
Requirements for all Custom Resources:
- All existing properties must be supported if extending a resource already handled by AWS CloudFormation.
- Tags must be supported on resources that support tagging. 
---
- [x] Attribute for generating custom resource code
- [x] HostedZone with support for setting the DelegationSetId
- [ ] Upsertable record sets (capable of taking control of existing resource record sets, including the NS and SOA ones automatically created when the Hosted Zone is.)
- [x] Certificate with support for automatic DNS validation.  

### GitHub Webhook
Webhook capable of deploying CloudFormation stacks on github repository push events. This would be used for setting up pipeline stacks. 

## Future
Features to be added in the future:

- [ ] Custom resource that decrypts values with KMS
- [ ] Password generator custom resource
- [ ] Move networking out of the utilities stack and 