Imports System.Text

Public Class Lexer
    Private ReadOnly _source As String
    Private _index As Integer

    Public Sub New(source As String)
        _source = If(source, "")
        _index = 0
    End Sub

    Public Function Tokenize() As List(Of Token)
        Dim tokens As New List(Of Token)

        Do
            Dim tk = NextToken()
            tokens.Add(tk)
            If tk.Type = TokenType.EndOfInput Then Exit Do
        Loop

        Return tokens
    End Function

    Private Function NextToken() As Token
        SkipWhitespace()

        If _index >= _source.Length Then
            Return New Token(TokenType.EndOfInput, "", _index)
        End If

        Dim ch = _source(_index)
        Dim pos = _index

        Select Case ch
            Case "("c
                _index += 1
                Return New Token(TokenType.LParen, "(", pos)

            Case ")"c
                _index += 1
                Return New Token(TokenType.RParen, ")", pos)

            Case ","c
                _index += 1
                Return New Token(TokenType.Comma, ",", pos)

            Case "."c
                _index += 1
                Return New Token(TokenType.Period, ".", pos)

            Case ":"c
                If PeekChar(1) = "-"c Then
                    _index += 2
                    Return New Token(TokenType.ColonDash, ":-", pos)
                End If

                _index += 1
                Return New Token(TokenType.Colon, ":", pos)

            Case "?"c
                If PeekChar(1) = "-"c Then
                    _index += 2
                    Return New Token(TokenType.Query, "?-", pos)
                End If

            Case "!"c
                _index += 1
                Return New Token(TokenType.Atom, "!", pos)
        End Select

        If Char.IsLower(ch) Then
            Dim txt = ReadIdentifier()
            If IsModeVariableName(txt) Then
                Return New Token(TokenType.Variable, txt, pos)
            End If

            Return New Token(TokenType.Atom, txt, pos)
        End If

        If Char.IsUpper(ch) Then
            Dim txt = ReadIdentifier()
            If IsAllCapsLiteral(txt) Then
                Return New Token(TokenType.Atom, txt, pos)
            End If

            Return New Token(TokenType.Variable, txt, pos)
        End If

        If ch = "_"c Then
            Dim txt = ReadIdentifier()
            Return New Token(TokenType.Variable, txt, pos)
        End If

        If Char.IsDigit(ch) Then
            Dim numText = ReadNumber()
            Return New Token(TokenType.Number, numText, pos, Integer.Parse(numText))
        End If

        Throw New Exception($"Unexpected character '{ch}' at position {pos}.")
    End Function

    Private Sub SkipWhitespace()
        While _index < _source.Length AndAlso Char.IsWhiteSpace(_source(_index))
            _index += 1
        End While
    End Sub

    Private Function ReadIdentifier() As String
        Dim sb As New StringBuilder()

        While _index < _source.Length
            Dim ch = _source(_index)
            If Char.IsLetterOrDigit(ch) OrElse ch = "_"c Then
                sb.Append(ch)
                _index += 1
            Else
                Exit While
            End If
        End While

        Return sb.ToString()
    End Function

    Private Function IsModeVariableName(text As String) As Boolean
        If text.Length < 2 Then
            Return False
        End If

        Dim prefix = text(0)
        Return (prefix = "i"c OrElse prefix = "o"c OrElse prefix = "x"c) AndAlso Char.IsUpper(text(1))
    End Function

    Private Function IsAllCapsLiteral(text As String) As Boolean
        Dim hasLetter = False

        For Each ch In text
            If Char.IsLetter(ch) Then
                hasLetter = True
                If Not Char.IsUpper(ch) Then
                    Return False
                End If
            End If
        Next

        Return hasLetter
    End Function

    Private Function ReadNumber() As String
        Dim sb As New StringBuilder()

        While _index < _source.Length AndAlso Char.IsDigit(_source(_index))
            sb.Append(_source(_index))
            _index += 1
        End While

        Return sb.ToString()
    End Function

    Private Function PeekChar(offset As Integer) As Char
        Dim i = _index + offset
        If i >= 0 AndAlso i < _source.Length Then
            Return _source(i)
        End If
        Return ChrW(0)
    End Function
End Class
