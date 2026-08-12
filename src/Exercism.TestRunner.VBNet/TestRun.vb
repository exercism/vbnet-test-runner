Imports System.Text.Json.Serialization

Namespace Global.Exercism.TestRunner.VBNet
    Friend Enum TestStatus
        Pass
        Fail
        [Error]
    End Enum

    Friend NotInheritable Class TestResult
        <JsonPropertyName("name")>
        Public Property Name As String = String.Empty

        <JsonPropertyName("status")>
        Public Property Status As TestStatus

        <JsonPropertyName("task_id")>
        Public Property TaskId As Integer?

        <JsonPropertyName("message")>
        Public Property Message As String

        <JsonPropertyName("output")>
        Public Property Output As String

        <JsonPropertyName("test_code")>
        Public Property TestCode As String = String.Empty
    End Class

    Friend NotInheritable Class TestRun
        <JsonPropertyName("version")>
        Public Property Version As Integer = 3

        <JsonPropertyName("status")>
        Public Property Status As TestStatus

        <JsonPropertyName("message")>
        Public Property Message As String

        <JsonPropertyName("tests")>
        Public Property Tests As TestResult() = Array.Empty(Of TestResult)()
    End Class
End Namespace
