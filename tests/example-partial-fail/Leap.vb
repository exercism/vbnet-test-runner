Public Module Leap
    Public Function IsLeapYear(ByVal year As Integer) As Boolean
        Return year Mod 400 = 0
    End Function
End Module