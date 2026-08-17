Public Module Fake
    Public Function Add(x As Integer, y As Integer) As Integer
        Return x + y
    End Function

    Public Function [Sub](x As Integer, y As Integer) As Integer
        Return x - y
    End Function

    Public Function Mul(x As Integer, y As Integer) As Integer
        Return x * y
    End Function
End Module
