using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cythral.CloudFormation.CustomResource
{

    public class ValidationGenerator
    {

        private ISymbol Property;

        private AttributeData Data;

        public ValidationGenerator(ISymbol property, AttributeData attributeData)
        {
            Property = property;
            Data = attributeData;
        }

        public StatementSyntax GenerateStatement()
        {
            var expression = GenerateInvocationExpression();

            return SyntaxFactory.ExpressionStatement(expression);
        }

        public ExpressionSyntax GenerateInvocationExpression()
        {
            var objCreationExpr = GenerateObjectCreationExpression();
            var accessName = (SimpleNameSyntax)SyntaxFactory.ParseName("Validate");
            var accessExpr = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, objCreationExpr, accessName);
            var args = SyntaxFactory.ParseArgumentList($"(Request.ResourceProperties.{Property.Name}, \"{Property.Name}\")");

            return SyntaxFactory.InvocationExpression(accessExpr, args);
        }

        public ExpressionSyntax GenerateObjectCreationExpression()
        {
            var args = GenerateConstructorArgumentList();
            var initializer = GenerateInitializerExpression();
            var attributeType = SyntaxFactory.ParseTypeName(Data.AttributeClass.Name);
            var objCreationExpr = SyntaxFactory.ObjectCreationExpression(attributeType, args, initializer);

            return SyntaxFactory.ParenthesizedExpression(objCreationExpr);
        }

        public ArgumentListSyntax GenerateConstructorArgumentList()
        {
            var args = GenerateConstructorArguments();
            var list = SyntaxFactory.SeparatedList(args);

            return SyntaxFactory.ArgumentList(list);
        }

        private IEnumerable<ArgumentSyntax> GenerateConstructorArguments()
        {
            foreach (var arg in Data.ConstructorArguments)
            {
                var expression = SyntaxFactory.ParseExpression(arg.ToCSharpString());

                yield return SyntaxFactory.Argument(expression);
            }
        }

        public InitializerExpressionSyntax GenerateInitializerExpression()
        {
            var args = GenerateInitializerArguments();
            var list = SyntaxFactory.SeparatedList(args);

            return SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, list);
        }

        private IEnumerable<ExpressionSyntax> GenerateInitializerArguments()
        {
            foreach (var arg in Data.NamedArguments)
            {
                var left = SyntaxFactory.ParseExpression(arg.Key.ToString());
                var right = SyntaxFactory.ParseExpression(arg.Value.ToCSharpString());

                yield return SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, left, right);
            }
        }

    }

}