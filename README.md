# CloudFormation
Utilities, custom resources, and a GitHub webhook for deploying CloudFormation stacks.  This is currently a work-in-progress.  The plan is to have at least the following in the initial release:

## Utilities
- [x] Shared deployment bucket

## Custom Resources
- [x] Attribute for generating custom resource code
- [x] HostedZone with support for setting the DelegationSetId
- [ ] Upsertable record sets (capable of taking control of existing resource record sets, including the NS and SOA ones automatically created when the Hosted Zone is.)
- [ ] Certificate with support for automatic DNS validation.  

## GitHub Webhook
Webhook capable of deploying CloudFormation stacks on github repository push events. This would be used for setting up pipeline stacks. 