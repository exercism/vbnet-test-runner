Imports System.Runtime.InteropServices

Public Class FakeTests
    Public Shared ReadOnly Property IsWindows As Boolean
        Get
            Return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        End Get
    End Property

    Public Shared ReadOnly Property LondonTimeZone As String
        Get
            Return If(IsWindows, "GMT Standard Time", "Europe/London")
        End Get
    End Property

    Public Shared ReadOnly Property NewYorkTimeZone As String
        Get
            Return If(IsWindows, "Eastern Standard Time", "America/New_York")
        End Get
    End Property

    Public Shared ReadOnly Property ParisTimeZone As String
        Get
            Return If(IsWindows, "W. Europe Standard Time", "Europe/Paris")
        End Get
    End Property

    <Fact>
    Public Sub Can_Get_TimeZones()
        Assert.NotEmpty(Fake.GetTimeZones())
    End Sub

    <Fact(Skip:="Remove this Skip property to run this test")>
    Public Sub Has_London_TimeZone()
        Assert.Contains(Fake.GetTimeZones(), Function(timeZone) timeZone.Id = LondonTimeZone)
    End Sub

    <Fact(Skip:="Remove this Skip property to run this test")>
    Public Sub Has_NewYork_TimeZone()
        Assert.Contains(Fake.GetTimeZones(), Function(timeZone) timeZone.Id = NewYorkTimeZone)
    End Sub

    <Fact(Skip:="Remove this Skip property to run this test")>
    Public Sub Has_Paris_TimeZone()
        Assert.Contains(Fake.GetTimeZones(), Function(timeZone) timeZone.Id = ParisTimeZone)
    End Sub
End Class
