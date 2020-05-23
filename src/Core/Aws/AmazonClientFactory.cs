using System.IO;
using System.Linq.Expressions;
using System;
using System.Threading.Tasks;

using Amazon;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

namespace Cythral.CloudFormation.Aws
{
    public class AmazonClientFactory<TInterface, TImplementation> where TImplementation : TInterface, new() where TInterface : Amazon.Runtime.IAmazonService
    {
        private static readonly Func<TInterface> New = Expression.Lambda<Func<TInterface>>(Expression.New(typeof(TInterface))).Compile();

        // (credentials) => new AmazonServiceClient(credentials)
        private static readonly Func<AWSCredentials, TImplementation> NewWithCredentials =
            Expression.Lambda<Func<AWSCredentials, TImplementation>>(
                Expression.New(
                    typeof(TImplementation).GetConstructor(new Type[] { }),
                    new Expression[] { Expression.Parameter(typeof(AWSCredentials), "credentials") }
                ),
                true,
                new ParameterExpression[] { Expression.Parameter(typeof(AWSCredentials), "credentials") }
            ).Compile();

        public virtual async Task<TInterface> Create(string roleArn = null)
        {
            if (roleArn != null)
            {
                var stsFactory = new AmazonClientFactory<IAmazonSecurityTokenService, AmazonSecurityTokenServiceClient>();
                var client = await stsFactory.Create();

                var response = await client.AssumeRoleAsync(new AssumeRoleRequest
                {
                    RoleArn = roleArn,
                    RoleSessionName = "client-ops"
                });

                return NewWithCredentials(response.Credentials);
            }

            return New();
        }
    }
}