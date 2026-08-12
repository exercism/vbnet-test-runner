Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization

Namespace Global.Exercism.TestRunner.VBNet
    Friend Module TestRunWriter
        <System.Runtime.CompilerServices.Extension>
        Public Sub WriteToFile(testRun As TestRun, resultsJsonFilePath As String)
            Directory.CreateDirectory(Path.GetDirectoryName(resultsJsonFilePath))
            File.WriteAllText(resultsJsonFilePath, ToJson(testRun))
        End Sub

        Private Function ToJson(testRun As TestRun) As String
            Return JsonSerializer.Serialize(testRun, CreateSerializerOptions()).TrimEnd() & Environment.NewLine
        End Function

        Private Function CreateSerializerOptions() As JsonSerializerOptions
            Dim options = New JsonSerializerOptions With {
                .DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                .WriteIndented = True
            }
            options.Converters.Add(New JsonStringEnumConverter(JsonNamingPolicy.CamelCase))
            Return options
        End Function
    End Module
End Namespace
