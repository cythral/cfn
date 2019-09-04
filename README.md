# CloudFormation.CustomResource
A C# attribute for creating Lambda-backed CloudFormation custom resources.  

# Installation

1. Install the CustomResource package:
```bash
dotnet add package Cythral.CloudFormation.CustomResource
```

2. Add this line to your .csproj file:

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