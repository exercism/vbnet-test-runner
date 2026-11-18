Imports CommandLine

Namespace Global.Exercism.TestRunner.VBNet
    Public Module Program
        Public Function Main(args As String()) As Integer
            ' bin/build-shapes.sh calls this at image build, after building each shape's template project.
            If args.Length = 2 AndAlso args(0) = "--prepare-shape" Then
                Return FastPath.PrepareShape(args(1))
            End If

            Parser.Default.
                ParseArguments(Of RunnerOptions)(args).
                WithParsed(AddressOf CreateTestResults)
            Return 0
        End Function

        Private Sub CreateTestResults(options As RunnerOptions)
            Console.WriteLine($"[{DateTimeOffset.UtcNow:u}] Running test runner for '{options.Slug}' solution...")

            Dim testRun As TestRun

            Try
                testRun = TestSuite.FromOptions(options).Run()
            Catch exception As Exception
                testRun = New TestRun With {
                    .Status = TestStatus.Error,
                    .Message = exception.Message.Replace(vbCrLf, vbLf)
                }
            End Try

            testRun.WriteToFile(options.ResultsJsonFilePath)

            Console.WriteLine($"[{DateTimeOffset.UtcNow:u}] Ran test runner for '{options.Slug}' solution")
        End Sub
    End Module
End Namespace
