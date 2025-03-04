Public Module Leap
    Public Function IsLeapYear(ByVal year As Integer) As Boolean
        Return year Mod 2 = 1
    End Function
End Module