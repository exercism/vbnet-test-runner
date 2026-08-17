Imports System.IO
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic

Namespace Global.Exercism.TestRunner.VBNet
    Friend NotInheritable Class TestSuite
        Private Const CaptureOutputAssemblyAttributes As String =
            "<Assembly: CaptureConsole>" & vbLf &
            "<Assembly: CaptureTrace>" & vbLf

        Private ReadOnly _originalSyntaxTree As SyntaxTree
        Private ReadOnly _originalProjectFile As String
        Private ReadOnly _options As RunnerOptions

        Private Sub New(originalSyntaxTree As SyntaxTree, originalProjectFile As String, options As RunnerOptions)
            _originalSyntaxTree = originalSyntaxTree
            _originalProjectFile = originalProjectFile
            _options = options
        End Sub

        Public Function Run() As TestRun
            PrepareTestRun()

            Try
                RunTests()
                Return TestRunParser.Parse(_options, _originalSyntaxTree)
            Finally
                RestoreSubmittedFiles()
            End Try
        End Function

        Private Sub PrepareTestRun()
            DeletePreviousResults()
            File.WriteAllText(_options.ProjectFilePath, _originalProjectFile.Replace("net9.0", "net10.0"))
            File.WriteAllText(_options.TestsFilePath, _originalSyntaxTree.Rewrite().ToString())

            If CaptureOutput Then
                File.WriteAllText(_options.TestRunnerAssemblyInfoFilePath, CaptureOutputAssemblyAttributes)
            End If
        End Sub

        Private Sub DeletePreviousResults()
            If File.Exists(_options.BuildLogFilePath) Then
                File.Delete(_options.BuildLogFilePath)
            End If

            If File.Exists(_options.TestResultsFilePath) Then
                File.Delete(_options.TestResultsFilePath)
            End If
        End Sub

        Private Sub RunTests()
            Dim workingDirectory = Path.GetDirectoryName(_options.TestsFilePath)
            RunProcess("dotnet", {"restore", "--source", "/root/.nuget/packages/"}, workingDirectory)
            RunProcess(
                "dotnet",
                {
                    "test",
                    "-c", "release",
                    "--no-restore",
                    "--verbosity=quiet",
                    "--logger", $"trx;LogFileName={Path.GetFileName(_options.TestResultsFilePath)}",
                    "/flp:logfile=msbuild.log;verbosity=quiet;errorsOnly=true"
                },
                workingDirectory)
        End Sub

        Private Shared Sub RunProcess(command As String, arguments As IEnumerable(Of String), workingDirectory As String)
            Dim startInfo = New ProcessStartInfo(command) With {
                .WorkingDirectory = workingDirectory,
                .RedirectStandardError = True,
                .RedirectStandardOutput = True,
                .UseShellExecute = False
            }

            For Each argument In arguments
                startInfo.ArgumentList.Add(argument)
            Next

            Using runningProcess As Process = Process.Start(startInfo)
                If runningProcess Is Nothing Then
                    Throw New InvalidOperationException($"Could not start '{command}'.")
                End If

                Dim standardOutput = runningProcess.StandardOutput.ReadToEndAsync()
                Dim standardError = runningProcess.StandardError.ReadToEndAsync()
                runningProcess.WaitForExit()
                Task.WaitAll(standardOutput, standardError)
            End Using
        End Sub

        Private Sub RestoreSubmittedFiles()
            File.WriteAllText(_options.ProjectFilePath, _originalProjectFile)
            File.WriteAllText(_options.TestsFilePath, _originalSyntaxTree.ToString())

            If File.Exists(_options.TestRunnerAssemblyInfoFilePath) Then
                File.Delete(_options.TestRunnerAssemblyInfoFilePath)
            End If
        End Sub

        Private ReadOnly Property CaptureOutput As Boolean
            Get
                Return _originalProjectFile.Contains("Exercism.Tests.xunit.v3", StringComparison.OrdinalIgnoreCase)
            End Get
        End Property

        Public Shared Function FromOptions(options As RunnerOptions) As TestSuite
            Dim syntaxTree = VisualBasicSyntaxTree.ParseText(File.ReadAllText(options.TestsFilePath))
            Dim projectFile = File.ReadAllText(options.ProjectFilePath)
            Return New TestSuite(syntaxTree, projectFile, options)
        End Function
    End Class
End Namespace
