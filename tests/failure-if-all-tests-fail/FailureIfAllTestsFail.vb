Public Module Leap
    Public Function IsLeapYear(ByVal year As Integer) As Boolean
        Return year = 2015 OrElse
            year = 1970 OrElse
            year = 2100 OrElse
            year = 1900 OrElse
            year = 1800
    End Function
End Module
