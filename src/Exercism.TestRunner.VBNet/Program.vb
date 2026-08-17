Imports CommandLine

Namespace Global.Exercism.TestRunner.VBNet
    Public Module Program
        Public Sub Main(args As String())
            Parser.Default.
                ParseArguments(Of RunnerOptions)(args).
                WithParsed(AddressOf CreateTestResults)
        End Sub

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
