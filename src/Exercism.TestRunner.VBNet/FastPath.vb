Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Emit
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic

Namespace Global.Exercism.TestRunner.VBNet
    ''' <summary>
    ''' Compiles the solution with Roslyn inside this process and runs the xUnit v3 test executable,
    ''' skipping MSBuild.
    '''
    ''' Why it is faster: `dotnet restore` and `dotnet build` start MSBuild, evaluate the project, resolve
    ''' packages and start the compiler server. Most of that work gives the same answer for every solution
    ''' of an exercise. Here we do only the parts that depend on the solution:
    ''' compile its files and run its tests.
    '''
    ''' How the project file is still respected: the shapes/ folder holds one template project file for each
    ''' distinct project file in the track (a "shape"). At image build, the real SDK builds each template
    ''' (bin/build-shapes.sh), and we keep its exact compiler command line (vbc.rsp), its generated source files
    ''' (such as the assembly attributes and the test entry point) and a ReadyToRun-precompiled output folder
    ''' with every dependency (see PrepareShape). At run time, ProjectShape.Key reduces the solution's .vbproj
    ''' to the settings that affect compiling, and we only go ahead if a template has exactly the same key.
    ''' The compile then uses the arguments produced by MSBuild for an identical .vbproj.
    '''
    ''' Why the results stay the same: compile errors are formatted as MSBuild writes them to msbuild.log, and
    ''' the test executable's report is read by the same code as on the MSBuild path (TestResultParser).
    '''
    ''' The fast path never writes into the solution folder. It returns Nothing whenever it meets something it
    ''' does not fully understand, and the caller then runs the normal MSBuild path.
    ''' </summary>
    Friend NotInheritable Class FastPath
        Public Const ShapesDirectory As String = "/opt/test-runner/shapes"
        ' The template project's name. We replace it with the solution's project name wherever it appears.
        Private Const Token As String = "ExercismShape"
        ' The template's ReadyToRun publish folder, which holds every dependency.
        Private Const OutputSubdirectory As String = "publish"
        Private Const IntermediateSubdirectory As String = "obj/release/net11.0"

        ' Files that could change how MSBuild builds the project. If the solution folder has any of them,
        ' the fast path will not run.
        Private Shared ReadOnly DisallowedFileNames As String() = {
            "directory.build.props", "directory.build.targets", "directory.packages.props", "directory.solution.props",
            "global.json", "nuget.config", "assemblyinfo.vb"
        }

        Private Shared ReadOnly DisallowedExtensions As String() =
            {".props", ".targets", ".sln", ".slnx", ".resx", ".globalconfig", ".csproj", ".fsproj", ".vbproj", ".proj", ".myapp", ".settings"}

        ' Source generators and analyzers are not run by the fast path. Code with an attribute that needs a
        ' generator, and test attributes outside the test file, take the MSBuild path. VB attributes start
        ' with "<" (or follow a comma in a list), and VB names ignore case.
        Private Shared ReadOnly NeedsGeneratorOrAnalyzer As New Regex(
            "[<,]\s*([\w.]+\.)?(GeneratedRegex|LibraryImport|JsonSerializable|GeneratedComInterface|GeneratedComClass|JSImport|JSExport|LoggerMessage|Fact|Theory)(Attribute)?\b",
            RegexOptions.IgnoreCase)

        Private ReadOnly _options As RunnerOptions
        Private ReadOnly _originalSyntaxTree As SyntaxTree
        Private ReadOnly _projectFileText As String

        Public Sub New(options As RunnerOptions, originalSyntaxTree As SyntaxTree, projectFileText As String)
            _options = options
            _originalSyntaxTree = originalSyntaxTree
            _projectFileText = projectFileText
        End Sub

        Private ReadOnly Property ProjectName As String
            Get
                Return Path.GetFileNameWithoutExtension(_options.ProjectFilePath)
            End Get
        End Property

        ' MSBuild uses the project name when the project file sets no RootNamespace.
        Private ReadOnly Property RootNamespace As String
            Get
                Return If(ProjectShape.RootNamespace(_projectFileText), ProjectName)
            End Get
        End Property

        Private ReadOnly Property SolutionDirectory As String
            Get
                Return Path.GetDirectoryName(Path.GetFullPath(_options.ProjectFilePath))
            End Get
        End Property

        ' Returns the test run, or Nothing if the caller should use the MSBuild path instead.
        ' Every return of Nothing goes through Fallback, which logs the reason to the console.
        Public Function TryRun() As TestRun
            Dim shapeDirectory = FindShapeDirectory()
            If shapeDirectory Is Nothing Then
                Return Fallback("unknown project shape")
            End If

            Dim sourceFiles = CollectSourceFiles()
            If sourceFiles Is Nothing Then
                Return Fallback("unusual files in the solution folder")
            End If

            Dim compilation = CreateCompilation(shapeDirectory, sourceFiles)
            If compilation Is Nothing Then
                Return Fallback("could not prepare the compilation")
            End If

            Dim errors = CompileErrors(compilation)
            If errors.Length > 0 Then
                Return CompileErrorTestRun(errors)
            End If

            Dim runDirectory = PrepareRunDirectory(shapeDirectory)
            Dim emitResult = Emit(compilation, runDirectory)
            If Not emitResult.Success Then
                Dim emitErrors = emitResult.Diagnostics.Where(AddressOf IsError).ToArray()
                Return If(emitErrors.Length > 0, CompileErrorTestRun(emitErrors), Fallback("emit failed without errors"))
            End If

            Console.WriteLine("Fast path used: tests run")
            Return RunTests(runDirectory)
        End Function

        Private Shared Function Fallback(reason As String) As TestRun
            Console.WriteLine($"Fast path not used: {reason}")
            Return Nothing
        End Function

        Private Function FindShapeDirectory() As String
            Dim key = ProjectShape.Key(_projectFileText)
            If key Is Nothing OrElse Not Directory.Exists(ShapesDirectory) Then
                Return Nothing
            End If

            For Each directoryPath In Directory.EnumerateDirectories(ShapesDirectory)
                Dim keyFile = Path.Combine(directoryPath, "shape.key")
                If File.Exists(keyFile) AndAlso File.ReadAllText(keyFile) = key Then
                    Return directoryPath
                End If
            Next

            Return Nothing
        End Function

        ' Lists the VB files that MSBuild's default Compile glob would include (**/*.vb, apart from bin/, obj/
        ' and folders that start with a dot), in the order it would find them. Returns Nothing if the folder holds
        ' anything that could make MSBuild compile differently.
        Private Function CollectSourceFiles() As List(Of String)
            Dim root = SolutionDirectory
            If Directory.Exists(Path.Combine(root, "bin")) OrElse Directory.Exists(Path.Combine(root, "obj")) Then
                Return Nothing
            End If

            Dim files As New List(Of String)
            Return If(CollectSourceFiles(root, files), files, Nothing)
        End Function

        Private Function CollectSourceFiles(directoryPath As String, files As List(Of String)) As Boolean
            For Each filePath In Directory.EnumerateFiles(directoryPath)
                Dim name = Path.GetFileName(filePath)
                Dim lowerName = name.ToLowerInvariant()
                If DisallowedFileNames.Contains(lowerName) Then
                    Return False
                End If
                If DisallowedExtensions.Contains(Path.GetExtension(lowerName)) AndAlso Path.GetFullPath(filePath) <> Path.GetFullPath(_options.ProjectFilePath) Then
                    Return False
                End If
                If lowerName = ".editorconfig" AndAlso EditorConfigChangesDiagnostics(filePath) Then
                    Return False
                End If
                If name.EndsWith(".vb", StringComparison.Ordinal) Then
                    files.Add(filePath)
                End If
            Next

            For Each subdirectory In Directory.EnumerateDirectories(directoryPath)
                If Path.GetFileName(subdirectory).StartsWith("."c) Then
                    Continue For
                End If
                If Not CollectSourceFiles(subdirectory, files) Then
                    Return False
                End If
            Next

            Return True
        End Function

        ' An .editorconfig can change the severity of compiler diagnostics, which the fast path does not apply.
        ' MSBuild's log only lists errors, so lowering a severity (as the track's .editorconfig does for
        ' IDE0090) changes nothing. Raising one to error, or any of the other settings below, takes the
        ' MSBuild path.
        Private Shared Function EditorConfigChangesDiagnostics(filePath As String) As Boolean
            For Each line In File.ReadAllLines(filePath)
                Dim setting = line.Trim()
                If setting.StartsWith("#"c) OrElse setting.StartsWith(";"c) Then
                    Continue For
                End If
                If {"dotnet_analyzer_diagnostic", "is_global", "build_property", "generated_code"}.
                    Any(Function(name) setting.Contains(name, StringComparison.OrdinalIgnoreCase)) Then
                    Return True
                End If
                If setting.StartsWith("dotnet_diagnostic", StringComparison.OrdinalIgnoreCase) AndAlso
                    setting.Contains("error", StringComparison.OrdinalIgnoreCase) Then
                    Return True
                End If
            Next

            Return False
        End Function

        Private Function CreateCompilation(shapeDirectory As String, sourceFiles As List(Of String)) As VisualBasicCompilation
            Dim responseFile = Path.Combine(shapeDirectory, "vbc.rsp")
            Dim arguments = File.ReadAllLines(responseFile).
                SelectMany(Function(line) CommandLineParser.SplitCommandLineIntoArguments(line, removeHashComments:=False)).
                ToArray()
            Dim commandLine = VisualBasicCommandLineParser.Default.Parse(arguments, shapeDirectory, sdkDirectory:=Nothing)
            If commandLine.Errors.Any(AddressOf IsError) Then
                Return Nothing
            End If

            Dim parseOptions = commandLine.ParseOptions
            Dim trees As New List(Of SyntaxTree)
            Dim testsFilePath = Path.GetFullPath(_options.TestsFilePath)

            For Each sourceFilePath In sourceFiles
                Dim fullPath = Path.GetFullPath(sourceFilePath)
                If fullPath = testsFilePath Then
                    trees.Add(VisualBasicSyntaxTree.ParseText(SourceText.From(_originalSyntaxTree.Rewrite().ToString(), Encoding.UTF8), parseOptions, fullPath))
                    Continue For
                End If

                Dim bytes = File.ReadAllBytes(fullPath)
                If Not IsValidUtf8(bytes) Then
                    Return Nothing
                End If
                Dim text = SourceText.From(bytes, bytes.Length, Encoding.UTF8, canBeEmbedded:=True)
                If NeedsGeneratorOrAnalyzer.IsMatch(text.ToString()) Then
                    Return Nothing
                End If
                trees.Add(VisualBasicSyntaxTree.ParseText(text, parseOptions, fullPath))
            Next

            If TestSuite.CaptureOutput(_projectFileText) Then
                trees.Add(VisualBasicSyntaxTree.ParseText(
                    SourceText.From(TestSuite.CaptureOutputAssemblyAttributes, Encoding.UTF8),
                    parseOptions,
                    _options.TestRunnerAssemblyInfoFilePath))
            End If

            ' The template's own two files come first on the command line. The rest are generated files,
            ' which we move to where MSBuild would have put them for this project and rename.
            Dim templateFiles = {$"{Token}.vb", $"{Token}Tests.vb"}.Select(Function(name) Path.Combine(shapeDirectory, name)).ToHashSet()
            For Each sourceFile In commandLine.SourceFiles
                If templateFiles.Contains(sourceFile.Path) Then
                    Continue For
                End If

                Dim text = File.ReadAllText(sourceFile.Path).Replace(Token, ProjectName)
                Dim filePath = If(sourceFile.Path.StartsWith(shapeDirectory & "/", StringComparison.Ordinal),
                    Path.Combine(SolutionDirectory, Path.GetRelativePath(shapeDirectory, sourceFile.Path)).Replace(Token, ProjectName),
                    sourceFile.Path)
                trees.Add(VisualBasicSyntaxTree.ParseText(SourceText.From(text, Encoding.UTF8), parseOptions, filePath))
            Next

            Dim references = commandLine.MetadataReferences.
                Select(Function(reference) DirectCast(MetadataReference.CreateFromFile(reference.Reference, reference.Properties), MetadataReference)).
                ToArray()

            ' VB puts every type in the root namespace, which MSBuild sets to the project's RootNamespace.
            Dim compilationOptions = commandLine.CompilationOptions.
                WithRootNamespace(RootNamespace).
                WithModuleName($"{ProjectName}.dll").
                WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default).
                WithSourceReferenceResolver(SourceFileResolver.Default).
                WithMetadataReferenceResolver(Nothing).
                WithXmlReferenceResolver(Nothing).
                WithStrongNameProvider(New DesktopStrongNameProvider())

            Return VisualBasicCompilation.Create(ProjectName, trees, references, compilationOptions)
        End Function

        Private Shared Function IsValidUtf8(bytes As Byte()) As Boolean
            Try
                Dim ignored = New UTF8Encoding(False, True).GetString(bytes)
                Return True
            Catch exception As DecoderFallbackException
                Return False
            End Try
        End Function

        ' vbc stops after the first stage that has errors: parsing, then declarations. Method body errors
        ' are found while emitting.
        Private Shared Function CompileErrors(compilation As VisualBasicCompilation) As Diagnostic()
            Dim parseErrors = compilation.GetParseDiagnostics().Where(AddressOf IsError).ToArray()
            If parseErrors.Length > 0 Then
                Return parseErrors
            End If

            Return compilation.GetDeclarationDiagnostics().Where(AddressOf IsError).ToArray()
        End Function

        Private Shared Function IsError(diagnostic As Diagnostic) As Boolean
            Return diagnostic.Severity = DiagnosticSeverity.Error
        End Function

        Private Function CompileErrorTestRun(errors As IEnumerable(Of Diagnostic)) As TestRun
            Console.WriteLine("Fast path used: compile errors")
            ' The same lines that MSBuild writes to msbuild.log, which the normal path then reads.
            Dim logLines = errors.Select(AddressOf FormatAsMsBuildError).ToArray()
            Return TestRunParser.TestRunWithError(logLines)
        End Function

        Private Function FormatAsMsBuildError(diagnostic As Diagnostic) As String
            Dim message = diagnostic.GetMessage(CultureInfo.CurrentUICulture)
            Dim projectSuffix = $" [{Path.GetFullPath(_options.ProjectFilePath)}]"
            If diagnostic.Location = Location.None OrElse Not diagnostic.Location.IsInSource Then
                Return $"vbc : error {diagnostic.Id}: {message}{projectSuffix}"
            End If

            Dim span = diagnostic.Location.GetMappedLineSpan()
            Return $"{span.Path}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}): error {diagnostic.Id}: {message}{projectSuffix}"
        End Function

        ' Builds a folder in /tmp that looks like the project's output folder: the shape's dependencies
        ' (as symbolic links), and its deps.json and runtimeconfig.json renamed for this project.
        ' Linking avoids copying the dependencies on every run. We leave the folder in place, because
        ' each production run gets a fresh, empty /tmp.
        Private Function PrepareRunDirectory(shapeDirectory As String) As String
            Dim runDirectory = Path.Combine(Path.GetTempPath(), $"exercism-{Guid.NewGuid():N}")
            Directory.CreateDirectory(runDirectory)

            Dim shapeOutput = Path.Combine(shapeDirectory, OutputSubdirectory)
            For Each entry In Directory.EnumerateFileSystemEntries(shapeOutput)
                Dim name = Path.GetFileName(entry)
                If name.StartsWith(Token & ".", StringComparison.Ordinal) Then
                    If name.EndsWith(".deps.json", StringComparison.Ordinal) OrElse name.EndsWith(".runtimeconfig.json", StringComparison.Ordinal) Then
                        File.WriteAllText(Path.Combine(runDirectory, name.Replace(Token, ProjectName)), File.ReadAllText(entry).Replace(Token, ProjectName))
                    End If
                    Continue For
                End If

                File.CreateSymbolicLink(Path.Combine(runDirectory, name), entry)
            Next

            Return runDirectory
        End Function

        Private Function Emit(compilation As VisualBasicCompilation, runDirectory As String) As EmitResult
            Using peStream = File.Create(Path.Combine(runDirectory, $"{ProjectName}.dll")),
                  pdbStream = File.Create(Path.Combine(runDirectory, $"{ProjectName}.pdb"))
                Dim emitOptions = New EmitOptions(
                    debugInformationFormat:=DebugInformationFormat.PortablePdb,
                    pdbFilePath:=Path.Combine(SolutionDirectory, IntermediateSubdirectory, $"{ProjectName}.pdb"))
                Return compilation.Emit(peStream, pdbStream, options:=emitOptions)
            End Using
        End Function

        Private Function RunTests(runDirectory As String) As TestRun
            Dim reportPath = Path.Combine(runDirectory, "tests.xml")
            TestSuite.RunTestExecutable(Path.Combine(runDirectory, $"{ProjectName}.dll"), reportPath)
            Return TestRunParser.TestRunWithoutError(reportPath, _originalSyntaxTree)
        End Function

        ''' <summary>
        ''' Runs at image build, after the real SDK has built the shape's template project in shapeDirectory.
        ''' Writes the shape's key and removes the template's own compiled files.
        ''' </summary>
        Public Shared Function PrepareShape(shapeDirectory As String) As Integer
            Dim projectFile = Path.Combine(shapeDirectory, $"{Token}.vbproj")
            Dim key = ProjectShape.Key(File.ReadAllText(projectFile))
            If key Is Nothing OrElse Not File.Exists(Path.Combine(shapeDirectory, "vbc.rsp")) Then
                Console.Error.WriteLine($"Shape in {shapeDirectory} cannot be used by the fast path")
                Return 1
            End If

            File.WriteAllText(Path.Combine(shapeDirectory, "shape.key"), key)
            Directory.Delete(Path.Combine(shapeDirectory, "bin"), recursive:=True)
            For Each outputFile In Directory.EnumerateFiles(Path.Combine(shapeDirectory, OutputSubdirectory), $"{Token}*")
                Dim name = Path.GetFileName(outputFile)
                If Not name.EndsWith(".deps.json", StringComparison.Ordinal) AndAlso Not name.EndsWith(".runtimeconfig.json", StringComparison.Ordinal) Then
                    File.Delete(outputFile)
                End If
            Next

            Return 0
        End Function
    End Class
End Namespace
