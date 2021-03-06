﻿Imports System.Collections.Immutable
Imports System.Composition
Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeActions
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Formatting
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Refactoring
    <ExportCodeFixProvider("ParameterRefactoryCodeFixProvider", LanguageNames.VisualBasic)>
    <[Shared]>
    Public Class ParameterRefactoryCodeFixProvider
        Inherits CodeFixProvider

        Public Overrides Async Function ComputeFixesAsync(context As CodeFixContext) As Task
            Dim root = Await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(False)

            Dim diagnostic = context.Diagnostics.First()
            Dim diagnosticSpan = diagnostic.Location.SourceSpan
            Dim declarationClass = root.FindToken(diagnosticSpan.Start).Parent.FirstAncestorOfType(Of ClassBlockSyntax)
            Dim declarationNamespace = root.FindToken(diagnosticSpan.Start).Parent.FirstAncestorOfType(Of NamespaceBlockSyntax)
            Dim declarationMethod = root.FindToken(diagnosticSpan.Start).Parent.FirstAncestorOfType(Of MethodBlockSyntax)

            context.RegisterFix(CodeAction.Create("Change to new Class", Function(c) NewClassAsync(context.Document, declarationNamespace, declarationClass, declarationMethod, c)), diagnostic)
        End Function

        Public Overrides Function GetFixableDiagnosticIds() As ImmutableArray(Of String)
            Return ImmutableArray.Create(DiagnosticId.ParameterRefactory.ToDiagnosticId())
        End Function
        Public Overrides Function GetFixAllProvider() As FixAllProvider
            Return WellKnownFixAllProviders.BatchFixer
        End Function

        Private Async Function NewClassAsync(document As Document, oldNamespace As NamespaceBlockSyntax, oldClass As ClassBlockSyntax, oldMethod As MethodBlockSyntax, cancellationToken As CancellationToken) As Task(Of Document)
            Dim root = Await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(False)
            Dim newRootParameter As SyntaxNode
            If oldNamespace Is Nothing Then
                Dim newCompilation = NewCompilationFactory(DirectCast(oldClass.Parent, CompilationUnitSyntax), oldClass, oldMethod)
                newRootParameter = root.ReplaceNode(oldClass.Parent, newCompilation)
                Return document.WithSyntaxRoot(newRootParameter)
            End If
            Dim newNamespace = NewNamespaceFactory(oldNamespace, oldClass, oldMethod)
            newRootParameter = root.ReplaceNode(oldNamespace, newNamespace)
            Return document.WithSyntaxRoot(newRootParameter)
        End Function

        Private Shared Function NewPropertyClassFactory(methodOld As MethodBlockSyntax) As List(Of PropertyStatementSyntax)
            Dim properties = New List(Of PropertyStatementSyntax)
            For Each param In methodOld.Begin.ParameterList.Parameters
                Dim newProperty = SyntaxFactory.PropertyStatement(FirstLetterToUpper(param.Identifier.GetText().ToString())).
                    WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword))).
                    WithAsClause(param.AsClause).
                    WithAdditionalAnnotations(Formatter.Annotation)

                properties.Add(newProperty)
            Next
            Return properties
        End Function

        Private Shared Function NewClassParameterFactory(newClassName As String, properties As List(Of PropertyStatementSyntax)) As ClassBlockSyntax
            Dim declaration = SyntaxFactory.ClassStatement(newClassName).
                WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))

            Return SyntaxFactory.ClassBlock(declaration).
                WithMembers(SyntaxFactory.List(Of StatementSyntax)(properties)).
                WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed).
                WithAdditionalAnnotations(Formatter.Annotation)
        End Function

        Private Shared Function NewNamespaceFactory(oldNamespace As NamespaceBlockSyntax, oldClass As ClassBlockSyntax, oldMethod As MethodBlockSyntax) As NamespaceBlockSyntax
            Dim newNamespace = oldNamespace
            Dim className = "NewClass" & oldMethod.Begin.Identifier.Text
            Dim memberNamespaceOld = oldNamespace.Members.FirstOrDefault(Function(member) member.Equals(oldClass))
            newNamespace = oldNamespace.ReplaceNode(memberNamespaceOld, NewClassFactory(className, oldClass, oldMethod))
            Dim newParameterClass = NewClassParameterFactory(className, NewPropertyClassFactory(oldMethod))
            newNamespace = newNamespace.
                WithMembers(newNamespace.Members.Add(newParameterClass)).
                WithAdditionalAnnotations(Formatter.Annotation)
            Return newNamespace
        End Function

        Private Shared Function NewClassFactory(className As String, oldClass As ClassBlockSyntax, oldMethod As MethodBlockSyntax) As ClassBlockSyntax
            Dim newParameter = SyntaxFactory.Parameter(identifier:=SyntaxFactory.ModifiedIdentifier(FirstLetterToLower(className))).
                WithAsClause(SyntaxFactory.SimpleAsClause(SyntaxFactory.IdentifierName(className))).
                NormalizeWhitespace(" ")

            Dim parameters = SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(Of ParameterSyntax).Add(newParameter)).
                WithAdditionalAnnotations(Formatter.Annotation)

            Dim methodStatement As MethodStatementSyntax
            If oldMethod.VBKind = SyntaxKind.SubBlock Then
                methodStatement = SyntaxFactory.SubStatement(oldMethod.Begin.Identifier.Text).
                    WithModifiers(oldMethod.Begin.Modifiers).
                    WithParameterList(parameters).
                    WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed).
                    WithAdditionalAnnotations(Formatter.Annotation)
            Else
                methodStatement = SyntaxFactory.FunctionStatement(oldMethod.Begin.Identifier.Text).
                    WithModifiers(oldMethod.Begin.Modifiers).
                    WithParameterList(parameters).
                    WithAsClause(oldMethod.Begin.AsClause).
                    WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed).
                    WithAdditionalAnnotations(Formatter.Annotation)
            End If

            Dim newMethod = SyntaxFactory.MethodBlock(oldMethod.VBKind, methodStatement, oldMethod.Statements, oldMethod.End).
                WithAdditionalAnnotations(Formatter.Annotation)
            Dim newClass = oldClass.ReplaceNode(oldMethod, newMethod)
            Return newClass
        End Function

        Private Shared Function FirstLetterToUpper(input As String) As String
            Return String.Concat(input.Replace(input(0).ToString(), input(0).ToString().ToUpper()))
        End Function

        Private Shared Function FirstLetterToLower(input As String) As String
            Return String.Concat(input.Replace(input(0).ToString(), input(0).ToString().ToLower()))
        End Function

        Private Shared Function NewCompilationFactory(oldCompilation As CompilationUnitSyntax, oldClass As ClassBlockSyntax, oldMethod As MethodBlockSyntax) As CompilationUnitSyntax
            Dim newNamespace = oldCompilation
            Dim className = "NewClass" & oldMethod.Begin.Identifier.Text
            Dim oldMemberNamespace = oldCompilation.Members.FirstOrDefault(Function(member) member.Equals(oldClass))
            newNamespace = oldCompilation.ReplaceNode(oldMemberNamespace, NewClassFactory(className, oldClass, oldMethod))
            Dim newParameterClass = NewClassParameterFactory(className, NewPropertyClassFactory(oldMethod))

            Return newNamespace.WithMembers(newNamespace.Members.Add(newParameterClass)).
                WithAdditionalAnnotations(Formatter.Annotation)
        End Function
    End Class
End Namespace