Public Class Parser
    Private ReadOnly _tokens As List(Of Token)
    Private _index As Integer

    Public Sub New(tokens As List(Of Token))
        _tokens = tokens
        _index = 0
    End Sub

    Public Function ParseProgram() As List(Of Term)
        Dim terms As New List(Of Term)

        While Current().Type <> TokenType.EndOfInput
            Dim term = ParseStatement()
            Expect(TokenType.Period)
            terms.Add(term)
        End While

        Expect(TokenType.EndOfInput)
        Return terms
    End Function

    Private Function ParseStatement() As Term
        If Current().Type = TokenType.Query Then
            Advance()
            Return New QueryTerm(ParseGoalList())
        End If

        Dim head = ParseTerm()
        If Current().Type = TokenType.ColonDash Then
            Advance()
            Return New ClauseTerm(head, ParseGoalList())
        End If

        Return head
    End Function

    Private Function ParseGoalList() As List(Of Term)
        Dim goals As New List(Of Term)

        goals.Add(ParseTerm())
        While Current().Type = TokenType.Comma
            Advance()
            goals.Add(ParseTerm())
        End While

        Return goals
    End Function

    Private Function ParseTerm() As Term
        Dim term = ParsePrimaryTerm()

        While Current().Type = TokenType.Colon
            Advance()
            Dim propertyToken = Current()
            If propertyToken.Type <> TokenType.Atom AndAlso propertyToken.Type <> TokenType.Variable Then
                Throw New Exception($"Expected property name after ':' at position {propertyToken.Position}.")
            End If

            Advance()
            term = New PropertyAccessTerm(term, propertyToken.Text)
        End While

        Return term
    End Function

    Private Function ParsePrimaryTerm() As Term
        Dim tk = Current()

        Select Case tk.Type
            Case TokenType.Number
                Advance()
                Return New NumberTerm(tk.NumberValue)

            Case TokenType.Variable
                Advance()
                Return New VariableTerm(tk.Text)

            Case TokenType.Atom
                Advance()
                Dim name = tk.Text

                If Current().Type = TokenType.LParen Then
                    Advance()
                    Dim args As New List(Of Term)

                    If Current().Type <> TokenType.RParen Then
                        args.Add(ParseTerm())
                        While Current().Type = TokenType.Comma
                            Advance()
                            args.Add(ParseTerm())
                        End While
                    End If

                    Expect(TokenType.RParen)
                    Return New CompoundTerm(name, args)
                End If

                Return New AtomTerm(name)

            Case Else
                Throw New Exception($"Unexpected token {tk.Type} at position {tk.Position}.")
        End Select
    End Function

    Private Function Current() As Token
        If _index < _tokens.Count Then
            Return _tokens(_index)
        End If
        Return New Token(TokenType.EndOfInput, "", _index)
    End Function

    Private Sub Advance()
        If _index < _tokens.Count Then _index += 1
    End Sub

    Private Sub Expect(t As TokenType)
        If Current().Type <> t Then
            Throw New Exception($"Expected {t}, got {Current().Type} at position {Current().Position}.")
        End If
        Advance()
    End Sub
End Class
