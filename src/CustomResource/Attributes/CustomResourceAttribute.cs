using System;
using System.Threading.Tasks;
using CodeGeneration.Roslyn;

namespace Cythral.CloudFormation.CustomResource.Attributes {

    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    [CodeGenerationAttribute(typeof(Generator))]
    public class CustomResourceAttribute : System.Attribute {
        
        private Type ResourcePropertiesType;

        public CustomResourceAttribute(Type resourcePropertiesType) {
            ResourcePropertiesType = resourcePropertiesType; 
        } 
    }
}