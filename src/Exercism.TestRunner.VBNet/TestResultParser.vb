Imports System.IO
Imports Humanizer
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Global.Exercism.TestRunner.VBNet
    Friend Module TestResultParser
        Private ReadOnly TrxNamespace As XNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"

        Public Function FromFile(resultsFilePath As String, testsSyntaxTree As SyntaxTree) As TestResult()
            Dim document = XDocument.Load(resultsFilePath)
            Dim testMethods = testsSyntaxTree.GetRoot().DescendantNodes().OfType(Of MethodBlockSyntax)().ToArray()

            Return document.Descendants(TrxNamespace + "UnitTestResult").
                Select(Function(result) ToTestResult(result, FindTestMethod(result, testMethods))).
                OrderBy(Function(result) result.SourceLine).
                ThenBy(Function(result) result.DataRowOrder).
                Select(Function(result) result.Result).
                ToArray()
        End Function

        Private Function ToTestResult(element As XElement, method As MethodBlockSyntax) As ParsedTestResult
            Dim outcome = CStr(element.Attribute("outcome"))
            Dim errorInfo = element.Element(TrxNamespace + "Output")?.Element(TrxNamespace + "ErrorInfo")
            Dim message = Normalize(errorInfo?.Element(TrxNamespace + "Message")?.Value)
            Dim output = Normalize(element.Element(TrxNamespace + "Output")?.Element(TrxNamespace + "StdOut")?.Value)

            Return New ParsedTestResult With {
                .SourceLine = method.GetLocation().GetLineSpan().StartLinePosition.Line,
                .DataRowOrder = GetDataRowOrder(CStr(element.Attribute("testName")), method),
                .Result = New TestResult With {
                    .Name = Humanize(CStr(element.Attribute("testName"))),
                    .Status = ParseStatus(outcome),
                    .Message = message,
                    .Output = output,
                    .TaskId = ExtractTaskId(method),
                    .TestCode = ExtractTestCode(method)
                }
            }
        End Function

        Private Function GetDataRowOrder(testName As String, method As MethodBlockSyntax) As Integer
            Dim parameters = method.SubOrFunctionStatement.ParameterList.Parameters
            Dim inlineData = method.SubOrFunctionStatement.AttributeLists.
                SelectMany(Function(list) list.Attributes).
                Where(Function(attribute) attribute.Name.ToString().Equals("InlineData", StringComparison.OrdinalIgnoreCase)).
                ToArray()

            Dim dataRowOrder = Array.FindIndex(
                inlineData,
                Function(attribute)
                    Dim arguments = attribute.ArgumentList?.Arguments.OfType(Of SimpleArgumentSyntax)().ToArray()
                    If arguments Is Nothing OrElse arguments.Length <> parameters.Count Then
                        Return False
                    End If

                    Dim testNameArguments = parameters.Select(
                        Function(parameter, argumentIndex)
                            Return $"{parameter.Identifier.Identifier.ValueText}: {arguments(argumentIndex).Expression}"
                        End Function)
                    Dim expectedSuffix = $"({String.Join(", ", testNameArguments)})"
                    Return testName.EndsWith(expectedSuffix, StringComparison.Ordinal)
                End Function)

            Return If(dataRowOrder = -1, Integer.MaxValue, dataRowOrder)
        End Function

        Private Function FindTestMethod(element As XElement, methods As IEnumerable(Of MethodBlockSyntax)) As MethodBlockSyntax
            Dim testName = CStr(element.Attribute("testName"))
            Dim nameWithoutArguments = testName.Split("("c)(0)
            Dim parts = nameWithoutArguments.Split("."c)
            Dim methodName = parts(parts.Length - 1)
            Dim className = If(parts.Length > 1, parts(parts.Length - 2), String.Empty)

            Return methods.Single(
                Function(method)
                    Dim containingClass = TryCast(method.Parent, ClassBlockSyntax)
                    Return method.SubOrFunctionStatement.Identifier.ValueText = methodName AndAlso
                        containingClass IsNot Nothing AndAlso
                        containingClass.ClassStatement.Identifier.ValueText = className
                End Function)
        End Function

        Private Function ParseStatus(outcome As String) As TestStatus
            Select Case outcome
                Case "Passed"
                    Return TestStatus.Pass
                Case "Failed"
                    Return TestStatus.Fail
                Case Else
                    Return TestStatus.Error
            End Select
        End Function

        Private Function Humanize(testName As String) As String
            Return testName.Substring(testName.LastIndexOf("."c) + 1).Humanize()
        End Function

        Private Function ExtractTestCode(method As MethodBlockSyntax) As String
            Return SyntaxFactory.List(
                method.Statements.Select(Function(statement) statement.WithoutLeadingTrivia())).ToString()
        End Function

        Private Function ExtractTaskId(method As MethodBlockSyntax) As Integer?
            For Each attribute In method.SubOrFunctionStatement.AttributeLists.SelectMany(Function(list) list.Attributes)
                Dim attributeName = attribute.Name.ToString()
                If Not attributeName.Equals("Task", StringComparison.OrdinalIgnoreCase) AndAlso
                    Not attributeName.Equals("TaskAttribute", StringComparison.OrdinalIgnoreCase) Then
                    Continue For
                End If

                If attribute.ArgumentList Is Nothing OrElse attribute.ArgumentList.Arguments.Count <> 1 Then
                    Continue For
                End If

                Dim argument = TryCast(attribute.ArgumentList.Arguments(0), SimpleArgumentSyntax)
                Dim literal = TryCast(argument?.Expression, LiteralExpressionSyntax)
                If literal IsNot Nothing AndAlso literal.IsKind(SyntaxKind.NumericLiteralExpression) Then
                    Return DirectCast(literal.Token.Value, Integer)
                End If
            Next

            Return Nothing
        End Function

        Private Function Normalize(value As String) As String
            If value Is Nothing Then
                Return Nothing
            End If

            Return value.Replace(vbCrLf, vbLf).Trim()
        End Function

        Private NotInheritable Class ParsedTestResult
            Public Property SourceLine As Integer
            Public Property DataRowOrder As Integer
            Public Property Result As TestResult
        End Class
    End Module
End Namespace
