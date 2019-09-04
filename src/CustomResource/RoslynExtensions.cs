using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Cythral.CloudFormation.CustomResource {

    public static class RoslynExtensions {

        public static string GetFullName(this ClassDeclarationSyntax node) {
            return node.Parent == null ? node.Identifier.ValueText : node.Parent.GetFullName() + "." + node.Identifier.ValueText;
        }

        public static string GetFullName(this NamespaceDeclarationSyntax node) {
            return node.Parent?.GetFullName() == null ? node.Name.ToString() : node.Parent.GetFullName() + "." + node.Name.ToString();
        }

        public static string GetFullName(this SyntaxNode node) {
            if(node is ClassDeclarationSyntax classNode) {
                return classNode.GetFullName();
            }

            if(node is NamespaceDeclarationSyntax namespaceNode) {
                return namespaceNode.GetFullName();
            }

            return null;
        }

        public static bool IsGlobalNamespace(this NamespaceDeclarationSyntax node) {
            return node.Parent == null && node.Name.ToString() == "";
        }
    }
}