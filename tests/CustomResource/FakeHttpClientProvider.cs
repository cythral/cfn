using System;
using System.Net.Http;
using Cythral.CloudFormation.CustomResource;
using RichardSzalay.MockHttp;

namespace Tests {
    public class FakeHttpClientProvider : IHttpClientProvider {
        public readonly MockHttpMessageHandler httpMock;

        public FakeHttpClientProvider(MockHttpMessageHandler httpMock) {
            this.httpMock = httpMock;
        }

        public HttpClient Provide() {
            return httpMock.ToHttpClient();
        }
    }
}