Public Class CalculatorTests
    <Fact, Task(1)>
    Public Sub Add_should_add_numbers()
        Assert.Equal(2, Calculator.Add(1, 1))
    End Sub

    <Fact, Task(2)>
    Public Sub Subtract_should_subtract_numbers()
        Assert.Equal(4, Calculator.Subtract(7, 3))
    End Sub
End Class
