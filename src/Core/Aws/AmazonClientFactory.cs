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
    public class AmazonClientFactory<TInterface, TImplementation> where TImplementation : TInterface where TInterface : Amazon.Runtime.IAmazonService
    {
        private static readonly Func<TInterface> New = Expression.Lambda<Func<TInterface>>(Expression.New(typeof(TImplementation))).Compile();

        // (credentials) => new AmazonServiceClient(credentials)
        private static readonly Func<AWSCredentials, TInterface> NewWithCredentials;

        static AmazonClientFactory()
        {
            var param = Expression.Parameter(typeof(AWSCredentials), "credentials");
            NewWithCredentials = Expression.Lambda<Func<AWSCredentials, TInterface>>(
                Expression.New(
                    typeof(TImplementation).GetConstructor(new Type[] { typeof(AWSCredentials) }),
                    new Expression[] { param }
                ),
                true,
                new ParameterExpression[] { param }
            ).Compile();
        }

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