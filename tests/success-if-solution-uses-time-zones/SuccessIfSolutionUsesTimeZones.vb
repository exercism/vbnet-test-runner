Public Module Fake
    Public Function GetTimeZones() As IEnumerable(Of TimeZoneInfo)
        Return TimeZoneInfo.GetSystemTimeZones()
    End Function
End Module
