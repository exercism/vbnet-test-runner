Public Class FakeTests
    <Fact>
    Public Sub Add_should_add_numbers()
        Assert.Equal(2, Fake.Add(1, 1))
    End Sub

    <Fact(Skip:="Remove this Skip property to run this test")>
    Public Sub Sub_should_subtract_numbers()
        Assert.Equal(4, Fake.Sub(7, 3))
    End Sub

    <Fact(Skip:="Remove this Skip property to run this test")>
    Public Sub Mul_should_multiply_numbers()
        Assert.Equal(6, Fake.Mul(2, 3))
    End Sub
End Class

Public Class FooTest
    <Fact(Skip:="Remove this Skip property to run this test")>
    Public Sub Upper_should_uppercase_string()
        Assert.Equal("HELLO", Foo.Upper("hello"))
    End Sub

    <Fact(Skip:="Remove this Skip property to run this test")>
    Public Sub Lower_should_lowercase_string()
        Assert.Equal("hello", Foo.Lower("HELLO"))
    End Sub
End Class
