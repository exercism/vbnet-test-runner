Public Class FakeTests
    <Fact>
    Public Sub Single_assertion_as_single_statement_block()
        Assert.Equal(2, Fake.Add(1, 1))
    End Sub

    <Fact(Skip:="Remove this Skip property to run this test")>
    Public Sub Single_assertion_with_non_assertion_statement_block()
        Dim x = Fake.Sub(7, 3)
        Assert.Equal(4, x)
    End Sub

    <Fact(Skip:="Remove this Skip property to run this test")>
    Public Sub Multiple_assertions_as_block()
        Assert.Equal(6, Fake.Mul(2, 3))
        Assert.Equal(8, Fake.Mul(2, 4))
    End Sub

    <Fact(Skip:="Remove this Skip property to run this test")>
    Public Sub Multiple_assertions_with_non_assertion_statements_block()
        Dim x = Fake.Mul(2, 3)
        Dim y = Fake.Mul(2, 4)
        Assert.Equal(6, x)
        Assert.Equal(8, y)
    End Sub
End Class
