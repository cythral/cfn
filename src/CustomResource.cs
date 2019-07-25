using System;
using System.Threading.Tasks;
using CodeGeneration.Roslyn;

namespace Cythral.CloudFormation.CustomResource {
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    [CodeGenerationAttribute(typeof(Generator))]
    public class CustomResource : Attribute {
        private Type ResourcePropertiesType;

        public CustomResource(Type resourcePropertiesType) {
            ResourcePropertiesType = resourcePropertiesType; 
        }        
    }
}