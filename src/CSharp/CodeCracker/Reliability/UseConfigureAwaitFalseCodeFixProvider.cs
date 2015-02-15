﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace CodeCracker.CSharp.Reliability
{
    [ExportCodeFixProvider("CodeCrackerUseConfigureAwaitFalseCodeFixProvider", LanguageNames.CSharp), Shared]
    public class UseConfigureAwaitFalseCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(DiagnosticId.UseConfigureAwaitFalse.ToDiagnosticId());
            }
        }

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var awaitExpression = (AwaitExpressionSyntax)root.FindNode(diagnostic.Location.SourceSpan);
            var newExpression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    awaitExpression.Expression,
                    SyntaxFactory.IdentifierName("ConfigureAwait")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(
                            SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression)))))
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newRoot = root.ReplaceNode(awaitExpression.Expression, newExpression);
            var newDocument = context.Document.WithSyntaxRoot(newRoot);

            context.RegisterCodeFix(
                CodeAction.Create("Use ConfigureAwait(false)", ct => Task.FromResult(newDocument)),
                diagnostic);
        }
    }
}