using System;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization;
using Amazon.Lambda.Serialization.Json;
using Amazon.Lambda.APIGatewayEvents;
using Cythral.CloudFormation.Cicd.Events;

namespace Cythral.CloudFormation.Cicd {
    class Webhook {

        [LambdaSerializer(typeof(JsonSerializer))]
        public APIGatewayProxyResponse Handler(APIGatewayProxyRequest request, ILambdaContext context) {
            var payload = System.Text.Json.JsonSerializer.Deserialize<PushEvent>(request.Body);

            return new APIGatewayProxyResponse {
                StatusCode = 200
            };
        }

        static void Main(string[] args) {}
    }
}
