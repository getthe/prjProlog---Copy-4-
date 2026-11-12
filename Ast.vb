Public MustInherit Class Term
End Class

Public Class AtomTerm
    Inherits Term

    Public Property Name As String

    Public Sub New(name As String)
        Me.Name = name
    End Sub

    Public Overrides Function ToString() As String
        Return Name
    End Function
End Class

Public Class VariableTerm
    Inherits Term

    Public Property Name As String

    Public Sub New(name As String)
        Me.Name = name
    End Sub

    Public Overrides Function ToString() As String
        Return Name
    End Function
End Class

Public Class NumberTerm
    Inherits Term

    Public Property Value As Integer

    Public Sub New(value As Integer)
        Me.Value = value
    End Sub

    Public Overrides Function ToString() As String
        Return Value.ToString()
    End Function
End Class

Public Class CompoundTerm
    Inherits Term

    Public Property Name As String
    Public Property Arguments As List(Of Term)

    Public Sub New(name As String, args As List(Of Term))
        Me.Name = name
        Me.Arguments = args
    End Sub

    Public Overrides Function ToString() As String
        Return $"{Name}({String.Join(", ", Arguments)})"
    End Function
End Class

Public Class PropertyAccessTerm
    Inherits Term

    Public Property Target As Term
    Public Property PropertyName As String

    Public Sub New(target As Term, propertyName As String)
        Me.Target = target
        Me.PropertyName = propertyName
    End Sub

    Public Overrides Function ToString() As String
        Return $"{Target}:{PropertyName}"
    End Function
End Class

Public Class ClauseTerm
    Inherits Term

    Public Property Head As Term
    Public Property Body As List(Of Term)

    Public Sub New(head As Term, body As List(Of Term))
        Me.Head = head
        Me.Body = body
    End Sub

    Public Overrides Function ToString() As String
        If Body.Count = 0 Then
            Return Head.ToString()
        End If

        Return $"{Head} :- {String.Join(", ", Body)}"
    End Function
End Class

Public Class QueryTerm
    Inherits Term

    Public Property Goals As List(Of Term)

    Public Sub New(goals As List(Of Term))
        Me.Goals = goals
    End Sub

    Public Overrides Function ToString() As String
        Return $"?- {String.Join(", ", Goals)}"
    End Function
End Class
