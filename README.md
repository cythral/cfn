# CloudFormation.CustomResource
A C# attribute for creating Lambda-backed CloudFormation custom resources.  

# Installation


```bash
dotnet add package Cythral.CloudFormation.CustomResource
```


# Usage
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Cythral.CloudFormation.CustomResource;

namespace Example {
    [CustomResource(typeof(ResourcePropertiesType))] // replace ResourcePropertiesType with the name of your ResourceProperties model
    public partial class ExampleClass {
        public async Task<object> Create() {
            // do stuff
        }

        public async Task<object> Delete() {
            // do stuff
        }

        public async Task<object> Update() {

        }
    }

}
```