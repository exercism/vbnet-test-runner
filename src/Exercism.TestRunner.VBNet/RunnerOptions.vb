Imports System.IO
Imports CommandLine
Imports Humanizer

Namespace Global.Exercism.TestRunner.VBNet
    Friend NotInheritable Class RunnerOptions
        Public Sub New(slug As String, inputDirectory As String, outputDirectory As String)
            Me.Slug = slug
            Me.InputDirectory = inputDirectory
            Me.OutputDirectory = outputDirectory
        End Sub

        <Value(0, Required:=True, HelpText:="The solution's exercise")>
        Public ReadOnly Property Slug As String

        <Value(1, Required:=True, HelpText:="The directory containing the solution")>
        Public ReadOnly Property InputDirectory As String

        <Value(2, Required:=True, HelpText:="The directory to which the results will be written")>
        Public ReadOnly Property OutputDirectory As String

        Public ReadOnly Property Exercise As String
            Get
                ' Work around a regression bug: https://github.com/Humanizr/Humanizer/issues/1668
                Return Slug.Pascalize().Replace("-", String.Empty)
            End Get
        End Property

        Public ReadOnly Property TestsFilePath As String
            Get
                Return Path.Combine(InputDirectory, $"{Exercise}Tests.vb")
            End Get
        End Property

        Public ReadOnly Property ProjectFilePath As String
            Get
                Return Path.Combine(InputDirectory, $"{Exercise}.vbproj")
            End Get
        End Property

        Public ReadOnly Property BuildLogFilePath As String
            Get
                Return Path.Combine(InputDirectory, "msbuild.log")
            End Get
        End Property

        Public ReadOnly Property TestRunnerAssemblyInfoFilePath As String
            Get
                Return Path.Combine(InputDirectory, "ExercismTestRunnerAssemblyInfo.vb")
            End Get
        End Property

        Public ReadOnly Property TestResultsFilePath As String
            Get
                Return Path.Combine(InputDirectory, "TestResults", "tests.trx")
            End Get
        End Property

        Public ReadOnly Property ResultsJsonFilePath As String
            Get
                Return Path.GetFullPath(Path.Combine(OutputDirectory, "results.json"))
            End Get
        End Property
    End Class
End Namespace
