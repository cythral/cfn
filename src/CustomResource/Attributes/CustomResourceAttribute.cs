using System;

using CodeGeneration.Roslyn;

namespace Cythral.CloudFormation.CustomResource.Attributes
{

    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    [CodeGenerationAttribute(typeof(Generator))]
    public class CustomResourceAttribute : System.Attribute
    {
        public CustomResourceAttribute() { }

        public Type ResourcePropertiesType { get; set; }

        public object[] Grantees { get; set; }

        public GranteeType GranteeType { get; set; }
    }
}