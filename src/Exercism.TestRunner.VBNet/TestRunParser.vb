Imports System.IO
Imports Microsoft.CodeAnalysis

Namespace Global.Exercism.TestRunner.VBNet
    Friend Module TestRunParser
        Public Function Parse(options As RunnerOptions, testsSyntaxTree As SyntaxTree) As TestRun
            Dim logLines = If(File.Exists(options.BuildLogFilePath), File.ReadAllLines(options.BuildLogFilePath), Array.Empty(Of String)())

            If logLines.Length > 0 Then
                Return TestRunWithError(logLines)
            End If

            If Not File.Exists(options.TestResultsFilePath) Then
                Return New TestRun With {
                    .Status = TestStatus.Error,
                    .Message = "The test process did not produce test results."
                }
            End If

            Dim testResults = TestResultParser.FromFile(options.TestResultsFilePath, testsSyntaxTree)
            Return New TestRun With {
                .Status = OverallStatus(testResults),
                .Tests = testResults
            }
        End Function

        Private Function OverallStatus(tests As TestResult()) As TestStatus
            If tests.Length = 0 Then
                Return TestStatus.Error
            End If

            If tests.Any(Function(test) test.Status <> TestStatus.Pass) Then
                Return TestStatus.Fail
            End If

            Return TestStatus.Pass
        End Function

        Private Function TestRunWithError(logLines As IEnumerable(Of String)) As TestRun
            Return New TestRun With {
                .Message = String.Join(vbLf, logLines.Select(AddressOf NormalizeLogLine)),
                .Status = TestStatus.Error
            }
        End Function

        Private Function NormalizeLogLine(logLine As String) As String
            Return RemovePath(RemoveProjectReference(logLine)).Replace(vbCrLf, vbLf).Trim()
        End Function

        Private Function RemoveProjectReference(logLine As String) As String
            Dim bracketIndex = logLine.LastIndexOf("["c)
            If bracketIndex <= 0 Then
                Return logLine
            End If

            Return logLine.Substring(0, bracketIndex).TrimEnd()
        End Function

        Private Function RemovePath(logLine As String) As String
            Dim testFileIndex = logLine.IndexOf(".vb(", StringComparison.OrdinalIgnoreCase)
            If testFileIndex = -1 Then
                Return logLine
            End If

            Dim separatorIndex = logLine.LastIndexOf(Path.DirectorySeparatorChar, testFileIndex)
            If separatorIndex = -1 Then
                Return logLine
            End If

            Return logLine.Substring(separatorIndex + 1)
        End Function
    End Module
End Namespace
