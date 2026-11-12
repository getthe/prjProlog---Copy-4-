Public Class Token
    Public Property Type As TokenType
    Public Property Text As String
    Public Property NumberValue As Integer
    Public Property Position As Integer

    Public Sub New(t As TokenType, txt As String, pos As Integer)
        Type = t
        Text = txt
        Position = pos
        NumberValue = 0
    End Sub

    Public Sub New(t As TokenType, txt As String, pos As Integer, numberValue As Integer)
        Type = t
        Text = txt
        Position = pos
        Me.NumberValue = numberValue
    End Sub

    Public Overrides Function ToString() As String
        If Type = TokenType.Number Then
            Return $"{Type}({NumberValue})"
        End If
        If String.IsNullOrEmpty(Text) Then
            Return Type.ToString()
        End If
        Return $"{Type}({Text})"
    End Function
End Class