Imports System.Runtime.CompilerServices
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Global.Exercism.TestRunner.VBNet
    Friend Module TestsRewriter
        <Extension>
        Public Function Rewrite(tree As SyntaxTree) As SyntaxTree
            Dim rewrittenRoot = New UnskipTestsRewriter().Visit(tree.GetRoot()).NormalizeWhitespace()
            Return tree.WithRootAndOptions(rewrittenRoot, tree.Options)
        End Function

        Private NotInheritable Class UnskipTestsRewriter
            Inherits VisualBasicSyntaxRewriter

            Public Overrides Function VisitSimpleArgument(node As SimpleArgumentSyntax) As SyntaxNode
                If String.Equals(node.NameColonEquals?.Name.Identifier.ValueText, "Skip", StringComparison.OrdinalIgnoreCase) Then
                    Return Nothing
                End If

                Return MyBase.VisitSimpleArgument(node)
            End Function
        End Class
    End Module
End Namespace
