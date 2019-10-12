using System;
using System.Threading.Tasks;
using Cythral.CloudFormation.CustomResource;
using RichardSzalay.MockHttp;

namespace Tests {
    public abstract class TestCustomResource {
        public Task<Response> Create() {
            ThrowIfNotPassing();

            return Task.FromResult(new Response {
                Data = new {
                    Status = "Created"
                }
            });
        }

        public Task<Response> Update() {
            ThrowIfNotPassing();

            return Task.FromResult(new Response {
                Data = new {
                    Status = "Updated"
                }
            });
        }

        public Task<Response> Delete() {
            ThrowIfNotPassing();

            return Task.FromResult(new Response {
                Data = new {
                    Status = "Deleted"
                }
            });
        }

        public virtual void ThrowIfNotPassing() {}
    }
}