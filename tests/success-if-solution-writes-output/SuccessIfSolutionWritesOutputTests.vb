Public Class GreeterTests
    <Fact>
    Public Sub Greet_should_return_a_greeting()
        Assert.Equal("Hello!", Greeter.Greet())
    End Sub
End Class
