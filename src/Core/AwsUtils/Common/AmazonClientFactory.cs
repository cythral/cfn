using System;
using System.IO;
using System.Linq.Expressions;
using System.Threading.Tasks;

using Amazon;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

namespace Cythral.CloudFormation.AwsUtils
{
    public class AmazonClientFactory<TInterface> where TInterface : Amazon.Runtime.IAmazonService
    {
        private static readonly Func<TInterface> New;

        // (credentials) => new AmazonServiceClient(credentials)
        private static readonly Func<AWSCredentials, TInterface> NewWithCredentials;

        static AmazonClientFactory()
        {
            var param = Expression.Parameter(typeof(AWSCredentials), "credentials");
            var interfaceType = typeof(TInterface);
            var implementationTypeName = interfaceType.Namespace + "." + interfaceType.Name.Substring(1) + "Client";
            var implementationType = interfaceType.Assembly.GetType(implementationTypeName)!;

            New = Expression.Lambda<Func<TInterface>>(Expression.New(implementationType)).Compile();

            NewWithCredentials = Expression.Lambda<Func<AWSCredentials, TInterface>>(
                Expression.New(
                    implementationType.GetConstructor(new Type[] { typeof(AWSCredentials) })!,
                    new Expression[] { param }
                ),
                true,
                new ParameterExpression[] { param }
            ).Compile();
        }

        public virtual async Task<TInterface> Create(string? roleArn = null)
        {
            if (roleArn != null)
            {
                var stsFactory = new AmazonClientFactory<IAmazonSecurityTokenService>();
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