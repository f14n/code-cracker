﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace CodeCracker.CSharp.Style
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TaskNameAsyncAnalyzer : DiagnosticAnalyzer
    {
        internal const string Title = "Async method can be terminating with 'Async' name.";
        internal const string MessageFormat = "Change method name to {0}";
        internal const string Category = SupportedCategories.Style;
        const string Description = "Async method can be terminating with 'Async' name.";

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId.TaskNameAsync.ToDiagnosticId(),
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: HelpLink.ForDiagnostic(DiagnosticId.TaskNameAsync));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context) => context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var method = (MethodDeclarationSyntax)context.Node;
            if (method.Identifier.ToString().EndsWith("Async")) return;

            var errorMessage = method.Identifier.ToString() + "Async";
            var diag = Diagnostic.Create(Rule, method.GetLocation(), errorMessage);

            if (method.Modifiers.Any(SyntaxKind.AsyncKeyword))
            {
                context.ReportDiagnostic(diag);
                return;
            }
            var returnType = context.SemanticModel.GetSymbolInfo(method.ReturnType).Symbol as INamedTypeSymbol;
            if (returnType == null) return;

            if (returnType.ToString() == "System.Threading.Tasks.Task" ||
                (returnType.IsGenericType && returnType.ConstructedFrom.ToString() == "System.Threading.Tasks.Task<TResult>"))
            {
                context.ReportDiagnostic(diag);
            }

        }
    }
}