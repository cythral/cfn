using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeGeneration.Roslyn.Engine;
using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Amazon.Runtime;

namespace Cythral.CloudFormation.CustomResource {
    public class PermissionsCollector : CSharpSyntaxWalker {

        private TransformationContext context;

        public HashSet<string> Permissions { get; private set; } = new HashSet<string>();

        public PermissionsCollector(TransformationContext context) {
            this.context = context;
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node) {
            try {
                // var testType = context.SemanticModel.GetTypeInfo(node).Type as INamedTypeSymbol;
                // Permissions.Add(testType.TypeArguments[0].Name);


                var type = GetCallingMemberType(node);
                if(type == null || !IsAmazonResponseType(type)) {
                    foreach(var child in node.ChildNodes()) {
                        Visit(child);
                    }

                    return;
                }
                
                
                var assembly = LoadAssemblyForType(type);
                var configClassName = GetConfigClassName(type);
                var metadataType = assembly.GetType(configClassName);
                var metadata = (ClientConfig) Activator.CreateInstance(metadataType);
                var iamPrefix = metadata.AuthenticationServiceName;
                var apiCallName = GetApiCallName(node);
                var permission = iamPrefix + ":" + apiCallName;

                if(permission == "lambda:Invoke") permission = "lambda:InvokeFunction";

                Permissions.Add(permission);
            } catch(Exception) {
                foreach(var child in node.ChildNodes()) {
                    Visit(child);
                    return;
                }
            }
        }

        private ITypeSymbol GetCallingMemberType(InvocationExpressionSyntax node) {
            try {

                var symbol = context.SemanticModel.GetTypeInfo(node).Type as INamedTypeSymbol;
                return symbol.TypeArguments[0];
                
            } catch(Exception) {
                return null;
            }
        }

        private string GetApiCallName(InvocationExpressionSyntax node) {
            try {
                var accessExpression = node.Expression as MemberAccessExpressionSyntax;
                var matcher = new Regex("Async$");
                return matcher.Replace(accessExpression.Name.ToString(), "");
            } catch(Exception) {
                return null;
            }
        }

        private bool IsAmazonResponseType(ITypeSymbol type) {
            try {
                return type.ContainingAssembly.Name.Split('.')[0] == "AWSSDK" && 
                    type.Name.EndsWith("Response");
            } catch(Exception) {
                return false;
            }
        }

        // gets the assembly for an amazon api call
        private Assembly LoadAssemblyForType(ITypeSymbol type) {
            var assemblyReferences = from reference in context.Compilation.ExternalReferences 
                where Path.GetFileNameWithoutExtension(reference.Display) == type.ContainingAssembly.Name 
                select reference.Display;

            var referenceLocation = assemblyReferences.First();
            return Assembly.LoadFrom(referenceLocation);
        }

        private string GetConfigClassName(ITypeSymbol type) {
            var fullNS = type.ContainingNamespace.ToString();
            var baseNS = string.Join(".", fullNS.Split('.').Take(2));
            var squashedNS = string.Join("", fullNS.Split('.').Take(2));
            return baseNS + "." + squashedNS + "Config";
        }
    }
}