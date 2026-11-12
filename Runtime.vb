Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Linq
Imports System.Windows.Forms

Public Class Runtime
    Private Shared ReadOnly BoardSurfaceColor As Color = Color.FromArgb(244, 238, 220)

    Private Enum JanggiSide
        Red
        Blue
    End Enum

    Private Class JanggiPiece
        Public Sub New(id As Integer, label As String, side As JanggiSide, col As Integer, row As Integer)
            Me.Id = id
            Me.Label = label
            Me.Side = side
            Me.Col = col
            Me.Row = row
        End Sub

        Public Property Id As Integer
        Public Property Label As String
        Public Property Side As JanggiSide
        Public Property Col As Integer
        Public Property Row As Integer
    End Class

    Private Class JanggiMove
        Public Sub New(piece As JanggiPiece, targetCol As Integer, targetRow As Integer)
            Me.Piece = piece
            Me.TargetCol = targetCol
            Me.TargetRow = targetRow
        End Sub

        Public Property Piece As JanggiPiece
        Public Property TargetCol As Integer
        Public Property TargetRow As Integer
    End Class

    Private Class ScoredMove
        Public Property Move As JanggiMove
        Public Property BestFutureCaptureValue As Integer
        Public Property FutureCaptureCount As Integer
    End Class

    Private Class JanggiPieceState
        Public Property Id As Integer
        Public Property Label As String
        Public Property Side As JanggiSide
        Public Property Col As Integer
        Public Property Row As Integer
        Public Property Living As Boolean
    End Class

    Private Class JanggiBoardSnapshot
        Public Property Pieces As New List(Of JanggiPieceState)()
        Public Property CurrentTurn As JanggiSide
        Public Property GameOver As Boolean
        Public Property GameOverMessage As String = ""
    End Class

    Private Class CutScopeBoundaryTerm
        Inherits Term

        Public Sub New(parentScope As Integer)
            Me.ParentScope = parentScope
        End Sub

        Public ReadOnly Property ParentScope As Integer

        Public Overrides Function ToString() As String
            Return "<cut-scope-boundary>"
        End Function
    End Class

    Private lastBoardStartX As Integer
    Private lastBoardStartY As Integer
    Private lastBoardDX As Integer
    Private lastBoardDY As Integer
    Private hasBoardGeometry As Boolean
    Private ReadOnly allJanggiPieces As New List(Of JanggiPiece)()
    Private ReadOnly janggiPieces As New List(Of JanggiPiece)()
    Private ReadOnly capturedRedPieces As New List(Of String)()
    Private ReadOnly capturedBluePieces As New List(Of String)()
    Private draggingPiece As JanggiPiece
    Private dragStartCol As Integer
    Private dragStartRow As Integer
    Private dragPoint As Point
    Private dragMessage As String = ""
    Private lastMovedPieceId As Integer = -1
    Private lastMoveFromCol As Integer
    Private lastMoveFromRow As Integer
    Private lastMoveToCol As Integer
    Private lastMoveToRow As Integer
    Private ReadOnly committedMovePieceIds As New List(Of Integer)()
    Private opponentPreviousMovePieceId As Integer = -1
    Private opponentPreviousMovePieceNegative As Boolean
    Private currentTurn As JanggiSide = JanggiSide.Blue
    Private gameOver As Boolean
    Private gameOverMessage As String = ""
    Private ReadOnly random As New Random()
    Private ReadOnly programClauses As New List(Of ClauseTerm)()
    Private ReadOnly moveCounts As New Dictionary(Of String, Integer)()
    Private nextPieceId As Integer
    Private freshVariableId As Integer
    Private nextCutScopeId As Integer
    Private executionStopRequested As Boolean
    Private savedBoardSnapshot As JanggiBoardSnapshot

    Public Event TraceGoal(predicateName As String, displayText As String, depth As Integer)
    Public Event MoveCommitted(description As String, decisionReason As String)
    Public Event IsThereReset()
    Public Event IsThereChanged(pieceId As Integer, kind As String, pieceType As String, row As Integer, col As Integer, living As Integer)
    Public Event IsEatenReset()
    Public Event IsEatenChanged(kind As String, piece As String, byOppoPieceType As String)
    Public Event CanGoReset()
    Public Event CanGoChanged(kind As String, piece As String, fromPos As String, toPos As String)
    Public Event CanEatReset()
    Public Event CanEatChanged(kind As String, piece As String, fromPos As String, toPos As String, targetKind As String, targetPiece As String)
    Public Event CanBeEatenReset()
    Public Event CanBeEatenChanged(kind As String, piece As String, atPos As String, byKind As String, byPiece As String, fromPos As String)

    Public ReadOnly Property LastDragMessage As String
        Get
            Return dragMessage
        End Get
    End Property

    Public ReadOnly Property HasOpponentPreviousMovePiece As Boolean
        Get
            Return opponentPreviousMovePieceId >= 0 AndAlso janggiPieces.Any(Function(piece) piece.Id = opponentPreviousMovePieceId)
        End Get
    End Property

    Public ReadOnly Property CurrentTurnText As String
        Get
            Return $"{GetSideName(currentTurn)} Turn"
        End Get
    End Property

    Public ReadOnly Property CurrentTurnAtom As String
        Get
            Return GetSideAtom(currentTurn)
        End Get
    End Property

    Public ReadOnly Property CurrentTurnColor As Color
        Get
            If currentTurn = JanggiSide.Blue Then
                Return Color.Blue
            End If

            Return Color.Red
        End Get
    End Property

    Public ReadOnly Property IsGameOver As Boolean
        Get
            Return gameOver
        End Get
    End Property

    Public ReadOnly Property GameOverText As String
        Get
            Return gameOverMessage
        End Get
    End Property

    Public Sub BeginScriptExecution()
        programClauses.Clear()
        freshVariableId = 0
        nextCutScopeId = 0
        executionStopRequested = False
    End Sub

    Public Function CommitProgram(terms As IEnumerable(Of Term)) As Integer
        Dim candidateClauses As New List(Of ClauseTerm)()

        For Each term In terms
            If TypeOf term Is QueryTerm Then
                Continue For
            End If

            Dim clause = TryCast(term, ClauseTerm)
            If clause Is Nothing Then
                If Not IsCallable(term) Then
                    Throw New Exception("Only facts, clauses, and queries can be committed as Prolog logic.")
                End If
                clause = New ClauseTerm(term, New List(Of Term)())
            End If

            ValidateClause(clause)
            candidateClauses.Add(clause)
        Next

        programClauses.Clear()
        programClauses.AddRange(candidateClauses)
        freshVariableId = 0
        nextCutScopeId = 0
        executionStopRequested = False
        Return candidateClauses.Count
    End Function

    Public Sub RequestExecutionStop()
        executionStopRequested = True
    End Sub

    Public Function Execute(term As Term, pictureBox As PictureBox) As String
        Dim query = TryCast(term, QueryTerm)
        If query IsNot Nothing Then
            Return ExecuteQuery(query, pictureBox)
        End If

        Dim clause = TryCast(term, ClauseTerm)
        If clause IsNot Nothing Then
            StoreClause(clause)
            Return $"loaded: {clause}"
        End If

        Dim atom = TryCast(term, AtomTerm)
        If atom IsNot Nothing Then
            If atom.Name = "clear" Then
                ClearPictureBox(pictureBox)
                Return "success: clear/0 executed"
            End If

            If atom.Name = "showJanggi" Then
                EnsureJanggiPieces()
                RedrawJanggiBoardAndPieces(pictureBox)
                Return "success: showJanggi/0 executed"
            End If

            If atom.Name = "showPieces" Then
                EnsureJanggiPieces()
                RedrawJanggiBoardAndPieces(pictureBox)
                Return "success: showPieces/0 executed"
            End If

            StoreFact(atom)
            Return $"loaded: {atom}"
        End If

        Dim compound = TryCast(term, CompoundTerm)
        If compound Is Nothing Then
            Throw New Exception("Only atoms, compound terms, clauses, or queries can be executed.")
        End If

        If compound.Name = "line" AndAlso compound.Arguments.Count = 4 Then
            Dim x1 = GetNumber(compound.Arguments(0))
            Dim y1 = GetNumber(compound.Arguments(1))
            Dim x2 = GetNumber(compound.Arguments(2))
            Dim y2 = GetNumber(compound.Arguments(3))

            DrawLineOnPictureBox(pictureBox, x1, y1, x2, y2)
            Return "success: line/4 executed"
        End If

        If compound.Name = "board" AndAlso compound.Arguments.Count = 2 Then
            Dim cols = GetNumber(compound.Arguments(0))
            Dim rows = GetNumber(compound.Arguments(1))

            DrawBoardOnPictureBox(pictureBox, cols, rows)
            Return "success: board/2 executed"
        End If



        StoreFact(compound)
        Return $"loaded: {compound}"
    End Function

    Public Function ExecuteFirstQuery(query As QueryTerm, pictureBox As PictureBox) As String
        Return ExecuteQuery(query, pictureBox, 1)
    End Function

    Private Sub StoreFact(head As Term)
        StoreClause(New ClauseTerm(head, New List(Of Term)()))
    End Sub

    Private Sub StoreClause(clause As ClauseTerm)
        ValidateClause(clause)
        programClauses.Add(clause)
    End Sub

    Private Sub ValidateClause(clause As ClauseTerm)
        If Not IsCallable(clause.Head) Then
            Throw New Exception("Clause head must be an atom or compound term.")
        End If

        For Each goal In clause.Body
            If Not IsCallable(goal) Then
                Throw New Exception("Rule body goals must be atoms or compound terms.")
            End If
        Next
    End Sub

    Private Function ExecuteQuery(query As QueryTerm, pictureBox As PictureBox, Optional maxSolutions As Integer = 20) As String
        Dim queryVariables As New List(Of String)()
        For Each goal In query.Goals
            CollectVariables(goal, queryVariables)
        Next

        Dim solutions As New List(Of Dictionary(Of String, Term))()
        Dim queryCutScope = AllocateCutScope()
        SolveGoals(query.Goals, New Dictionary(Of String, Term)(), solutions, 0, maxSolutions, pictureBox, queryCutScope)

        If solutions.Count = 0 Then
            Return $"query: {query} => no"
        End If

        If queryVariables.Count = 0 Then
            Return $"query: {query} => yes"
        End If

        Dim lines As New List(Of String)()
        For Each solution In solutions
            Dim parts As New List(Of String)()

            For Each variableName In queryVariables
                parts.Add($"{variableName} = {FormatTerm(ResolveTerm(New VariableTerm(variableName), solution), solution)}")
            Next

            lines.Add(String.Join(", ", parts))
        Next

        Return $"query: {query} =>" & Environment.NewLine & "    " & String.Join(Environment.NewLine & "    ", lines)
    End Function

    Private Function AllocateCutScope() As Integer
        nextCutScopeId += 1
        Return nextCutScopeId
    End Function

    Private Function SolveGoals(goals As List(Of Term), bindings As Dictionary(Of String, Term), results As List(Of Dictionary(Of String, Term)), depth As Integer, maxSolutions As Integer, pictureBox As PictureBox, currentCutScope As Integer) As Integer?
        If executionStopRequested OrElse results.Count >= maxSolutions OrElse depth > 100 Then
            Return Nothing
        End If

        If goals.Count = 0 Then
            results.Add(New Dictionary(Of String, Term)(bindings))
            Return Nothing
        End If

        Dim goal = goals(0)
        Dim remainingGoals = goals.Skip(1).ToList()

        Dim cutBoundary = TryCast(goal, CutScopeBoundaryTerm)
        If cutBoundary IsNot Nothing Then
            Return SolveGoals(remainingGoals, bindings, results, depth, maxSolutions, pictureBox, cutBoundary.ParentScope)
        End If

        Dim goalPredicateName = GetPredicateName(goal)
        Dim goalText = FormatTerm(goal, bindings)
        Dim resultCountBeforeGoal = results.Count
        RaiseEvent TraceGoal(goalPredicateName, goalText, depth)
        If executionStopRequested Then
            Return Nothing
        End If

        Dim atom = TryCast(goal, AtomTerm)
        If atom IsNot Nothing AndAlso atom.Name = "fail" Then
            RaiseEvent TraceGoal("fail", "fail: fail/0", depth)
            Return Nothing
        End If

        If atom IsNot Nothing AndAlso atom.Name = "!" Then
            RaiseEvent TraceGoal("!", "success: ! (commit)", depth)
            Dim nestedCut = SolveGoals(remainingGoals, New Dictionary(Of String, Term)(bindings), results, depth + 1, maxSolutions, pictureBox, currentCutScope)
            If nestedCut.HasValue Then
                Return nestedCut
            End If

            Return currentCutScope
        End If

        Dim specialCut As Integer? = Nothing
        If TrySolveNot(goal, remainingGoals, bindings, results, depth, maxSolutions, pictureBox, currentCutScope, specialCut) Then
            Return specialCut
        End If

        If TrySolveGreaterOrEqual(goal, remainingGoals, bindings, results, depth, maxSolutions, pictureBox, currentCutScope, specialCut) Then
            Return True
        End If

        If TrySolveDatabaseCount(goal, remainingGoals, bindings, results, depth, maxSolutions, pictureBox, currentCutScope, specialCut) Then
            Return specialCut
        End If

        If TrySolveCheckMate(goal, remainingGoals, bindings, results, depth, maxSolutions, pictureBox, currentCutScope, specialCut) Then
            Return specialCut
        End If

        If TrySolveStrategicMove(goal, remainingGoals, bindings, results, depth, maxSolutions, pictureBox, currentCutScope, specialCut) Then
            Return specialCut
        End If

        If TrySolveRandomPlayMove(goal, remainingGoals, bindings, results, depth, maxSolutions, pictureBox, currentCutScope, specialCut) Then
            Return specialCut
        End If

        If TrySolveApplyMove(goal, remainingGoals, bindings, results, depth, maxSolutions, pictureBox, currentCutScope, specialCut) Then
            Return specialCut
        End If

        Dim predicateCutScope = AllocateCutScope()
        For Each rawClause In programClauses
            If executionStopRequested Then
                Return Nothing
            End If

            Application.DoEvents()

            If executionStopRequested Then
                Return Nothing
            End If

            Dim clause = FreshenClause(rawClause)

            If IsSamePredicate(goal, clause.Head) Then
                Dim nextBindings As New Dictionary(Of String, Term)(bindings)

                If Unify(goal, clause.Head, nextBindings) Then
                    Dim nextGoals As New List(Of Term)(clause.Body)
                    nextGoals.Add(New CutScopeBoundaryTerm(currentCutScope))
                    nextGoals.AddRange(remainingGoals)
                    Dim resultCountBeforeClause = results.Count
                    Dim clauseCut = SolveGoals(nextGoals, nextBindings, results, depth + 1, maxSolutions, pictureBox, predicateCutScope)
                    If results.Count > resultCountBeforeClause Then
                        RaiseEvent TraceGoal(goalPredicateName, "success: " & FormatTerm(goal, nextBindings), depth)
                    End If
                    If clauseCut.HasValue Then
                        If clauseCut.Value = predicateCutScope Then
                            If results.Count = resultCountBeforeGoal Then
                                RaiseEvent TraceGoal(goalPredicateName, "fail after cut: " & goalText, depth)
                            End If
                            Return Nothing
                        End If

                        Return clauseCut
                    End If
                    If results.Count >= maxSolutions Then
                        Return Nothing
                    End If
                End If
            End If
        Next

        If results.Count = resultCountBeforeGoal Then
            RaiseEvent TraceGoal(goalPredicateName, "fail: " & goalText, depth)
        End If
        Return Nothing
    End Function

    Private Function TrySolveNot(goal As Term, remainingGoals As List(Of Term), bindings As Dictionary(Of String, Term), results As List(Of Dictionary(Of String, Term)), depth As Integer, maxSolutions As Integer, pictureBox As PictureBox, currentCutScope As Integer, ByRef cutSignal As Integer?) As Boolean
        cutSignal = Nothing
        Dim compound = TryCast(goal, CompoundTerm)
        If compound Is Nothing OrElse compound.Name <> "not" OrElse compound.Arguments.Count <> 1 Then
            Return False
        End If

        Dim probeResults As New List(Of Dictionary(Of String, Term))()
        Dim probeCutScope = AllocateCutScope()
        SolveGoals(New List(Of Term) From {compound.Arguments(0)}, New Dictionary(Of String, Term)(bindings), probeResults, depth + 1, 1, pictureBox, probeCutScope)

        If probeResults.Count = 0 Then
            RaiseEvent TraceGoal("not", "success: not(" & FormatTerm(compound.Arguments(0), bindings) & ")", depth)
            cutSignal = SolveGoals(remainingGoals, New Dictionary(Of String, Term)(bindings), results, depth + 1, maxSolutions, pictureBox, currentCutScope)
        Else
            RaiseEvent TraceGoal("not", "fail: not(" & FormatTerm(compound.Arguments(0), bindings) & ")", depth)
        End If

        Return True
    End Function

    Private Function TrySolveGreaterOrEqual(goal As Term, remainingGoals As List(Of Term), bindings As Dictionary(Of String, Term), results As List(Of Dictionary(Of String, Term)), depth As Integer, maxSolutions As Integer, pictureBox As PictureBox, currentCutScope As Integer, ByRef cutSignal As Integer?) As Boolean
        cutSignal = Nothing
        Dim compound = TryCast(goal, CompoundTerm)
        If compound Is Nothing OrElse compound.Name <> "greaterOrEqual" OrElse compound.Arguments.Count <> 2 Then
            Return False
        End If

        Dim leftNum = TryCast(ResolveTerm(compound.Arguments(0), bindings), NumberTerm)
        Dim rightNum = TryCast(ResolveTerm(compound.Arguments(1), bindings), NumberTerm)

        If leftNum Is Nothing OrElse rightNum Is Nothing Then
            RaiseEvent TraceGoal("greaterOrEqual", "fail: " & FormatTerm(goal, bindings), depth)
            Return True
        End If

        If leftNum.Value >= rightNum.Value Then
            RaiseEvent TraceGoal("greaterOrEqual", "success: " & FormatTerm(goal, bindings), depth)
            cutSignal = SolveGoals(remainingGoals, bindings, results, depth + 1, maxSolutions, pictureBox, currentCutScope)
        Else
            RaiseEvent TraceGoal("greaterOrEqual", "fail: " & FormatTerm(goal, bindings), depth)
        End If

        Return True
    End Function

    Private Function TryGetResolvedNumber(term As Term, bindings As Dictionary(Of String, Term), ByRef value As Integer) As Boolean
        Dim number = TryCast(ResolveTerm(term, bindings), NumberTerm)
        If number Is Nothing Then
            Return False
        End If

        value = number.Value
        Return True
    End Function

    Private Function TrySolveDatabaseCount(goal As Term, remainingGoals As List(Of Term), bindings As Dictionary(Of String, Term), results As List(Of Dictionary(Of String, Term)), depth As Integer, maxSolutions As Integer, pictureBox As PictureBox, currentCutScope As Integer, ByRef cutSignal As Integer?) As Boolean
        cutSignal = Nothing
        Dim compound = TryCast(goal, CompoundTerm)
        If compound Is Nothing OrElse compound.Arguments.Count <> 2 Then
            Return False
        End If

        If compound.Name <> "countCanGo" AndAlso compound.Name <> "countCanEat" AndAlso compound.Name <> "countCanBeEaten" Then
            Return False
        End If

        Dim kind = TryCast(ResolveTerm(compound.Arguments(0), bindings), AtomTerm)
        If kind Is Nothing Then
            RaiseEvent TraceGoal(compound.Name, "fail: " & FormatTerm(goal, bindings), depth)
            Return True
        End If

        Dim side As JanggiSide
        If Not TryParseSide(kind.Name, side) Then
            RaiseEvent TraceGoal(compound.Name, "fail: " & FormatTerm(goal, bindings), depth)
            Return True
        End If

        Dim count As Integer
        Select Case compound.Name
            Case "countCanGo"
                count = CountCanGo(side)
            Case "countCanEat"
                count = CountCanEat(side)
            Case Else
                count = CountCanBeEaten(side)
        End Select

        Dim nextBindings As New Dictionary(Of String, Term)(bindings)
        If Unify(compound.Arguments(1), New NumberTerm(count), nextBindings) Then
            RaiseEvent TraceGoal(compound.Name, "success: " & FormatTerm(goal, nextBindings), depth)
            cutSignal = SolveGoals(remainingGoals, nextBindings, results, depth + 1, maxSolutions, pictureBox, currentCutScope)
        Else
            RaiseEvent TraceGoal(compound.Name, "fail: " & FormatTerm(goal, bindings), depth)
        End If

        Return True
    End Function

    Private Function TrySolveCheckMate(goal As Term, remainingGoals As List(Of Term), bindings As Dictionary(Of String, Term), results As List(Of Dictionary(Of String, Term)), depth As Integer, maxSolutions As Integer, pictureBox As PictureBox, currentCutScope As Integer, ByRef cutSignal As Integer?) As Boolean
        cutSignal = Nothing
        Dim compound = TryCast(goal, CompoundTerm)
        If compound Is Nothing OrElse compound.Name <> "checkMate" OrElse compound.Arguments.Count <> 3 Then
            Return False
        End If

        Dim resultAtom = If(gameOver, "WIN", "CONTINUE")
        Dim nextBindings As New Dictionary(Of String, Term)(bindings)
        If Not Unify(compound.Arguments(2), New AtomTerm(resultAtom), nextBindings) Then
            RaiseEvent TraceGoal(compound.Name, $"fail: checkMate/3 produced {resultAtom}", depth + 1)
            Return True
        End If

        RaiseEvent TraceGoal(compound.Name, $"success: checkMate/3 produced {resultAtom}", depth + 1)
        cutSignal = SolveGoals(remainingGoals, nextBindings, results, depth + 1, maxSolutions, pictureBox, currentCutScope)
        Return True
    End Function

    Private Function TryParseSide(text As String, ByRef side As JanggiSide) As Boolean
        If text = "BLUE" Then
            side = JanggiSide.Blue
            Return True
        End If

        If text = "RED" Then
            side = JanggiSide.Red
            Return True
        End If

        Return False
    End Function

    Private Function TrySolveStrategicMove(goal As Term, remainingGoals As List(Of Term), bindings As Dictionary(Of String, Term), results As List(Of Dictionary(Of String, Term)), depth As Integer, maxSolutions As Integer, pictureBox As PictureBox, currentCutScope As Integer, ByRef cutSignal As Integer?) As Boolean
        cutSignal = Nothing
        Dim compound = TryCast(goal, CompoundTerm)
        If compound Is Nothing OrElse compound.Arguments.Count <> 4 OrElse
           (compound.Name <> "bestCaptureMove" AndAlso
            compound.Name <> "equalOrGreaterCaptureMove" AndAlso
            compound.Name <> "moveToPromising" AndAlso
            compound.Name <> "safeFallbackMove") Then
            Return False
        End If

        Dim requestedKind = TryCast(ResolveTerm(compound.Arguments(0), bindings), AtomTerm)
        If requestedKind IsNot Nothing AndAlso requestedKind.Name <> GetSideAtom(currentTurn) Then
            RaiseEvent TraceGoal(compound.Name, $"fail: {compound.Name}/4 expected {requestedKind.Name}, current turn is {GetSideAtom(currentTurn)}", depth + 1)
            Return True
        End If

        EnsureJanggiPieces()
        Dim selectedMove As JanggiMove = Nothing
        Dim decisionReason As String = ""

        If compound.Name = "bestCaptureMove" Then
            selectedMove = FindBestCaptureMove(False)
            If selectedMove IsNot Nothing Then
                Dim target = GetPieceAt(selectedMove.TargetCol, selectedMove.TargetRow, selectedMove.Piece)
                decisionReason = $"canEat chose the highest-valued safe target: {target.Label} (value {GetJanggiPieceValue(target)})."
            End If
        ElseIf compound.Name = "equalOrGreaterCaptureMove" Then
            selectedMove = FindBestCaptureMove(True)
            If selectedMove IsNot Nothing Then
                Dim target = GetPieceAt(selectedMove.TargetCol, selectedMove.TargetRow, selectedMove.Piece)
                decisionReason = $"equalOrGreaterMove accepted an exchange because target {target.Label} (value {GetJanggiPieceValue(target)}) is worth at least attacker {selectedMove.Piece.Label} (value {GetJanggiPieceValue(selectedMove.Piece)})."
            End If
        ElseIf compound.Name = "moveToPromising" Then
            Dim scored = FindBestPromisingMove()
            If scored IsNot Nothing Then
                selectedMove = scored.Move
                decisionReason = $"moveToPromising chose a safe empty square that creates {scored.FutureCaptureCount} next-turn capture opportunity/opportunities; best future target value is {scored.BestFutureCaptureValue}."
            End If
        Else
            selectedMove = FindSafeFallbackMove()
            If selectedMove IsNot Nothing Then
                decisionReason = "No capture, threatened-piece rescue, or immediate promising setup was available. safeFallbackMove chose a deterministic legal move, preferring a safe forward move by a lower-valued piece."
            End If
        End If

        If selectedMove Is Nothing Then
            RaiseEvent TraceGoal(compound.Name, $"fail: {compound.Name}/4 found no qualifying move", depth + 1)
            Return True
        End If

        Dim movingKind = GetSideAtom(currentTurn)
        Dim pieceLabel = selectedMove.Piece.Label
        Dim fromCol = selectedMove.Piece.Col
        Dim fromRow = selectedMove.Piece.Row
        Dim toCol = selectedMove.TargetCol
        Dim toRow = selectedMove.TargetRow

        Dim nextBindings As New Dictionary(Of String, Term)(bindings)
        If Not Unify(compound.Arguments(1), New AtomTerm(pieceLabel), nextBindings) OrElse
           Not Unify(compound.Arguments(2), New CompoundTerm("pos", New List(Of Term) From {New NumberTerm(fromCol), New NumberTerm(fromRow)}), nextBindings) OrElse
           Not Unify(compound.Arguments(3), New CompoundTerm("pos", New List(Of Term) From {New NumberTerm(toCol), New NumberTerm(toRow)}), nextBindings) Then
            RaiseEvent TraceGoal(compound.Name, $"fail: {compound.Name}/4 could not bind its move arguments", depth + 1)
            Return True
        End If

        RaiseEvent TraceGoal(compound.Name, $"success: {compound.Name}({movingKind}, {pieceLabel}, pos({fromCol}, {fromRow}), pos({toCol}, {toRow}))", depth + 1)
        cutSignal = SolveGoals(remainingGoals, nextBindings, results, depth + 1, maxSolutions, pictureBox, currentCutScope)
        Return True
    End Function

    Private Function TrySolveApplyMove(goal As Term, remainingGoals As List(Of Term), bindings As Dictionary(Of String, Term), results As List(Of Dictionary(Of String, Term)), depth As Integer, maxSolutions As Integer, pictureBox As PictureBox, currentCutScope As Integer, ByRef cutSignal As Integer?) As Boolean
        cutSignal = Nothing
        Dim compound = TryCast(goal, CompoundTerm)
        If compound Is Nothing OrElse compound.Name <> "applyMove" OrElse compound.Arguments.Count <> 4 Then
            Return False
        End If

        Dim kindAtom = TryCast(ResolveTerm(compound.Arguments(0), bindings), AtomTerm)
        Dim pieceAtom = TryCast(ResolveTerm(compound.Arguments(1), bindings), AtomTerm)
        Dim fromPos = TryCast(ResolveTerm(compound.Arguments(2), bindings), CompoundTerm)
        Dim toPos = TryCast(ResolveTerm(compound.Arguments(3), bindings), CompoundTerm)

        If kindAtom Is Nothing OrElse pieceAtom Is Nothing OrElse fromPos Is Nothing OrElse toPos Is Nothing Then
            RaiseEvent TraceGoal(compound.Name, "fail: applyMove/4 arguments not fully bound", depth + 1)
            Return True
        End If

        Dim fromCol = CInt(DirectCast(fromPos.Arguments(0), NumberTerm).Value)
        Dim fromRow = CInt(DirectCast(fromPos.Arguments(1), NumberTerm).Value)
        Dim toCol = CInt(DirectCast(toPos.Arguments(0), NumberTerm).Value)
        Dim toRow = CInt(DirectCast(toPos.Arguments(1), NumberTerm).Value)

        Dim piece = GetPieceAt(fromCol, fromRow)
        If piece Is Nothing OrElse piece.Label <> pieceAtom.Name Then
            RaiseEvent TraceGoal(compound.Name, "fail: piece not found for applyMove", depth + 1)
            Return True
        End If

        Dim move As New JanggiMove(piece, toCol, toRow)
        Dim movingKind As String = ""
        Dim pieceLabel As String = ""
        Dim outFromCol As Integer, outFromRow As Integer, outToCol As Integer, outToRow As Integer
        
        If Not TryApplySelectedMove(pictureBox, move, "Prolog decided on this move", movingKind, pieceLabel, outFromCol, outFromRow, outToCol, outToRow) Then
            RaiseEvent TraceGoal(compound.Name, "fail: applyMove/4 failed in TryApplySelectedMove", depth + 1)
            Return True
        End If

        RaiseEvent TraceGoal(compound.Name, $"success: applyMove({kindAtom.Name}, {pieceLabel}, pos({fromCol}, {fromRow}), pos({toCol}, {toRow}))", depth + 1)
        cutSignal = SolveGoals(remainingGoals, bindings, results, depth + 1, maxSolutions, pictureBox, currentCutScope)
        Return True
    End Function

    Private Function TrySolveRandomPlayMove(goal As Term, remainingGoals As List(Of Term), bindings As Dictionary(Of String, Term), results As List(Of Dictionary(Of String, Term)), depth As Integer, maxSolutions As Integer, pictureBox As PictureBox, currentCutScope As Integer, ByRef cutSignal As Integer?) As Boolean
        cutSignal = Nothing
        Dim compound = TryCast(goal, CompoundTerm)
        If compound Is Nothing OrElse compound.Name <> "randomPlayMove" OrElse compound.Arguments.Count <> 4 Then
            Return False
        End If

        Dim requestedKind = TryCast(ResolveTerm(compound.Arguments(0), bindings), AtomTerm)
        If requestedKind IsNot Nothing AndAlso requestedKind.Name <> GetSideAtom(currentTurn) Then
            RaiseEvent TraceGoal("randomPlayMove", $"fail: randomPlayMove/4 expected {requestedKind.Name}, current turn is {GetSideAtom(currentTurn)}", depth + 1)
            Return True
        End If

        Dim pieceLabel As String = ""
        Dim movingKind As String = ""
        Dim fromCol As Integer
        Dim fromRow As Integer
        Dim toCol As Integer
        Dim toRow As Integer

        If Not TryApplyRandomMove(pictureBox, movingKind, pieceLabel, fromCol, fromRow, toCol, toRow) Then
            RaiseEvent TraceGoal("randomPlayMove", "fail: randomPlayMove/4 found no legal move", depth + 1)
            Return True
        End If

        Dim nextBindings As New Dictionary(Of String, Term)(bindings)
        If Not Unify(compound.Arguments(1), New AtomTerm(pieceLabel), nextBindings) Then
            RaiseEvent TraceGoal("randomPlayMove", $"fail: randomPlayMove/4 could not bind piece {pieceLabel}", depth + 1)
            Return True
        End If

        If Not Unify(compound.Arguments(2), New CompoundTerm("pos", New List(Of Term) From {New NumberTerm(fromCol), New NumberTerm(fromRow)}), nextBindings) Then
            RaiseEvent TraceGoal("randomPlayMove", $"fail: randomPlayMove/4 could not bind from pos({fromCol}, {fromRow})", depth + 1)
            Return True
        End If

        If Not Unify(compound.Arguments(3), New CompoundTerm("pos", New List(Of Term) From {New NumberTerm(toCol), New NumberTerm(toRow)}), nextBindings) Then
            RaiseEvent TraceGoal("randomPlayMove", $"fail: randomPlayMove/4 could not bind to pos({toCol}, {toRow})", depth + 1)
            Return True
        End If

        RaiseEvent TraceGoal("randomPlayMove", $"success: randomPlayMove({movingKind}, {pieceLabel}, pos({fromCol}, {fromRow}), pos({toCol}, {toRow}))", depth + 1)
        cutSignal = SolveGoals(remainingGoals, nextBindings, results, depth + 1, maxSolutions, pictureBox, currentCutScope)
        Return True
    End Function

    Private Function GetPredicateName(term As Term) As String
        Dim atom = TryCast(term, AtomTerm)
        If atom IsNot Nothing Then
            Return atom.Name
        End If

        Dim compound = TryCast(term, CompoundTerm)
        If compound IsNot Nothing Then
            Return compound.Name
        End If

        Return ""
    End Function

    Private Function FreshenClause(clause As ClauseTerm) As ClauseTerm
        freshVariableId += 1
        Dim variableMap As New Dictionary(Of String, String)()
        Dim suffix = $"${freshVariableId}"

        Dim head = CloneTermWithFreshVariables(clause.Head, suffix, variableMap)
        Dim body As New List(Of Term)()
        For Each goal In clause.Body
            body.Add(CloneTermWithFreshVariables(goal, suffix, variableMap))
        Next

        Return New ClauseTerm(head, body)
    End Function

    Private Function CloneTermWithFreshVariables(term As Term, suffix As String, variableMap As Dictionary(Of String, String)) As Term
        Dim variable = TryCast(term, VariableTerm)
        If variable IsNot Nothing Then
            If IsDontCareVariable(variable.Name) Then
                Return New VariableTerm("_")
            End If

            If Not variableMap.ContainsKey(variable.Name) Then
                variableMap(variable.Name) = variable.Name & suffix
            End If

            Return New VariableTerm(variableMap(variable.Name))
        End If

        Dim atom = TryCast(term, AtomTerm)
        If atom IsNot Nothing Then
            Return New AtomTerm(atom.Name)
        End If

        Dim number = TryCast(term, NumberTerm)
        If number IsNot Nothing Then
            Return New NumberTerm(number.Value)
        End If

        Dim propertyAccess = TryCast(term, PropertyAccessTerm)
        If propertyAccess IsNot Nothing Then
            Return New PropertyAccessTerm(CloneTermWithFreshVariables(propertyAccess.Target, suffix, variableMap), propertyAccess.PropertyName)
        End If

        Dim compound = TryCast(term, CompoundTerm)
        If compound IsNot Nothing Then
            Dim args As New List(Of Term)()
            For Each arg In compound.Arguments
                args.Add(CloneTermWithFreshVariables(arg, suffix, variableMap))
            Next

            Return New CompoundTerm(compound.Name, args)
        End If

        Throw New Exception("Unsupported term inside clause.")
    End Function

    Private Function Unify(left As Term, right As Term, bindings As Dictionary(Of String, Term)) As Boolean
        left = ResolveTerm(left, bindings)
        right = ResolveTerm(right, bindings)

        Dim leftVariable = TryCast(left, VariableTerm)
        If leftVariable IsNot Nothing Then
            Return BindVariable(leftVariable, right, bindings)
        End If

        Dim rightVariable = TryCast(right, VariableTerm)
        If rightVariable IsNot Nothing Then
            Return BindVariable(rightVariable, left, bindings)
        End If

        Dim leftAtom = TryCast(left, AtomTerm)
        Dim rightAtom = TryCast(right, AtomTerm)
        If leftAtom IsNot Nothing AndAlso rightAtom IsNot Nothing Then
            Return leftAtom.Name = rightAtom.Name
        End If

        Dim leftNumber = TryCast(left, NumberTerm)
        Dim rightNumber = TryCast(right, NumberTerm)
        If leftNumber IsNot Nothing AndAlso rightNumber IsNot Nothing Then
            Return leftNumber.Value = rightNumber.Value
        End If

        Dim leftCompound = TryCast(left, CompoundTerm)
        Dim rightCompound = TryCast(right, CompoundTerm)
        If leftCompound Is Nothing OrElse rightCompound Is Nothing Then
            Return False
        End If

        If leftCompound.Name <> rightCompound.Name OrElse leftCompound.Arguments.Count <> rightCompound.Arguments.Count Then
            Return False
        End If

        For i = 0 To leftCompound.Arguments.Count - 1
            If Not Unify(leftCompound.Arguments(i), rightCompound.Arguments(i), bindings) Then
                Return False
            End If
        Next

        Return True
    End Function

    Private Function BindVariable(variable As VariableTerm, value As Term, bindings As Dictionary(Of String, Term)) As Boolean
        If IsDontCareVariable(variable.Name) Then
            Return True
        End If

        bindings(variable.Name) = value
        Return True
    End Function

    Private Function ResolveTerm(term As Term, bindings As Dictionary(Of String, Term)) As Term
        Dim variable = TryCast(term, VariableTerm)
        If variable IsNot Nothing AndAlso Not IsDontCareVariable(variable.Name) AndAlso bindings.ContainsKey(variable.Name) Then
            Return ResolveTerm(bindings(variable.Name), bindings)
        End If

        Dim propertyAccess = TryCast(term, PropertyAccessTerm)
        If propertyAccess IsNot Nothing Then
            Dim target = ResolveTerm(propertyAccess.Target, bindings)
            Dim compound = TryCast(target, CompoundTerm)
            If compound IsNot Nothing AndAlso compound.Name = "pos" AndAlso compound.Arguments.Count >= 2 Then
                Select Case propertyAccess.PropertyName.ToLowerInvariant()
                    Case "x", "row", "r"
                        Return ResolveTerm(compound.Arguments(0), bindings)
                    Case "y", "col", "c"
                        Return ResolveTerm(compound.Arguments(1), bindings)
                End Select
            End If

            Return New PropertyAccessTerm(target, propertyAccess.PropertyName)
        End If

        Return term
    End Function

    Private Function FormatTerm(term As Term, bindings As Dictionary(Of String, Term)) As String
        term = ResolveTerm(term, bindings)

        Dim compound = TryCast(term, CompoundTerm)
        If compound IsNot Nothing Then
            Dim args As New List(Of String)()
            For Each arg In compound.Arguments
                args.Add(FormatTerm(arg, bindings))
            Next

            Return $"{compound.Name}({String.Join(", ", args)})"
        End If

        Return term.ToString()
    End Function

    Private Sub CollectVariables(term As Term, variables As List(Of String))
        Dim variable = TryCast(term, VariableTerm)
        If variable IsNot Nothing Then
            If Not IsDontCareVariable(variable.Name) AndAlso Not variables.Contains(variable.Name) Then
                variables.Add(variable.Name)
            End If

            Return
        End If

        Dim compound = TryCast(term, CompoundTerm)
        If compound IsNot Nothing Then
            For Each arg In compound.Arguments
                CollectVariables(arg, variables)
            Next
            Return
        End If

        Dim propertyAccess = TryCast(term, PropertyAccessTerm)
        If propertyAccess IsNot Nothing Then
            CollectVariables(propertyAccess.Target, variables)
        End If
    End Sub

    Private Function IsCallable(term As Term) As Boolean
        Return TypeOf term Is AtomTerm OrElse TypeOf term Is CompoundTerm
    End Function

    Private Function IsSamePredicate(left As Term, right As Term) As Boolean
        Dim leftAtom = TryCast(left, AtomTerm)
        Dim rightAtom = TryCast(right, AtomTerm)
        If leftAtom IsNot Nothing AndAlso rightAtom IsNot Nothing Then
            Return leftAtom.Name = rightAtom.Name
        End If

        Dim leftCompound = TryCast(left, CompoundTerm)
        Dim rightCompound = TryCast(right, CompoundTerm)
        If leftCompound IsNot Nothing AndAlso rightCompound IsNot Nothing Then
            Return leftCompound.Name = rightCompound.Name AndAlso leftCompound.Arguments.Count = rightCompound.Arguments.Count
        End If

        Return False
    End Function

    Private Function IsDontCareVariable(variableName As String) As Boolean
        Return variableName = "_"
    End Function

    Private Function GetNumber(term As Term) As Integer
        Dim n = TryCast(term, NumberTerm)
        If n Is Nothing Then
            Throw New Exception("Numeric argument required.")
        End If
        Return n.Value
    End Function

    Private Sub EnsureBitmap(pb As PictureBox)
        Dim w = Math.Max(pb.Width, 1)
        Dim h = Math.Max(pb.Height, 1)

        If pb.Image Is Nothing OrElse pb.Image.Width <> w OrElse pb.Image.Height <> h Then
            Dim bmp As New Bitmap(w, h)
            Using g = Graphics.FromImage(bmp)
                g.Clear(BoardSurfaceColor)
            End Using
            pb.Image = bmp
            hasBoardGeometry = False
        End If
    End Sub

    Private Sub RememberBoardGeometry(startX As Integer, startY As Integer, dx As Integer, dy As Integer)
        lastBoardStartX = startX
        lastBoardStartY = startY
        lastBoardDX = dx
        lastBoardDY = dy
        hasBoardGeometry = True
    End Sub

    Private Sub EnsureJanggiPieces()
        If janggiPieces.Count = 0 Then
            ResetJanggiPieces()
        End If
    End Sub

    Public Sub ResetJanggiGame(pb As PictureBox)
        ResetJanggiPieces()
        RedrawJanggiBoardAndPieces(pb)
    End Sub

    Public Function SaveJanggiGameState() As Boolean
        If allJanggiPieces.Count = 0 AndAlso janggiPieces.Count = 0 Then
            Return False
        End If

        Dim snapshot As New JanggiBoardSnapshot With {
            .CurrentTurn = currentTurn,
            .GameOver = gameOver,
            .GameOverMessage = gameOverMessage
        }

        For Each piece In allJanggiPieces
            snapshot.Pieces.Add(New JanggiPieceState With {
                .Id = piece.Id,
                .Label = piece.Label,
                .Side = piece.Side,
                .Col = piece.Col,
                .Row = piece.Row,
                .Living = janggiPieces.Contains(piece)
            })
        Next

        savedBoardSnapshot = snapshot
        Return True
    End Function

    Public Function RestoreSavedJanggiGame(pb As PictureBox) As Boolean
        If savedBoardSnapshot Is Nothing OrElse savedBoardSnapshot.Pieces.Count = 0 Then
            Return False
        End If

        allJanggiPieces.Clear()
        janggiPieces.Clear()
        capturedRedPieces.Clear()
        capturedBluePieces.Clear()
        moveCounts.Clear()
        draggingPiece = Nothing
        nextPieceId = 0
        RaiseEvent IsThereReset()
        RaiseEvent IsEatenReset()

        For Each state In savedBoardSnapshot.Pieces
            Dim piece = New JanggiPiece(state.Id, state.Label, state.Side, state.Col, state.Row)
            allJanggiPieces.Add(piece)
            nextPieceId = Math.Max(nextPieceId, state.Id)

            If state.Living Then
                janggiPieces.Add(piece)
            Else
                AddCapturedPiece(piece)
                PublishIsEaten(piece, Nothing)
            End If

            PublishIsThere(piece, If(state.Living, 1, 0))
        Next

        currentTurn = savedBoardSnapshot.CurrentTurn
        gameOver = savedBoardSnapshot.GameOver
        gameOverMessage = savedBoardSnapshot.GameOverMessage
        dragMessage = $"restored saved board state; {CurrentTurnText}"
        PublishCanGoDatabase()
        RedrawJanggiBoardAndPieces(pb)
        Return True
    End Function

    Private Sub ResetJanggiPieces()
        allJanggiPieces.Clear()
        janggiPieces.Clear()
        capturedRedPieces.Clear()
        capturedBluePieces.Clear()
        moveCounts.Clear()
        committedMovePieceIds.Clear()
        lastMovedPieceId = -1
        opponentPreviousMovePieceId = -1
        opponentPreviousMovePieceNegative = False
        nextPieceId = 0
        currentTurn = JanggiSide.Blue
        gameOver = False
        gameOverMessage = ""
        dragMessage = CurrentTurnText
        RaiseEvent IsThereReset()
        RaiseEvent IsEatenReset()

        AddJanggiPiece("車", JanggiSide.Red, 0, 0)
        AddJanggiPiece("象", JanggiSide.Red, 1, 0)
        AddJanggiPiece("馬", JanggiSide.Red, 2, 0)
        AddJanggiPiece("士", JanggiSide.Red, 3, 0)
        AddJanggiPiece("士", JanggiSide.Red, 5, 0)
        AddJanggiPiece("象", JanggiSide.Red, 6, 0)
        AddJanggiPiece("馬", JanggiSide.Red, 7, 0)
        AddJanggiPiece("車", JanggiSide.Red, 8, 0)
        AddJanggiPiece("漢", JanggiSide.Red, 4, 1)
        AddJanggiPiece("砲", JanggiSide.Red, 1, 2)
        AddJanggiPiece("砲", JanggiSide.Red, 7, 2)
        AddJanggiPiece("兵", JanggiSide.Red, 0, 3)
        AddJanggiPiece("兵", JanggiSide.Red, 2, 3)
        AddJanggiPiece("兵", JanggiSide.Red, 4, 3)
        AddJanggiPiece("兵", JanggiSide.Red, 6, 3)
        AddJanggiPiece("兵", JanggiSide.Red, 8, 3)

        AddJanggiPiece("卒", JanggiSide.Blue, 0, 6)
        AddJanggiPiece("卒", JanggiSide.Blue, 2, 6)
        AddJanggiPiece("卒", JanggiSide.Blue, 4, 6)
        AddJanggiPiece("卒", JanggiSide.Blue, 6, 6)
        AddJanggiPiece("卒", JanggiSide.Blue, 8, 6)
        AddJanggiPiece("包", JanggiSide.Blue, 1, 7)
        AddJanggiPiece("包", JanggiSide.Blue, 7, 7)
        AddJanggiPiece("楚", JanggiSide.Blue, 4, 8)
        AddJanggiPiece("車", JanggiSide.Blue, 0, 9)
        AddJanggiPiece("象", JanggiSide.Blue, 1, 9)
        AddJanggiPiece("馬", JanggiSide.Blue, 2, 9)
        AddJanggiPiece("士", JanggiSide.Blue, 3, 9)
        AddJanggiPiece("士", JanggiSide.Blue, 5, 9)
        AddJanggiPiece("象", JanggiSide.Blue, 6, 9)
        AddJanggiPiece("馬", JanggiSide.Blue, 7, 9)
        AddJanggiPiece("車", JanggiSide.Blue, 8, 9)
        PublishCanGoDatabase()
    End Sub

    Private Sub AddJanggiPiece(label As String, side As JanggiSide, col As Integer, row As Integer)
        nextPieceId += 1
        Dim piece = New JanggiPiece(nextPieceId, label, side, col, row)
        allJanggiPieces.Add(piece)
        janggiPieces.Add(piece)
        PublishIsThere(piece, 1)
    End Sub

    Private Sub PublishIsThere(piece As JanggiPiece, living As Integer)
        RaiseEvent IsThereChanged(piece.Id, GetSideAtom(piece.Side), GetPieceTypeAtom(piece), piece.Row, piece.Col, living)
    End Sub

    Private Sub PublishIsEaten(eatenPiece As JanggiPiece, byPiece As JanggiPiece)
        Dim byType = If(byPiece Is Nothing, "unknown", GetPieceTypeAtom(byPiece))
        RaiseEvent IsEatenChanged(GetSideAtom(eatenPiece.Side), GetPieceTypeAtom(eatenPiece), byType)
    End Sub

    Private Sub PublishCanGoDatabase()
        RaiseEvent CanGoReset()
        RaiseEvent CanEatReset()
        RaiseEvent CanBeEatenReset()

        If janggiPieces.Count = 0 Then
            Return
        End If

        Dim previousMessage = dragMessage

        For Each piece In janggiPieces.ToArray()
            For row = 0 To 9
                For col = 0 To 8
                    If IsLegalMove(piece, col, row) Then
                        Dim fromPos = FormatPos(piece.Row, piece.Col)
                        Dim toPos = FormatPos(row, col)
                        RaiseEvent CanGoChanged(
                            GetSideAtom(piece.Side),
                            piece.Label,
                            fromPos,
                            toPos
                        )

                        Dim targetPiece = GetPieceAt(col, row, piece)
                        If targetPiece IsNot Nothing AndAlso targetPiece.Side <> piece.Side Then
                            RaiseEvent CanEatChanged(
                                GetSideAtom(piece.Side),
                                piece.Label,
                                fromPos,
                                toPos,
                                GetSideAtom(targetPiece.Side),
                                targetPiece.Label
                            )
                            RaiseEvent CanBeEatenChanged(
                                GetSideAtom(targetPiece.Side),
                                targetPiece.Label,
                                toPos,
                                GetSideAtom(piece.Side),
                                piece.Label,
                                fromPos
                            )
                        End If
                    End If
                Next
            Next
        Next

        dragMessage = previousMessage
    End Sub

    Private Function FormatPos(row As Integer, col As Integer) As String
        Return $"({row}, {col})"
    End Function

    Private Function GetPieceTypeAtom(piece As JanggiPiece) As String
        Select Case piece.Label
            Case "楚", "漢"
                Return "king"
            Case "車"
                Return "chariot"
            Case "砲", "包"
                Return "cannon"
            Case "馬"
                Return "horse"
            Case "象"
                Return "elephant"
            Case "士"
                Return "guard"
            Case "兵", "卒"
                Return "soldier"
            Case Else
                Return "piece"
        End Select
    End Function

    Private Function GetSideName(side As JanggiSide) As String
        If side = JanggiSide.Blue Then
            Return "Blue"
        End If

        Return "Red"
    End Function

    Private Function GetSideAtom(side As JanggiSide) As String
        If side = JanggiSide.Blue Then
            Return "BLUE"
        End If

        Return "RED"
    End Function

    Private Sub AdvanceTurn()
        If currentTurn = JanggiSide.Blue Then
            currentTurn = JanggiSide.Red
        Else
            currentTurn = JanggiSide.Blue
        End If
    End Sub

    Private Sub RedrawJanggiBoardAndPieces(pb As PictureBox)
        DrawCenteredJanggiBoard(pb)
        DrawJanggiPieces(pb)
        DrawCapturedPieces(pb)
    End Sub

    Private Sub DrawLineOnPictureBox(pb As PictureBox, x1 As Integer, y1 As Integer, x2 As Integer, y2 As Integer)
        EnsureBitmap(pb)

        Dim bmp = DirectCast(pb.Image, Bitmap)
        Using g = Graphics.FromImage(bmp)
            DrawEtchedLine(g, x1, y1, x2, y2)
        End Using

        pb.Invalidate()
    End Sub

    Private Sub DrawBoardOnPictureBox(pb As PictureBox, cols As Integer, rows As Integer)
        EnsureBitmap(pb)

        Dim bmp = DirectCast(pb.Image, Bitmap)
        Using g = Graphics.FromImage(bmp)
            g.Clear(BoardSurfaceColor)

            If cols < 2 OrElse rows < 2 Then Exit Sub

            Dim margin = 20
            Dim availableWidth = Math.Max(10, pb.Width - margin * 2)
            Dim availableHeight = Math.Max(10, pb.Height - margin * 2)

            Dim dx As Integer = Math.Max(1, availableWidth \ (cols - 1))
            Dim dy As Integer = Math.Max(1, availableHeight \ (rows - 1))

            Dim boardWidth = dx * (cols - 1)
            Dim boardHeight = dy * (rows - 1)

            Dim startX = (pb.Width - boardWidth) \ 2
            Dim startY = (pb.Height - boardHeight) \ 2

            RememberBoardGeometry(startX, startY, dx, dy)

            For c = 0 To cols - 1
                Dim x = startX + c * dx
                DrawEtchedLine(g, x, startY, x, startY + boardHeight)
            Next

            For r = 0 To rows - 1
                Dim y = startY + r * dy
                DrawEtchedLine(g, startX, y, startX + boardWidth, y)
            Next
            DrawRaisedBoardFrame(g, startX, startY, boardWidth, boardHeight)
        End Using

        pb.Invalidate()
    End Sub

    Private Sub DrawCenteredJanggiBoard(pb As PictureBox)
        EnsureBitmap(pb)

        Dim bmp = DirectCast(pb.Image, Bitmap)
        Using g = Graphics.FromImage(bmp)
            g.Clear(BoardSurfaceColor)

            Dim cols = 9
            Dim rows = 10

            Dim margin = 20
            Dim availableWidth = Math.Max(10, pb.Width - margin * 2)
            Dim availableHeight = Math.Max(10, pb.Height - margin * 2)

            Dim dx As Integer = Math.Max(1, availableWidth \ (cols - 1))
            Dim dy As Integer = Math.Max(1, availableHeight \ (rows - 1))

            Dim cellSize = Math.Min(dx, dy)
            dx = cellSize
            dy = cellSize

            Dim boardWidth = dx * (cols - 1)
            Dim boardHeight = dy * (rows - 1)

            Dim startX = (pb.Width - boardWidth) \ 2
            Dim startY = (pb.Height - boardHeight) \ 2

            RememberBoardGeometry(startX, startY, dx, dy)

            ' Etched grid
            For c = 0 To cols - 1
                Dim x = startX + c * dx
                DrawEtchedLine(g, x, startY, x, startY + boardHeight)
            Next

            For r = 0 To rows - 1
                Dim y = startY + r * dy
                DrawEtchedLine(g, startX, y, startX + boardWidth, y)
            Next

            ' Etched palace diagonals
            DrawPalaceDiagonals(g, startX, startY, dx, dy)
            Dim bottomPalaceTopY = startY + 7 * dy
            DrawPalaceDiagonals(g, startX, bottomPalaceTopY, dx, dy)
            DrawRaisedBoardFrame(g, startX, startY, boardWidth, boardHeight)
        End Using

        pb.Invalidate()
    End Sub

    Private Sub DrawJanggiPieces(pb As PictureBox)
        EnsureBitmap(pb)

        If Not hasBoardGeometry Then
            DrawCenteredJanggiBoard(pb)
        End If

        EnsureBitmap(pb)

        Dim bmp = DirectCast(pb.Image, Bitmap)
        Using g = Graphics.FromImage(bmp)
            Using blueBrush As New SolidBrush(Color.Blue)
                Using redBrush As New SolidBrush(Color.Red)
                    Dim fontSize As Single = Math.Max(8.0F, Math.Min(lastBoardDX, lastBoardDY) * 0.42F)

                    Using font As New Font("Microsoft YaHei UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel)
                        For Each piece In janggiPieces
                            If piece IsNot draggingPiece Then
                                Dim pieceBrush As Brush = If(piece.Side = JanggiSide.Blue, blueBrush, redBrush)
                                DrawPieceAt(g, font, pieceBrush, piece.Col, piece.Row, piece.Label,
                                    piece.Id = opponentPreviousMovePieceId AndAlso opponentPreviousMovePieceNegative)
                            End If
                        Next

                        If draggingPiece IsNot Nothing Then
                            Dim dragBrush As Brush = If(draggingPiece.Side = JanggiSide.Blue, blueBrush, redBrush)
                            DrawPieceAtPoint(g, font, dragBrush, dragPoint.X, dragPoint.Y, draggingPiece.Label,
                                draggingPiece.Id = opponentPreviousMovePieceId AndAlso opponentPreviousMovePieceNegative)
                        End If
                    End Using
                End Using
            End Using
        End Using

        pb.Invalidate()
    End Sub

    Private Sub DrawPieceRow(g As Graphics, font As Font, brush As Brush, row As Integer, labels As String())
        For col = 0 To Math.Min(labels.Length, 9) - 1
            If labels(col) <> "" Then
                DrawPieceAt(g, font, brush, col, row, labels(col))
            End If
        Next
    End Sub

    Public Function BeginPieceDrag(pb As PictureBox, location As Point) As Boolean
        EnsureJanggiPieces()

        If Not hasBoardGeometry Then
            RedrawJanggiBoardAndPieces(pb)
        End If

        draggingPiece = FindPieceAtPoint(location)
        If draggingPiece Is Nothing Then
            dragMessage = $"select a {GetSideName(currentTurn)} piece"
            RedrawJanggiBoardAndPieces(pb)
            Return False
        End If

        If draggingPiece.Side <> currentTurn Then
            dragMessage = $"{GetSideName(currentTurn)} Turn"
            draggingPiece = Nothing
            RedrawJanggiBoardAndPieces(pb)
            Return False
        End If

        dragStartCol = draggingPiece.Col
        dragStartRow = draggingPiece.Row
        dragPoint = location
        dragMessage = $"dragging {draggingPiece.Label}"
        RedrawJanggiBoardAndPieces(pb)
        Return True
    End Function

    Public Function MovePieceDrag(pb As PictureBox, location As Point) As Boolean
        If draggingPiece Is Nothing Then
            Return False
        End If

        dragPoint = location
        RedrawJanggiBoardAndPieces(pb)
        Return True
    End Function

    Public Function EndPieceDrag(pb As PictureBox, location As Point) As Boolean
        If draggingPiece Is Nothing Then
            Return False
        End If

        Dim targetCol As Integer
        Dim targetRow As Integer
        Dim moved = False

        Dim legalDrop = TryPointToBoardPosition(location, targetCol, targetRow) AndAlso
            IsLegalMove(draggingPiece, targetCol, targetRow)
        If legalDrop Then
            Dim capturedPiece = GetPieceAt(targetCol, targetRow, draggingPiece)

            If capturedPiece IsNot Nothing Then
                AddCapturedPiece(capturedPiece)
                PublishIsThere(capturedPiece, 0)
                PublishIsEaten(capturedPiece, draggingPiece)
                janggiPieces.Remove(capturedPiece)
                dragMessage = $"{draggingPiece.Label} captured {capturedPiece.Label}"
            Else
                dragMessage = $"{draggingPiece.Label} moved"
            End If

            draggingPiece.Col = targetCol
            draggingPiece.Row = targetRow
            RememberLastCommittedMove(draggingPiece, dragStartCol, dragStartRow, targetCol, targetRow)
            PublishIsThere(draggingPiece, 1)
            AdvanceTurn()
            If capturedPiece IsNot Nothing AndAlso IsKingPiece(capturedPiece) Then
                gameOver = True
                gameOverMessage = $"{GetSideName(draggingPiece.Side)} captured the king ({capturedPiece.Label}). Game over."
                dragMessage = gameOverMessage
            Else
                dragMessage &= $"; {CurrentTurnText}"
            End If
            PublishCanGoDatabase()
            RaiseEvent MoveCommitted(dragMessage, "The manually dragged move passed the Janggi legal-move rules. No Prolog move-selection clause chose this move.")
            moved = True
        Else
            draggingPiece.Col = dragStartCol
            draggingPiece.Row = dragStartRow

            If dragMessage = "" OrElse dragMessage.StartsWith("dragging") Then
                dragMessage = "illegal move"
            End If
        End If

        draggingPiece = Nothing
        RedrawJanggiBoardAndPieces(pb)
        Return moved
    End Function

    Public Sub CancelPieceDrag(pb As PictureBox)
        If draggingPiece Is Nothing Then Exit Sub

        draggingPiece.Col = dragStartCol
        draggingPiece.Row = dragStartRow
        draggingPiece = Nothing
        dragMessage = "move cancelled"
        RedrawJanggiBoardAndPieces(pb)
    End Sub

    Public Function MakeRandomMove(pb As PictureBox) As Boolean
        Dim movingKind As String = ""
        Dim pieceLabel As String = ""
        Dim fromCol As Integer
        Dim fromRow As Integer
        Dim toCol As Integer
        Dim toRow As Integer

        Return TryApplyRandomMove(pb, movingKind, pieceLabel, fromCol, fromRow, toCol, toRow)
    End Function

    Public Sub AnimateLastCommittedMove(pb As PictureBox)
        If pb Is Nothing OrElse pb.IsDisposed OrElse lastMovedPieceId < 0 Then Exit Sub

        Dim movedPiece = janggiPieces.FirstOrDefault(Function(piece) piece.Id = lastMovedPieceId)
        If movedPiece Is Nothing Then Exit Sub

        AnimateJanggiMove(pb, movedPiece, lastMoveToCol, lastMoveToRow, lastMoveFromCol, lastMoveFromRow)
        AnimateJanggiMove(pb, movedPiece, lastMoveFromCol, lastMoveFromRow, lastMoveToCol, lastMoveToRow)
        RedrawJanggiBoardAndPieces(pb)
        pb.Refresh()
    End Sub

    Public Sub SetOpponentPreviousMovePieceNegative(pb As PictureBox, negativeImage As Boolean)
        opponentPreviousMovePieceNegative = negativeImage
        If pb Is Nothing OrElse pb.IsDisposed OrElse Not HasOpponentPreviousMovePiece Then Exit Sub

        RedrawJanggiBoardAndPieces(pb)
        pb.Refresh()
    End Sub

    Private Sub RememberLastCommittedMove(piece As JanggiPiece, fromCol As Integer, fromRow As Integer, toCol As Integer, toRow As Integer)
        lastMovedPieceId = piece.Id
        lastMoveFromCol = fromCol
        lastMoveFromRow = fromRow
        lastMoveToCol = toCol
        lastMoveToRow = toRow
        committedMovePieceIds.Add(piece.Id)
        If committedMovePieceIds.Count > 32 Then
            committedMovePieceIds.RemoveAt(0)
        End If

        If committedMovePieceIds.Count >= 2 Then
            opponentPreviousMovePieceId = committedMovePieceIds(committedMovePieceIds.Count - 2)
        Else
            opponentPreviousMovePieceId = -1
        End If
        opponentPreviousMovePieceNegative = False
    End Sub

    Public Function GetLastCommittedMoveTerm() As Term
        If lastMovedPieceId < 0 Then Return Nothing

        Dim piece = allJanggiPieces.FirstOrDefault(Function(candidate) candidate.Id = lastMovedPieceId)
        If piece Is Nothing Then Return Nothing

        Return New CompoundTerm("move", New List(Of Term) From {
            New AtomTerm(GetSideAtom(piece.Side)),
            New AtomTerm(GetPieceTypeAtom(piece)),
            New CompoundTerm("pos", New List(Of Term) From {New NumberTerm(lastMoveFromCol), New NumberTerm(lastMoveFromRow)}),
            New CompoundTerm("pos", New List(Of Term) From {New NumberTerm(lastMoveToCol), New NumberTerm(lastMoveToRow)})
        })
    End Function

    Public Function HasLegalMoveForCurrentTurn() As Boolean
        EnsureJanggiPieces()
        If gameOver Then
            Return False
        End If

        Return GetLegalMovesForCurrentTurn().Count > 0
    End Function

    Private Function TryApplyRandomMove(pb As PictureBox, ByRef movingKind As String, ByRef pieceLabel As String, ByRef fromCol As Integer, ByRef fromRow As Integer, ByRef toCol As Integer, ByRef toRow As Integer) As Boolean
        EnsureJanggiPieces()

        If gameOver Then
            dragMessage = gameOverMessage
            Return False
        End If

        If Not hasBoardGeometry Then
            RedrawJanggiBoardAndPieces(pb)
        End If

        Dim legalMoves = GetLegalMovesForCurrentTurn()
        If legalMoves.Count = 0 Then
            dragMessage = $"{CurrentTurnText}: no legal moves"
            RedrawJanggiBoardAndPieces(pb)
            Return False
        End If

        Dim safeMoves = legalMoves.Where(Function(candidate) Not CanOpponentCaptureAfterMove(candidate)).ToList()
        Dim movePool = If(safeMoves.Count > 0, safeMoves, legalMoves)
        Dim decisionReason As String
        If safeMoves.Count > 0 Then
            decisionReason = $"The runtime chose randomly from {safeMoves.Count} safe legal move(s), excluding moves that the opponent could capture immediately."
        Else
            decisionReason = $"No immediately safe move was available, so the runtime chose randomly from all {legalMoves.Count} legal move(s)."
        End If
        Dim move = movePool(random.Next(movePool.Count))
        Return TryApplySelectedMove(pb, move, decisionReason, movingKind, pieceLabel, fromCol, fromRow, toCol, toRow)
    End Function

    Private Function TryApplySelectedMove(pb As PictureBox, move As JanggiMove, decisionReason As String, ByRef movingKind As String, ByRef pieceLabel As String, ByRef fromCol As Integer, ByRef fromRow As Integer, ByRef toCol As Integer, ByRef toRow As Integer) As Boolean
        If move Is Nothing OrElse move.Piece Is Nothing OrElse gameOver Then Return False
        If move.Piece.Side <> currentTurn Then
            dragMessage = $"turn mismatch: expected {CurrentTurnText}, selected {GetSideName(move.Piece.Side)}"
            Return False
        End If

        Dim capturedPiece = GetPieceAt(move.TargetCol, move.TargetRow, move.Piece)
        fromCol = move.Piece.Col
        fromRow = move.Piece.Row
        toCol = move.TargetCol
        toRow = move.TargetRow
        pieceLabel = move.Piece.Label
        movingKind = GetSideAtom(currentTurn)

        If capturedPiece IsNot Nothing Then
            AddCapturedPiece(capturedPiece)
            PublishIsThere(capturedPiece, 0)
            PublishIsEaten(capturedPiece, move.Piece)
            janggiPieces.Remove(capturedPiece)
        End If

        AnimateJanggiMove(pb, move.Piece, fromCol, fromRow, toCol, toRow)
        move.Piece.Col = toCol
        move.Piece.Row = toRow
        RememberLastCommittedMove(move.Piece, fromCol, fromRow, toCol, toRow)
        PublishIsThere(move.Piece, 1)
        Dim sideName = GetSideName(currentTurn)
        AdvanceTurn()
        PublishCanGoDatabase()

        If capturedPiece IsNot Nothing Then
            dragMessage = $"{sideName} {move.Piece.Label} {FormatMove(fromCol, fromRow, toCol, toRow)} captured {capturedPiece.Label}; {CurrentTurnText}"
            If IsKingPiece(capturedPiece) Then
                gameOver = True
                gameOverMessage = $"{sideName} captured the king ({capturedPiece.Label}). Game over."
                dragMessage = gameOverMessage
            End If
        Else
            dragMessage = $"{sideName} {move.Piece.Label} {FormatMove(fromCol, fromRow, toCol, toRow)}; {CurrentTurnText}"
        End If

        RaiseEvent MoveCommitted(dragMessage, decisionReason)
        RedrawJanggiBoardAndPieces(pb)
        Return True
    End Function

    Private Function FindBestCaptureMove(riskyEqualOrGreaterOnly As Boolean) As JanggiMove
        Return GetLegalMovesForCurrentTurn().
            Where(Function(candidate)
                      Dim target = GetPieceAt(candidate.TargetCol, candidate.TargetRow, candidate.Piece)
                      If target Is Nothing OrElse target.Side = candidate.Piece.Side Then Return False
                      Dim canBeCaptured = CanOpponentCaptureAfterMove(candidate)
                      If riskyEqualOrGreaterOnly Then
                          Return canBeCaptured AndAlso GetJanggiPieceValue(target) >= GetJanggiPieceValue(candidate.Piece)
                      End If
                      Return Not canBeCaptured
                  End Function).
            OrderByDescending(Function(candidate) GetJanggiPieceValue(GetPieceAt(candidate.TargetCol, candidate.TargetRow, candidate.Piece))).
            ThenBy(Function(candidate) GetJanggiPieceValue(candidate.Piece)).
            FirstOrDefault()
    End Function

    Private Function FindBestProtectiveMove() As JanggiMove
        Dim threatenedPieceIds = GetThreatenedPieceIds(currentTurn)

        If threatenedPieceIds.Count = 0 Then Return Nothing

        Return GetLegalMovesForCurrentTurn().
            Where(Function(candidate)
                      Return threatenedPieceIds.Contains(candidate.Piece.Id) AndAlso
                             ProtectiveEscapeAvoidsGreaterLoss(candidate, threatenedPieceIds)
                  End Function).
            OrderByDescending(Function(candidate) GetJanggiPieceValue(candidate.Piece)).
            ThenByDescending(Function(candidate) GetJanggiPieceValue(GetPieceAt(candidate.TargetCol, candidate.TargetRow, candidate.Piece))).
            FirstOrDefault()
    End Function

    Private Function GetThreatenedPieceIds(side As JanggiSide) As HashSet(Of Integer)
        Return janggiPieces.
            Where(Function(piece) piece.Side = side AndAlso IsPieceCurrentlyThreatened(piece)).
            Select(Function(piece) piece.Id).
            ToHashSet()
    End Function

    Private Function ProtectiveEscapeAvoidsGreaterLoss(move As JanggiMove, threatsBefore As HashSet(Of Integer)) As Boolean
        Dim movingPiece = move.Piece
        Dim originalCol = movingPiece.Col
        Dim originalRow = movingPiece.Row
        Dim capturedPiece = GetPieceAt(move.TargetCol, move.TargetRow, movingPiece)
        Dim capturedIndex = If(capturedPiece Is Nothing, -1, janggiPieces.IndexOf(capturedPiece))
        Dim previousMessage = dragMessage

        Try
            If capturedPiece IsNot Nothing Then janggiPieces.Remove(capturedPiece)
            movingPiece.Col = move.TargetCol
            movingPiece.Row = move.TargetRow

            Dim threatsAfter = GetThreatenedPieceIds(movingPiece.Side)
            If threatsAfter.Contains(movingPiece.Id) Then Return False

            Dim escapingValue = GetJanggiPieceValue(movingPiece)
            For Each friendlyPiece In janggiPieces
                If friendlyPiece.Side <> movingPiece.Side OrElse friendlyPiece.Id = movingPiece.Id Then Continue For
                If threatsAfter.Contains(friendlyPiece.Id) AndAlso
                   Not threatsBefore.Contains(friendlyPiece.Id) AndAlso
                   GetJanggiPieceValue(friendlyPiece) > escapingValue Then
                    Return False
                End If
            Next

            Return True
        Finally
            movingPiece.Col = originalCol
            movingPiece.Row = originalRow
            If capturedPiece IsNot Nothing Then
                If capturedIndex >= 0 AndAlso capturedIndex <= janggiPieces.Count Then
                    janggiPieces.Insert(capturedIndex, capturedPiece)
                Else
                    janggiPieces.Add(capturedPiece)
                End If
            End If
            dragMessage = previousMessage
        End Try
    End Function

    Private Function FindSafeFallbackMove() As JanggiMove
        Dim legalMoves = GetLegalMovesForCurrentTurn()
        If legalMoves.Count = 0 Then Return Nothing

        Dim safeMoves = legalMoves.
            Where(Function(candidate) Not CanOpponentCaptureAfterMove(candidate)).
            ToList()
        Dim candidates = If(safeMoves.Count > 0, safeMoves, legalMoves)

        Return candidates.
            OrderByDescending(Function(candidate) ForwardProgress(candidate)).
            ThenBy(Function(candidate) GetJanggiPieceValue(candidate.Piece)).
            ThenBy(Function(candidate) candidate.Piece.Id).
            ThenBy(Function(candidate) candidate.TargetRow).
            ThenBy(Function(candidate) candidate.TargetCol).
            FirstOrDefault()
    End Function

    Private Function ForwardProgress(move As JanggiMove) As Integer
        If move Is Nothing OrElse move.Piece Is Nothing Then Return Integer.MinValue
        If move.Piece.Side = JanggiSide.Red Then
            Return move.TargetRow - move.Piece.Row
        End If
        Return move.Piece.Row - move.TargetRow
    End Function

    Private Function IsPieceCurrentlyThreatened(piece As JanggiPiece) As Boolean
        Dim previousMessage = dragMessage
        Dim threatened = janggiPieces.Any(
            Function(opponent)
                Return opponent IsNot piece AndAlso
                       opponent.Side <> piece.Side AndAlso
                       IsLegalMove(opponent, piece.Col, piece.Row)
            End Function)
        dragMessage = previousMessage
        Return threatened
    End Function

    Private Function FindBestPromisingMove() As ScoredMove
        Dim scoredMoves As New List(Of ScoredMove)()

        For Each candidate In GetLegalMovesForCurrentTurn()
            If GetPieceAt(candidate.TargetCol, candidate.TargetRow, candidate.Piece) IsNot Nothing Then Continue For
            If CanOpponentCaptureAfterMove(candidate) Then Continue For

            Dim captureCount As Integer
            Dim bestCaptureValue As Integer
            EvaluateFutureCaptures(candidate, captureCount, bestCaptureValue)
            If captureCount > 0 Then
                scoredMoves.Add(New ScoredMove With {
                    .Move = candidate,
                    .FutureCaptureCount = captureCount,
                    .BestFutureCaptureValue = bestCaptureValue
                })
            End If
        Next

        Return scoredMoves.
            OrderByDescending(Function(scored) scored.BestFutureCaptureValue).
            ThenByDescending(Function(scored) scored.FutureCaptureCount).
            ThenBy(Function(scored) GetJanggiPieceValue(scored.Move.Piece)).
            Select(Function(scored) scored).
            FirstOrDefault()
    End Function

    Private Sub EvaluateFutureCaptures(move As JanggiMove, ByRef captureCount As Integer, ByRef bestCaptureValue As Integer)
        captureCount = 0
        bestCaptureValue = 0
        Dim movingPiece = move.Piece
        Dim originalCol = movingPiece.Col
        Dim originalRow = movingPiece.Row
        Dim previousMessage = dragMessage

        movingPiece.Col = move.TargetCol
        movingPiece.Row = move.TargetRow
        For Each target In janggiPieces.ToArray()
            If target IsNot movingPiece AndAlso target.Side <> movingPiece.Side AndAlso IsLegalMove(movingPiece, target.Col, target.Row) Then
                captureCount += 1
                bestCaptureValue = Math.Max(bestCaptureValue, GetJanggiPieceValue(target))
            End If
        Next

        movingPiece.Col = originalCol
        movingPiece.Row = originalRow
        dragMessage = previousMessage
    End Sub

    Private Function GetJanggiPieceValue(piece As JanggiPiece) As Integer
        If piece Is Nothing Then Return 0

        Select Case piece.Label
            Case "楚", "漢"
                Return 1000
            Case "車"
                Return 90
            Case "包", "砲"
                Return 50
            Case "馬"
                Return 40
            Case "象"
                Return 30
            Case "士"
                Return 20
            Case "卒", "兵"
                Return 10
            Case Else
                Return 0
        End Select
    End Function

    Private Function CanOpponentCaptureAfterMove(move As JanggiMove) As Boolean
        Dim movingPiece = move.Piece
        Dim originalCol = movingPiece.Col
        Dim originalRow = movingPiece.Row
        Dim capturedPiece = GetPieceAt(move.TargetCol, move.TargetRow, movingPiece)
        Dim previousMessage = dragMessage

        If capturedPiece IsNot Nothing AndAlso IsKingPiece(capturedPiece) Then
            Return False
        End If

        If capturedPiece IsNot Nothing Then
            janggiPieces.Remove(capturedPiece)
        End If

        movingPiece.Col = move.TargetCol
        movingPiece.Row = move.TargetRow

        Dim canBeEaten = False
        For Each opponent In janggiPieces.ToArray()
            If opponent IsNot movingPiece AndAlso opponent.Side <> movingPiece.Side AndAlso IsLegalMove(opponent, movingPiece.Col, movingPiece.Row) Then
                canBeEaten = True
                Exit For
            End If
        Next

        movingPiece.Col = originalCol
        movingPiece.Row = originalRow

        If capturedPiece IsNot Nothing Then
            janggiPieces.Add(capturedPiece)
        End If

        dragMessage = previousMessage
        Return canBeEaten
    End Function

    Private Function IsKingPiece(piece As JanggiPiece) As Boolean
        Return piece.Label = "楚" OrElse piece.Label = "漢"
    End Function

    Private Sub AddCapturedPiece(piece As JanggiPiece)
        If piece.Side = JanggiSide.Red Then
            capturedRedPieces.Add(piece.Label)
        Else
            capturedBluePieces.Add(piece.Label)
        End If
    End Sub

    Private Sub AnimateJanggiMove(pb As PictureBox, piece As JanggiPiece, fromCol As Integer, fromRow As Integer, toCol As Integer, toRow As Integer)
        If Not hasBoardGeometry Then
            RedrawJanggiBoardAndPieces(pb)
        End If

        Dim startPoint = GetBoardPoint(fromCol, fromRow)
        Dim endPoint = GetBoardPoint(toCol, toRow)
        draggingPiece = piece

        For stepIndex = 0 To 6
            Dim t = stepIndex / 6.0
            dragPoint = New Point(
                CInt(startPoint.X + (endPoint.X - startPoint.X) * t),
                CInt(startPoint.Y + (endPoint.Y - startPoint.Y) * t)
            )
            RedrawJanggiBoardAndPieces(pb)
            pb.Refresh()
            Application.DoEvents()
            Threading.Thread.Sleep(18)
        Next

        draggingPiece = Nothing
    End Sub

    Private Function GetLegalMovesForCurrentTurn() As List(Of JanggiMove)
        Dim legalMoves As New List(Of JanggiMove)()
        Dim previousMessage = dragMessage

        For Each piece In janggiPieces.ToArray()
            If piece.Side = currentTurn Then
                For row = 0 To 9
                    For col = 0 To 8
                        If IsLegalMove(piece, col, row) Then
                            legalMoves.Add(New JanggiMove(piece, col, row))
                        End If
                    Next
                Next
            End If
        Next

        dragMessage = previousMessage
        Return legalMoves
    End Function

    Private Function CountCanGo(side As JanggiSide) As Integer
        Return GetLegalMovesForSide(side).Count
    End Function

    Private Function CountCanEat(side As JanggiSide) As Integer
        Dim total = 0

        For Each move In GetLegalMovesForSide(side)
            Dim targetPiece = GetPieceAt(move.TargetCol, move.TargetRow, move.Piece)
            If targetPiece IsNot Nothing AndAlso targetPiece.Side <> side Then
                total += 1
            End If
        Next

        Return total
    End Function

    Private Function CountCanBeEaten(side As JanggiSide) As Integer
        Dim total = 0
        Dim opponentSide = If(side = JanggiSide.Blue, JanggiSide.Red, JanggiSide.Blue)

        For Each move In GetLegalMovesForSide(opponentSide)
            Dim targetPiece = GetPieceAt(move.TargetCol, move.TargetRow, move.Piece)
            If targetPiece IsNot Nothing AndAlso targetPiece.Side = side Then
                total += 1
            End If
        Next

        Return total
    End Function

    Private Function GetLegalMovesForSide(side As JanggiSide) As List(Of JanggiMove)
        Dim legalMoves As New List(Of JanggiMove)()
        Dim previousMessage = dragMessage

        For Each piece In janggiPieces.ToArray()
            If piece.Side = side Then
                For row = 0 To 9
                    For col = 0 To 8
                        If IsLegalMove(piece, col, row) Then
                            legalMoves.Add(New JanggiMove(piece, col, row))
                        End If
                    Next
                Next
            End If
        Next

        dragMessage = previousMessage
        Return legalMoves
    End Function

    Private Function FormatMove(fromCol As Integer, fromRow As Integer, targetCol As Integer, targetRow As Integer) As String
        Return $"({fromCol},{fromRow}) -> ({targetCol},{targetRow})"
    End Function

    Private Function FindPieceAtPoint(location As Point) As JanggiPiece
        For i = janggiPieces.Count - 1 To 0 Step -1
            Dim piece = janggiPieces(i)
            Dim center = GetBoardPoint(piece.Col, piece.Row)
            Dim radius = GetPieceRadius(piece.Label)
            Dim dx = location.X - center.X
            Dim dy = location.Y - center.Y

            If dx * dx + dy * dy <= radius * radius Then
                Return piece
            End If
        Next

        Return Nothing
    End Function

    Private Function TryPointToBoardPosition(location As Point, ByRef col As Integer, ByRef row As Integer) As Boolean
        If Not hasBoardGeometry OrElse lastBoardDX <= 0 OrElse lastBoardDY <= 0 Then
            Return False
        End If

        col = CInt(Math.Floor(((location.X - lastBoardStartX) / CDbl(lastBoardDX)) + 0.5))
        row = CInt(Math.Floor(((location.Y - lastBoardStartY) / CDbl(lastBoardDY)) + 0.5))

        If Not IsInsideBoard(col, row) Then
            dragMessage = "illegal: outside board"
            Return False
        End If

        Dim snappedPoint = GetBoardPoint(col, row)
        Dim dx = location.X - snappedPoint.X
        Dim dy = location.Y - snappedPoint.Y
        Dim snapRadius = Math.Min(lastBoardDX, lastBoardDY) * 0.48

        If dx * dx + dy * dy > snapRadius * snapRadius Then
            dragMessage = "illegal: drop closer to a board intersection"
            Return False
        End If

        Return True
    End Function

    Private Function IsLegalMove(piece As JanggiPiece, targetCol As Integer, targetRow As Integer) As Boolean
        If Not IsInsideBoard(targetCol, targetRow) Then
            dragMessage = "illegal: outside board"
            Return False
        End If

        If piece.Col = targetCol AndAlso piece.Row = targetRow Then
            dragMessage = "move cancelled"
            Return False
        End If

        Dim targetPiece = GetPieceAt(targetCol, targetRow, piece)
        If targetPiece IsNot Nothing AndAlso targetPiece.Side = piece.Side Then
            dragMessage = "illegal: your own piece is there"
            Return False
        End If

        Dim legal As Boolean
        Select Case piece.Label
            Case "兵", "卒"
                legal = IsLegalSoldierMove(piece, targetCol, targetRow)
            Case "楚", "漢", "士"
                legal = IsLegalPalaceMove(piece, targetCol, targetRow)
            Case "車"
                legal = IsLegalChariotMove(piece, targetCol, targetRow)
            Case "馬"
                legal = IsLegalHorseMove(piece, targetCol, targetRow)
            Case "象"
                legal = IsLegalElephantMove(piece, targetCol, targetRow)
            Case "包", "砲"
                legal = IsLegalCannonMove(piece, targetCol, targetRow)
            Case Else
                legal = False
        End Select

        If Not legal Then
            dragMessage = $"illegal: {piece.Label} cannot move there"
        End If

        Return legal
    End Function

    Private Function IsLegalSoldierMove(piece As JanggiPiece, targetCol As Integer, targetRow As Integer) As Boolean
        Dim dc = targetCol - piece.Col
        Dim dr = targetRow - piece.Row
        Dim forward = If(piece.Side = JanggiSide.Red, 1, -1)

        If dc = 0 AndAlso dr = forward Then Return True
        If Math.Abs(dc) = 1 AndAlso dr = 0 Then Return True
        If Math.Abs(dc) = 1 AndAlso dr = forward AndAlso IsPalaceDiagonalStep(piece.Col, piece.Row, targetCol, targetRow) Then Return True

        Return False
    End Function

    Private Function IsLegalPalaceMove(piece As JanggiPiece, targetCol As Integer, targetRow As Integer) As Boolean
        If Not IsInsideOwnPalace(piece.Side, piece.Col, piece.Row) OrElse Not IsInsideOwnPalace(piece.Side, targetCol, targetRow) Then
            Return False
        End If

        Dim dc = Math.Abs(targetCol - piece.Col)
        Dim dr = Math.Abs(targetRow - piece.Row)

        If dc + dr = 1 Then Return True
        If dc = 1 AndAlso dr = 1 AndAlso IsPalaceDiagonalStep(piece.Col, piece.Row, targetCol, targetRow) Then Return True

        Return False
    End Function

    Private Function IsLegalChariotMove(piece As JanggiPiece, targetCol As Integer, targetRow As Integer) As Boolean
        If piece.Col <> targetCol AndAlso piece.Row <> targetRow Then
            Return False
        End If

        Return IsStraightPathClear(piece.Col, piece.Row, targetCol, targetRow, piece)
    End Function

    Private Function IsLegalHorseMove(piece As JanggiPiece, targetCol As Integer, targetRow As Integer) As Boolean
        Dim dc = targetCol - piece.Col
        Dim dr = targetRow - piece.Row
        Dim absDc = Math.Abs(dc)
        Dim absDr = Math.Abs(dr)
        Dim blockCol As Integer
        Dim blockRow As Integer

        If absDc = 2 AndAlso absDr = 1 Then
            blockCol = piece.Col + Math.Sign(dc)
            blockRow = piece.Row
        ElseIf absDc = 1 AndAlso absDr = 2 Then
            blockCol = piece.Col
            blockRow = piece.Row + Math.Sign(dr)
        Else
            Return False
        End If

        Return GetPieceAt(blockCol, blockRow, piece) Is Nothing
    End Function

    Private Function IsLegalElephantMove(piece As JanggiPiece, targetCol As Integer, targetRow As Integer) As Boolean
        Dim dc = targetCol - piece.Col
        Dim dr = targetRow - piece.Row
        Dim absDc = Math.Abs(dc)
        Dim absDr = Math.Abs(dr)
        Dim block1Col As Integer
        Dim block1Row As Integer
        Dim block2Col As Integer
        Dim block2Row As Integer

        If absDc = 3 AndAlso absDr = 2 Then
            block1Col = piece.Col + Math.Sign(dc)
            block1Row = piece.Row
            block2Col = piece.Col + Math.Sign(dc) * 2
            block2Row = piece.Row + Math.Sign(dr)
        ElseIf absDc = 2 AndAlso absDr = 3 Then
            block1Col = piece.Col
            block1Row = piece.Row + Math.Sign(dr)
            block2Col = piece.Col + Math.Sign(dc)
            block2Row = piece.Row + Math.Sign(dr) * 2
        Else
            Return False
        End If

        Return GetPieceAt(block1Col, block1Row, piece) Is Nothing AndAlso GetPieceAt(block2Col, block2Row, piece) Is Nothing
    End Function

    Private Function IsLegalCannonMove(piece As JanggiPiece, targetCol As Integer, targetRow As Integer) As Boolean
        If piece.Col <> targetCol AndAlso piece.Row <> targetRow Then
            Return False
        End If

        Dim screenPiece As JanggiPiece = Nothing
        Dim screenCount = CountPiecesBetweenStraight(piece.Col, piece.Row, targetCol, targetRow, piece, screenPiece)
        If screenCount <> 1 OrElse IsCannon(screenPiece) Then
            Return False
        End If

        Dim targetPiece = GetPieceAt(targetCol, targetRow, piece)
        If targetPiece IsNot Nothing AndAlso IsCannon(targetPiece) Then
            Return False
        End If

        Return True
    End Function

    Private Function IsStraightPathClear(fromCol As Integer, fromRow As Integer, targetCol As Integer, targetRow As Integer, ignoredPiece As JanggiPiece) As Boolean
        Dim stepCol = Math.Sign(targetCol - fromCol)
        Dim stepRow = Math.Sign(targetRow - fromRow)
        Dim col = fromCol + stepCol
        Dim row = fromRow + stepRow

        While col <> targetCol OrElse row <> targetRow
            If GetPieceAt(col, row, ignoredPiece) IsNot Nothing Then
                Return False
            End If

            col += stepCol
            row += stepRow
        End While

        Return True
    End Function

    Private Function CountPiecesBetweenStraight(fromCol As Integer, fromRow As Integer, targetCol As Integer, targetRow As Integer, ignoredPiece As JanggiPiece, ByRef firstPiece As JanggiPiece) As Integer
        Dim stepCol = Math.Sign(targetCol - fromCol)
        Dim stepRow = Math.Sign(targetRow - fromRow)
        Dim col = fromCol + stepCol
        Dim row = fromRow + stepRow
        Dim count = 0
        firstPiece = Nothing

        While col <> targetCol OrElse row <> targetRow
            Dim piece = GetPieceAt(col, row, ignoredPiece)
            If piece IsNot Nothing Then
                count += 1

                If firstPiece Is Nothing Then
                    firstPiece = piece
                End If
            End If

            col += stepCol
            row += stepRow
        End While

        Return count
    End Function

    Private Function GetPieceAt(col As Integer, row As Integer, Optional ignoredPiece As JanggiPiece = Nothing) As JanggiPiece
        For Each piece In janggiPieces
            If piece IsNot ignoredPiece AndAlso piece.Col = col AndAlso piece.Row = row Then
                Return piece
            End If
        Next

        Return Nothing
    End Function

    Private Function IsInsideBoard(col As Integer, row As Integer) As Boolean
        Return col >= 0 AndAlso col <= 8 AndAlso row >= 0 AndAlso row <= 9
    End Function

    Private Function IsCannon(piece As JanggiPiece) As Boolean
        Return piece IsNot Nothing AndAlso (piece.Label = "包" OrElse piece.Label = "砲")
    End Function

    Private Function IsInsideOwnPalace(side As JanggiSide, col As Integer, row As Integer) As Boolean
        If col < 3 OrElse col > 5 Then
            Return False
        End If

        If side = JanggiSide.Red Then
            Return row >= 0 AndAlso row <= 2
        End If

        Return row >= 7 AndAlso row <= 9
    End Function

    Private Function IsInsideAnyPalace(col As Integer, row As Integer) As Boolean
        Return col >= 3 AndAlso col <= 5 AndAlso ((row >= 0 AndAlso row <= 2) OrElse (row >= 7 AndAlso row <= 9))
    End Function

    Private Function IsPalaceDiagonalStep(fromCol As Integer, fromRow As Integer, targetCol As Integer, targetRow As Integer) As Boolean
        If Math.Abs(targetCol - fromCol) <> 1 OrElse Math.Abs(targetRow - fromRow) <> 1 Then
            Return False
        End If

        If Not IsInsideAnyPalace(fromCol, fromRow) OrElse Not IsInsideAnyPalace(targetCol, targetRow) Then
            Return False
        End If

        Dim fromCenterRow = GetPalaceCenterRow(fromRow)
        Dim targetCenterRow = GetPalaceCenterRow(targetRow)
        If fromCenterRow <> targetCenterRow Then
            Return False
        End If

        Return (fromCol = 4 AndAlso fromRow = fromCenterRow) OrElse (targetCol = 4 AndAlso targetRow = targetCenterRow)
    End Function

    Private Function GetPalaceCenterRow(row As Integer) As Integer
        If row >= 0 AndAlso row <= 2 Then Return 1
        If row >= 7 AndAlso row <= 9 Then Return 8
        Return -1
    End Function

    Private Sub DrawPieceAt(g As Graphics, font As Font, brush As Brush, col As Integer, row As Integer, label As String, Optional negativeImage As Boolean = False)
        Dim center = GetBoardPoint(col, row)
        DrawPieceAtPoint(g, font, brush, center.X, center.Y, label, negativeImage)
    End Sub

    Private Sub DrawPieceAtPoint(g As Graphics, font As Font, brush As Brush, x As Single, y As Single, label As String, Optional negativeImage As Boolean = False)
        Dim pieceScale = GetPieceScale(label)
        Dim radius = GetPieceRadius(label)
        Dim rect As New RectangleF(x - radius, y - radius, radius * 2.0F, radius * 2.0F)
        Dim piecePoints = CreateRegularPolygonPoints(x, y, radius, 8, -112.5)
        Dim shadowPoints = CreateRegularPolygonPoints(x + 2.5F, y + 3.0F, radius, 8, -112.5)
        Dim faceStart = If(negativeImage, Color.FromArgb(30, 34, 43), Color.White)
        Dim faceEnd = If(negativeImage, Color.FromArgb(76, 65, 48), Color.FromArgb(211, 203, 184))
        Dim outerColor = If(negativeImage, Color.FromArgb(225, 238, 234, 215), Color.FromArgb(75, 64, 50))
        Dim originalLabelColor = Color.Black
        Dim solidLabelBrush = TryCast(brush, SolidBrush)
        If solidLabelBrush IsNot Nothing Then originalLabelColor = solidLabelBrush.Color
        Dim labelColor = If(negativeImage,
            Color.FromArgb(255 - originalLabelColor.R, 255 - originalLabelColor.G, 255 - originalLabelColor.B),
            originalLabelColor)

        Dim oldSmoothingMode = g.SmoothingMode
        g.SmoothingMode = SmoothingMode.AntiAlias

        Dim tokenShadowColor = If(negativeImage, Color.FromArgb(90, 255, 255, 235), Color.FromArgb(70, 0, 0, 0))
        Using shadowBrush As New SolidBrush(tokenShadowColor)
            g.FillPolygon(shadowBrush, shadowPoints)
        End Using
        Using faceBrush As New LinearGradientBrush(rect, faceStart, faceEnd, LinearGradientMode.ForwardDiagonal)
            g.FillPolygon(faceBrush, piecePoints)
        End Using
        Using outerPen As New Pen(outerColor, 1.6F)
            g.DrawPolygon(outerPen, piecePoints)
        End Using

        ' Light on the upper-left edges and shade on the lower-right edges
        ' create a raised, beveled token without changing its hit area.
        For i = 0 To piecePoints.Length - 1
            Dim nextIndex = (i + 1) Mod piecePoints.Length
            Dim midpointX = (piecePoints(i).X + piecePoints(nextIndex).X) / 2.0F
            Dim midpointY = (piecePoints(i).Y + piecePoints(nextIndex).Y) / 2.0F
            Dim isHighlight = (midpointX - x) + (midpointY - y) < 0
            Dim edgeColor As Color
            If negativeImage Then
                edgeColor = If(isHighlight, Color.FromArgb(190, 8, 10, 14), Color.FromArgb(235, 235, 224, 198))
            Else
                edgeColor = If(isHighlight, Color.FromArgb(245, 255, 255, 255), Color.FromArgb(165, 75, 62, 45))
            End If
            Using edgePen As New Pen(edgeColor, 2.0F)
                g.DrawLine(edgePen, piecePoints(i), piecePoints(nextIndex))
            End Using
        Next

        Dim innerPoints = CreateRegularPolygonPoints(x, y, Math.Max(2.0F, radius - 3.0F), 8, -112.5)
        Dim innerColor = If(negativeImage, Color.FromArgb(125, 0, 0, 0), Color.FromArgb(100, 255, 255, 255))
        Using innerPen As New Pen(innerColor, 1.0F)
            g.DrawPolygon(innerPen, innerPoints)
        End Using

        Using format As New StringFormat()
            format.Alignment = StringAlignment.Center
            format.LineAlignment = StringAlignment.Center
            Using pieceFont As New Font(font.FontFamily, font.Size * pieceScale, font.Style, font.Unit)
                Dim shadowRect = rect
                shadowRect.Offset(1.0F, 1.0F)
                Using textShadow As New SolidBrush(If(negativeImage, Color.FromArgb(130, 0, 0, 0), Color.FromArgb(75, 0, 0, 0)))
                    g.DrawString(label, pieceFont, textShadow, shadowRect, format)
                End Using
                Using activeLabelBrush As New SolidBrush(labelColor)
                    g.DrawString(label, pieceFont, activeLabelBrush, rect, format)
                End Using
            End Using
        End Using

        g.SmoothingMode = oldSmoothingMode
    End Sub

    Private Function GetBoardPoint(col As Integer, row As Integer) As PointF
        Return New PointF(lastBoardStartX + col * lastBoardDX, lastBoardStartY + row * lastBoardDY)
    End Function

    Private Function GetPieceRadius(label As String) As Single
        Return CSng(Math.Max(6.0, Math.Min(lastBoardDX, lastBoardDY) * 0.36 * GetPieceScale(label)))
    End Function

    Private Function GetPieceScale(label As String) As Single
        ' Piece sizes are proportional to strategic value so the player can
        ' instantly judge relative importance at a glance.
        ' king  (楚/漢) : largest  — the game-ending piece
        ' chariot (車)   : very large — most mobile attacker
        ' cannon (砲/包) : large
        ' horse  (馬)   : medium-large
        ' elephant (象) : medium
        ' guard  (士)   : medium-small
        ' soldier (兵/卒): smallest
        Select Case label
            Case "楚", "漢"    ' king
                Return 1.28F
            Case "車"          ' chariot (highest combat value)
                Return 1.15F
            Case "砲", "包"    ' cannon
                Return 1.08F
            Case "馬"          ' horse
                Return 1.01F
            Case "象"          ' elephant
                Return 0.95F
            Case "士"          ' guard
                Return 0.88F
            Case "兵", "卒"    ' soldier
                Return 0.78F
            Case Else
                Return 0.90F
        End Select
    End Function
    Private Function CreateRegularPolygonPoints(centerX As Single, centerY As Single, radius As Single, sides As Integer, startAngleDegrees As Double) As PointF()
        Dim points(sides - 1) As PointF
        Dim startAngle = startAngleDegrees * Math.PI / 180.0
        Dim stepAngle = Math.PI * 2.0 / sides

        For i = 0 To sides - 1
            Dim angle = startAngle + i * stepAngle
            Dim pointX = centerX + CSng(Math.Cos(angle) * radius)
            Dim pointY = centerY + CSng(Math.Sin(angle) * radius)
            points(i) = New PointF(pointX, pointY)
        Next

        Return points
    End Function

    Private Sub DrawEtchedLine(g As Graphics, x1 As Single, y1 As Single, x2 As Single, y2 As Single)
        Using highlightPen As New Pen(Color.FromArgb(225, 255, 255, 250), 1.0F)
            g.DrawLine(highlightPen, x1 - 1.0F, y1 - 1.0F, x2 - 1.0F, y2 - 1.0F)
        End Using
        Using shadowPen As New Pen(Color.FromArgb(145, 66, 55, 42), 1.6F)
            g.DrawLine(shadowPen, x1 + 1.0F, y1 + 1.0F, x2 + 1.0F, y2 + 1.0F)
        End Using
        Using groovePen As New Pen(Color.FromArgb(118, 94, 68), 1.0F)
            g.DrawLine(groovePen, x1, y1, x2, y2)
        End Using
    End Sub

    Private Sub DrawRaisedBoardFrame(g As Graphics, startX As Integer, startY As Integer, boardWidth As Integer, boardHeight As Integer)
        Dim left = CSng(startX - 3)
        Dim top = CSng(startY - 3)
        Dim right = CSng(startX + boardWidth + 3)
        Dim bottom = CSng(startY + boardHeight + 3)

        Using lightPen As New Pen(Color.FromArgb(245, 255, 255, 250), 3.0F)
            g.DrawLine(lightPen, left, bottom, left, top)
            g.DrawLine(lightPen, left, top, right, top)
        End Using
        Using darkPen As New Pen(Color.FromArgb(150, 65, 52, 38), 3.0F)
            g.DrawLine(darkPen, right, top, right, bottom)
            g.DrawLine(darkPen, right, bottom, left, bottom)
        End Using
    End Sub

    Private Sub DrawPalaceDiagonals(g As Graphics, startX As Integer, palaceTopY As Integer, dx As Integer, dy As Integer)
        Dim leftX = startX + 3 * dx
        Dim rightX = startX + 5 * dx
        Dim topY = palaceTopY
        Dim bottomY = palaceTopY + 2 * dy

        DrawEtchedLine(g, leftX, topY, rightX, bottomY)
        DrawEtchedLine(g, rightX, topY, leftX, bottomY)
    End Sub

    Public Sub ClearPictureBox(pb As PictureBox)
        Dim bmp As New Bitmap(Math.Max(pb.Width, 1), Math.Max(pb.Height, 1))
        Using g = Graphics.FromImage(bmp)
            g.Clear(BoardSurfaceColor)
        End Using
        pb.Image = bmp
        hasBoardGeometry = False
        allJanggiPieces.Clear()
        janggiPieces.Clear()
        capturedRedPieces.Clear()
        capturedBluePieces.Clear()
        savedBoardSnapshot = Nothing
        committedMovePieceIds.Clear()
        lastMovedPieceId = -1
        opponentPreviousMovePieceId = -1
        opponentPreviousMovePieceNegative = False
        RaiseEvent IsThereReset()
        RaiseEvent IsEatenReset()
        RaiseEvent CanGoReset()
        RaiseEvent CanEatReset()
        RaiseEvent CanBeEatenReset()
        draggingPiece = Nothing
        currentTurn = JanggiSide.Blue
        dragMessage = CurrentTurnText
        pb.Invalidate()
    End Sub

    Private Sub DrawCapturedPieces(pb As PictureBox)
        EnsureBitmap(pb)

        If Not hasBoardGeometry Then
            Return
        End If

        Dim bmp = DirectCast(pb.Image, Bitmap)
        Using g = Graphics.FromImage(bmp)
            Using blueBrush As New SolidBrush(Color.Blue)
                Using redBrush As New SolidBrush(Color.Red)
                    Dim trayFontSize As Single = Math.Max(8.0F, Math.Min(lastBoardDX, lastBoardDY) * 0.34F)

                    Using font As New Font("Microsoft YaHei UI", trayFontSize, FontStyle.Bold, GraphicsUnit.Pixel)
                        DrawCapturedTray(g, font, redBrush, capturedRedPieces, lastBoardStartX - Math.Max(34, lastBoardDX), "Red")
                        DrawCapturedTray(g, font, blueBrush, capturedBluePieces, lastBoardStartX + lastBoardDX * 8 + Math.Max(34, lastBoardDX), "Blue")
                    End Using
                End Using
            End Using
        End Using

        pb.Invalidate()
    End Sub

    Private Sub DrawCapturedTray(g As Graphics, font As Font, brush As Brush, labels As List(Of String), x As Integer, title As String)
        If labels.Count = 0 Then
            Return
        End If

        Dim y = lastBoardStartY + Math.Max(10, lastBoardDY \ 2)
        Dim spacing = Math.Max(18, CInt(lastBoardDY * 0.56))

        Using titleFont As New Font("Segoe UI", Math.Max(7.0F, font.Size * 0.58F), FontStyle.Bold, GraphicsUnit.Pixel)
            Dim titleSize = g.MeasureString(title, titleFont)
            g.DrawString(title, titleFont, Brushes.DimGray, x - titleSize.Width / 2.0F, Math.Max(0, y - spacing))
        End Using

        For i = 0 To labels.Count - 1
            DrawPieceAtPoint(g, font, brush, x, y + i * spacing, labels(i))
        Next
    End Sub
End Class
