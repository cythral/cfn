using System;
using System.Net;
using System.Net.Http;

namespace Cythral.CloudFormation.CustomResource {

    public interface IHttpClientProvider {

        HttpClient Provide();
        
    }
    
}