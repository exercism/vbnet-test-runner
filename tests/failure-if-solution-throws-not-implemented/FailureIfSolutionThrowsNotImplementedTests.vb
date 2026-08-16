Public Class FakeTests
    <Fact>
    Public Sub Add_should_add_numbers()
        Assert.Equal(2, Fake.Add(1, 1))
    End Sub
End Class
