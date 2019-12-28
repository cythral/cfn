using System;
using System.Threading.Tasks;

using Cythral.CloudFormation.CustomResource;

using RichardSzalay.MockHttp;

namespace Tests
{
    public abstract class TestCustomResource
    {
        public bool CreateWasCalled { get; set; }
        public bool UpdateWasCalled { get; set; }
        public bool DeleteWasCalled { get; set; }

        public Task<Response> Create()
        {
            CreateWasCalled = true;
            ThrowIfNotPassing();

            return Task.FromResult(new Response
            {
                Data = new
                {
                    Status = "Created"
                }
            });
        }

        public Task<Response> Update()
        {
            UpdateWasCalled = true;
            ThrowIfNotPassing();

            return Task.FromResult(new Response
            {
                Data = new
                {
                    Status = "Updated"
                }
            });
        }

        public Task<Response> Delete()
        {
            DeleteWasCalled = true;
            ThrowIfNotPassing();

            return Task.FromResult(new Response
            {
                Data = new
                {
                    Status = "Deleted"
                }
            });
        }

        public virtual void ThrowIfNotPassing() { }
    }
}