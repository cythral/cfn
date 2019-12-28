using System;

namespace Cythral.CloudFormation.CustomResource.Attributes
{
    [AttributeUsage(AttributeTargets.Interface, Inherited = true, AllowMultiple = true)]
    public class AmazonApiAttribute : Attribute
    {
        public AmazonApiAttribute(Type api) { }
    }
}