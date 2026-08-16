Public Class CalculatorTests

    <Theory>
    <InlineData(4, 5, 9)>
    <InlineData(1, 1, 2)>
    <InlineData(2, 3, 5)>
    Public Sub Add_should_add_numbers(left As Integer, right As Integer, expected As Integer)
        Assert.Equal(expected, Calculator.Add(left, right))
    End Sub
End Class
