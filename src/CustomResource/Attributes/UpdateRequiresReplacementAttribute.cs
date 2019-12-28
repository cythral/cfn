using System;

namespace Cythral.CloudFormation.CustomResource.Attributes
{
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
    public class UpdateRequiresReplacementAttribute : Attribute
    {
        public UpdateRequiresReplacementAttribute() { }
    }
}