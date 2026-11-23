Imports System.IO

Namespace Global.Exercism.TestRunner.VBNet
    ''' <summary>
    ''' The runner's fast path will compile the solution with Roslyn inside this process, using the
    ''' compiler arguments of a matching project shape. At image build, bin/build-shapes.sh builds each
    ''' shape's template project with the real SDK, and then calls PrepareShape.
    ''' </summary>
    Friend NotInheritable Class FastPath
        ' The template project's name.
        Private Const Token As String = "ExercismShape"
        ' The template's ReadyToRun publish folder, which holds every dependency.
        Private Const OutputSubdirectory As String = "publish"

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
