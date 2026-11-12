Imports System.Text
Imports System.Drawing
Imports System.IO
Imports System.Linq

Public Class Form1
    Private _pictureSizeSyncEnabled As Boolean = False
    Private WithEvents _runtime As New Runtime()
    Private _isUpdatingLayout As Boolean
    Private _isDraggingPiece As Boolean
    Private _isRunningScript As Boolean
    Private _stopRequested As Boolean
    Private _pausedAfterMove As Boolean
    Private _currentKibo As Term = New AtomTerm("emptyKibo")
    Private _humanExplanationRequired As Boolean
    Private _lastMoveWasHuman As Boolean
    Private _lastMoveTerm As Term
    Private ReadOnly _expertExplanationFacts As New List(Of String)()
    Private ReadOnly _newlyRecordedExpertFacts As New List(Of String)()
    Private ReadOnly _pendingInsertionCandidates As New List(Of String)()
    Private _pendingExpertPurpose As String = ""
    Private _lastPurposeInterpretation As String = ""
    Private Const HumanSideAtom As String = "BLUE"
    Private Const ComputerSideAtom As String = "RED"
    Private _isClosing As Boolean
    Private _globalHighlightWord As String = ""
    Private ReadOnly _predicateLineMap As New Dictionary(Of String, Integer)()
    Private ReadOnly _breakpointLines As New HashSet(Of Integer)()
    Private ReadOnly _traceNodeStack As New List(Of TreeNode)()
    Private ReadOnly _whyCallPath As New List(Of String)()
    Private ReadOnly _lastWhyClauses As New List(Of String)()
    Private _uiSnapshot As UiPaneSnapshot
    Private _whyTab As TabPage
    Private _whyQuestionTextBox As TextBox
    Private _askWhyButton As Button
    Private _computerExplanationTextBox As RichTextBox
    Private _whyAnswerTextBox As RichTextBox
    Private _commitLogicButton As ToolStripButton
    Private _moveForMeButton As ToolStripButton
    Private _guideRichTextBox As RichTextBox
    Private _newTermTextBox As TextBox
    Private _addTermButton As Button
    Private _callHierarchyTab As TabPage
    Private _callHierarchyTree As TreeView
    Private _hierarchyNodeStack As New List(Of TreeNode)()
    Private _txtHierarchyFilter As TextBox
    Private _btnExpandHierarchy As Button
    Private _btnCollapseHierarchy As Button
    Private _btnRefreshHierarchy As Button
    Private _btnToggleHierarchyView As Button
    Private _graphicalHierarchyCanvas As GraphicalHierarchyCanvas
    Private _graphicalPanel As Panel
    Private _graphicalGraphicStack As New List(Of HierarchyGraphicNode)()
    Private _computerMovingForHuman As Boolean
    Private _committedSourceText As String = ""
    Private _lastWhyMove As String = ""
    Private _lastWhyDecision As String = ""
    Private _traceSeq As Integer
    Private WithEvents randomPlayTimer As New Timer()
    Private WithEvents automaticPlayTimer As New Timer()
    Private WithEvents pauseReplayTimer As New Timer()
    Private _isPauseReplayAnimating As Boolean
    Private WithEvents opponentMoveBlinkTimer As New Timer()
    Private _opponentMoveBlinkNegative As Boolean
    Private Const MaxRunTurns As Integer = 80
    Private Const MaxTraceRows As Integer = 1200
    Private Const MaxMoveRows As Integer = 1200
    Private Const MaxListRows As Integer = 800

    Private Class UiPaneSnapshot
        Public Property SourceText As String = ""
        Public Property DebugText As String = ""
        Public Property WhyQuestion As String = ""
        Public Property WhyAnswer As String = ""
        Public Property LastWhyMove As String = ""
        Public Property LastWhyDecision As String = ""
        Public Property LastWhyClauses As New List(Of String)()
        Public Property TraceNodes As New List(Of TreeNode)()
        Public Property TraceRows As New List(Of GridRowSnapshot)()
        Public Property MoveRows As New List(Of GridRowSnapshot)()
        Public Property CanGoRows As New List(Of GridRowSnapshot)()
        Public Property CanEatRows As New List(Of GridRowSnapshot)()
        Public Property CanBeEatenRows As New List(Of GridRowSnapshot)()
        Public Property TraceSeq As Integer
        Public Property SelectedInspectorTab As Integer
        Public Property Split1Distance As Integer
        Public Property Split2Distance As Integer
        Public Property Split3Distance As Integer
        Public Property Split4Distance As Integer
    End Class

    Private Class GridRowSnapshot
        Public Property Values As Object()
        Public Property Tag As Object
        Public Property ForeColor As Color
    End Class

    Private Sub ApplyResponsiveLayout()
        If _isUpdatingLayout OrElse Not Me.IsHandleCreated Then Exit Sub

        _isUpdatingLayout = True
        Try
            If SplitContainer1.Panel2Collapsed Then
                SplitContainer4.Panel1MinSize = 200
                SplitContainer4.Panel2MinSize = 400
                SetSplitterDistance(SplitContainer4, Math.Max(220, CInt(SplitContainer4.Width * 0.48)))
                Exit Try
            End If

            SplitContainer1.Panel1MinSize = 80
            SplitContainer1.Panel2MinSize = 120
            SplitContainer2.Panel1MinSize = 160
            SplitContainer2.Panel2MinSize = 240
            SplitContainer3.Panel2Collapsed = True
            SplitContainer4.Panel1MinSize = 200
            SplitContainer4.Panel2MinSize = 400

            SetSplitterDistance(SplitContainer1, Math.Max(140, CInt(SplitContainer1.Height * 0.42)))
            SetSplitterDistance(SplitContainer2, CInt(SplitContainer2.Width * 0.5))
            SetSplitterDistance(SplitContainer4, Math.Max(220, CInt(SplitContainer4.Width * 0.48)))
        Finally
            _isUpdatingLayout = False
        End Try
    End Sub

    Private Sub SetSplitterDistance(splitter As SplitContainer, desiredDistance As Integer)
        Dim maxDistance As Integer

        If splitter.Orientation = Orientation.Horizontal Then
            maxDistance = splitter.Height - splitter.SplitterWidth - splitter.Panel2MinSize
        Else
            maxDistance = splitter.Width - splitter.SplitterWidth - splitter.Panel2MinSize
        End If

        Dim minDistance = splitter.Panel1MinSize
        Dim nextDistance = Math.Max(minDistance, Math.Min(maxDistance, desiredDistance))

        If maxDistance >= minDistance AndAlso splitter.SplitterDistance <> nextDistance Then
            splitter.SplitterDistance = nextDistance
        End If
    End Sub

    Private Sub SyncPictureSizeToParent()
        If picCanvas.Parent Is Nothing Then Exit Sub

        picCanvas.Dock = DockStyle.Fill
        picCanvas.Width = Math.Max(1, picCanvas.Parent.ClientSize.Width)
        picCanvas.Height = Math.Max(1, picCanvas.Parent.ClientSize.Height)
    End Sub

    Private Sub UpdateTurnLabel()
        If Not CanUseToolStripItem(tslTurn) Then Exit Sub

        tslTurn.Text = If(_runtime.CurrentTurnAtom = HumanSideAtom,
            "Human (Blue) Turn", "Computer (Red) Turn")
        tslTurn.ForeColor = _runtime.CurrentTurnColor
    End Sub

    Private Sub SetRandomPlayEnabled(enabled As Boolean)
        If _isClosing OrElse randomPlayTimer Is Nothing Then Exit Sub

        If enabled Then SetAutomaticPlayEnabled(False)

        randomPlayTimer.Enabled = enabled
        If CanUseToolStripItem(btnRandomPlay) Then
            btnRandomPlay.Text = If(enabled, "Stop Random", "Random")
        End If
    End Sub

    Private Sub SetAutomaticPlayEnabled(enabled As Boolean)
        If _isClosing OrElse automaticPlayTimer Is Nothing Then Exit Sub

        If enabled Then SetRandomPlayEnabled(False)

        automaticPlayTimer.Enabled = enabled
        If CanUseToolStripItem(btnAutomaticPlay) Then
            btnAutomaticPlay.Text = If(enabled, "Stop Auto", "Auto Play")
        End If
    End Sub

    Private Sub SetPauseReplayEnabled(enabled As Boolean)
        If pauseReplayTimer Is Nothing Then Exit Sub
        If _isClosing AndAlso enabled Then Exit Sub
        pauseReplayTimer.Enabled = enabled
        If Not enabled Then SetOpponentMoveBlinkEnabled(False)
    End Sub

    Private Sub SetOpponentMoveBlinkEnabled(enabled As Boolean)
        If opponentMoveBlinkTimer Is Nothing Then Exit Sub
        If _isClosing AndAlso enabled Then Exit Sub

        Dim shouldEnable = enabled AndAlso _runtime.HasOpponentPreviousMovePiece
        opponentMoveBlinkTimer.Enabled = shouldEnable
        If Not shouldEnable Then
            _opponentMoveBlinkNegative = False
            _runtime.SetOpponentPreviousMovePieceNegative(picCanvas, False)
        End If
    End Sub

    Private Sub ReplayLastMoveOnce()
        If _isClosing OrElse _isPauseReplayAnimating Then Exit Sub

        _isPauseReplayAnimating = True
        Dim restoreAskButton = CanUseControl(_askWhyButton) AndAlso _askWhyButton.Enabled
        If restoreAskButton Then _askWhyButton.Enabled = False
        Try
            _runtime.AnimateLastCommittedMove(picCanvas)
        Finally
            If restoreAskButton AndAlso CanUseControl(_askWhyButton) Then _askWhyButton.Enabled = True
            _isPauseReplayAnimating = False
        End Try
    End Sub

    Private Sub pauseReplayTimer_Tick(sender As Object, e As EventArgs) Handles pauseReplayTimer.Tick
        If _isClosing OrElse _isRunningScript OrElse Not _pausedAfterMove Then Exit Sub
        ReplayLastMoveOnce()
    End Sub

    Private Sub opponentMoveBlinkTimer_Tick(sender As Object, e As EventArgs) Handles opponentMoveBlinkTimer.Tick
        If _isClosing OrElse _isRunningScript OrElse Not _pausedAfterMove OrElse _isPauseReplayAnimating Then Exit Sub
        If Not _runtime.HasOpponentPreviousMovePiece Then
            SetOpponentMoveBlinkEnabled(False)
            Exit Sub
        End If

        _opponentMoveBlinkNegative = Not _opponentMoveBlinkNegative
        _runtime.SetOpponentPreviousMovePieceNegative(picCanvas, _opponentMoveBlinkNegative)
    End Sub

    Private Sub SetDebugText(text As String)
        If Not CanUseControl(rtbDebug) Then Exit Sub

        Try
            rtbDebug.Text = text
            rtbDebug.SelectionStart = rtbDebug.TextLength
            rtbDebug.SelectionLength = 0
            rtbDebug.ScrollToCaret()
            If Not _isRunningScript Then ApplyFactRuleFontStyles(rtbDebug)
            ApplyGlobalHighlightToRichTextBox(rtbDebug)
            rtbDebug.Refresh()
        Catch ex As ObjectDisposedException
            _isClosing = True
        Catch ex As InvalidOperationException
            If rtbDebug.IsDisposed Then
                _isClosing = True
            Else
                Throw
            End If
        End Try
    End Sub

    Private Sub SaveUiPaneSnapshot()
        Dim snapshot As New UiPaneSnapshot With {
            .SourceText = If(CanUseControl(rtbSource), rtbSource.Text, ""),
            .DebugText = If(CanUseControl(rtbDebug), rtbDebug.Text, ""),
            .WhyQuestion = If(CanUseControl(_whyQuestionTextBox), _whyQuestionTextBox.Text, ""),
            .WhyAnswer = If(CanUseControl(_whyAnswerTextBox), _whyAnswerTextBox.Text, ""),
            .LastWhyMove = _lastWhyMove,
            .LastWhyDecision = _lastWhyDecision,
            .TraceSeq = _traceSeq,
            .SelectedInspectorTab = If(CanUseControl(tabInspector), tabInspector.SelectedIndex, 0),
            .Split1Distance = SplitContainer1.SplitterDistance,
            .Split2Distance = SplitContainer2.SplitterDistance,
            .Split3Distance = SplitContainer3.SplitterDistance,
            .Split4Distance = SplitContainer4.SplitterDistance
        }

        If CanUseControl(lstCommands) Then
            For Each node As TreeNode In lstCommands.Nodes
                snapshot.TraceNodes.Add(DirectCast(node.Clone(), TreeNode))
            Next
        End If

        snapshot.LastWhyClauses.AddRange(_lastWhyClauses)

        CaptureGridRows(dgvTrace, snapshot.TraceRows)
        CaptureGridRows(dgvMoves, snapshot.MoveRows)
        CaptureGridRows(dgvCanGo, snapshot.CanGoRows)
        CaptureGridRows(dgvCanEat, snapshot.CanEatRows)
        CaptureGridRows(dgvCanBeEaten, snapshot.CanBeEatenRows)
        _uiSnapshot = snapshot
    End Sub

    Private Sub RestoreUiPaneSnapshot(Optional preserveCurrentSource As Boolean = False)
        If _uiSnapshot Is Nothing Then Exit Sub

        If Not preserveCurrentSource AndAlso CanUseControl(rtbSource) Then
            rtbSource.Text = _uiSnapshot.SourceText
            ApplyFactRuleFontStyles(rtbSource)
        End If

        If CanUseControl(rtbDebug) Then
            rtbDebug.Text = _uiSnapshot.DebugText
            ApplyFactRuleFontStyles(rtbDebug)
            rtbDebug.SelectionStart = rtbDebug.TextLength
            rtbDebug.ScrollToCaret()
        End If

        If CanUseControl(_whyQuestionTextBox) Then
            _whyQuestionTextBox.Text = _uiSnapshot.WhyQuestion
        End If
        If CanUseControl(_whyAnswerTextBox) Then
            _whyAnswerTextBox.Text = _uiSnapshot.WhyAnswer
            ApplyFactRuleFontStyles(_whyAnswerTextBox)
        End If
        _lastWhyMove = _uiSnapshot.LastWhyMove
        _lastWhyDecision = _uiSnapshot.LastWhyDecision
        _lastWhyClauses.Clear()
        _lastWhyClauses.AddRange(_uiSnapshot.LastWhyClauses)

        If CanUseControl(lstCommands) Then
            lstCommands.Nodes.Clear()
            For Each node In _uiSnapshot.TraceNodes
                lstCommands.Nodes.Add(DirectCast(node.Clone(), TreeNode))
            Next
            If lstCommands.Nodes.Count > 0 Then
                lstCommands.SelectedNode = lstCommands.Nodes(lstCommands.Nodes.Count - 1)
                lstCommands.SelectedNode.EnsureVisible()
            End If
        End If

        RestoreGridRows(dgvTrace, _uiSnapshot.TraceRows)
        RestoreGridRows(dgvMoves, _uiSnapshot.MoveRows)
        RestoreGridRows(dgvCanGo, _uiSnapshot.CanGoRows)
        RestoreGridRows(dgvCanEat, _uiSnapshot.CanEatRows)
        RestoreGridRows(dgvCanBeEaten, _uiSnapshot.CanBeEatenRows)
        _traceSeq = _uiSnapshot.TraceSeq
        _traceNodeStack.Clear()

        If CanUseControl(tabInspector) AndAlso _uiSnapshot.SelectedInspectorTab >= 0 AndAlso _uiSnapshot.SelectedInspectorTab < tabInspector.TabPages.Count Then
            tabInspector.SelectedIndex = _uiSnapshot.SelectedInspectorTab
        End If

        RestoreSplitterDistance(SplitContainer1, _uiSnapshot.Split1Distance)
        RestoreSplitterDistance(SplitContainer2, _uiSnapshot.Split2Distance)
        RestoreSplitterDistance(SplitContainer3, _uiSnapshot.Split3Distance)
        RestoreSplitterDistance(SplitContainer4, _uiSnapshot.Split4Distance)
        ApplyGlobalHighlightToRichTextBox(rtbDebug)
        ApplyGlobalHighlightToTreeView()
    End Sub

    Private Sub CaptureGridRows(grid As DataGridView, target As List(Of GridRowSnapshot))
        If Not CanUseControl(grid) Then Exit Sub

        For Each row As DataGridViewRow In grid.Rows
            Dim values(row.Cells.Count - 1) As Object
            For i = 0 To row.Cells.Count - 1
                values(i) = row.Cells(i).Value
            Next

            target.Add(New GridRowSnapshot With {
                .Values = values,
                .Tag = row.Tag,
                .ForeColor = row.DefaultCellStyle.ForeColor
            })
        Next
    End Sub

    Private Sub RestoreGridRows(grid As DataGridView, rows As List(Of GridRowSnapshot))
        If Not CanUseControl(grid) Then Exit Sub

        grid.Rows.Clear()
        For Each rowSnapshot In rows
            Dim rowIndex = grid.Rows.Add(rowSnapshot.Values)
            grid.Rows(rowIndex).Tag = rowSnapshot.Tag
            grid.Rows(rowIndex).DefaultCellStyle.ForeColor = rowSnapshot.ForeColor
        Next

        ScrollGridToLastRow(grid)
    End Sub

    Private Sub RestoreSplitterDistance(splitter As SplitContainer, distance As Integer)
        If distance <= 0 Then Exit Sub
        SetSplitterDistance(splitter, distance)
    End Sub

    Private Function CanUseControl(control As Control) As Boolean
        Return Not _isClosing AndAlso control IsNot Nothing AndAlso Not control.IsDisposed AndAlso control.IsHandleCreated
    End Function

    Private Function CanUseToolStripItem(item As ToolStripItem) As Boolean
        Return Not _isClosing AndAlso item IsNot Nothing AndAlso Not item.IsDisposed
    End Function

    Private Sub resizeTimer_Tick(sender As Object, e As EventArgs) Handles resizeTimer.Tick
        If _isClosing Then Exit Sub

        resizeTimer.Stop()

        If _pictureSizeSyncEnabled Then
            SyncPictureSizeToParent()
        End If

        If CanUseControl(picCanvas) Then
            _runtime.Execute(New AtomTerm("showJanggi"), picCanvas)
        End If
    End Sub
    Private Sub UpdateCommandListFromSource()
        For Each rawLine As String In rtbSource.Lines
            Dim cmd = rawLine.Trim()

            If cmd <> "" Then
                AddCommandTreeNode(cmd)
            End If
        Next
    End Sub

    Private Sub ResetSourceHighlight()
        Dim selectionStart = rtbSource.SelectionStart
        Dim selectionLength = rtbSource.SelectionLength

        rtbSource.SelectAll()
        rtbSource.SelectionBackColor = rtbSource.BackColor
        rtbSource.Select(selectionStart, selectionLength)
        ApplyBreakpointHighlights()
    End Sub

    Private Sub HighlightSourceLine(lineIndex As Integer, color As Color)
        If lineIndex < 0 OrElse lineIndex >= rtbSource.Lines.Length Then Exit Sub

        Dim firstChar = rtbSource.GetFirstCharIndexFromLine(lineIndex)
        If firstChar < 0 Then Exit Sub

        Dim lineLength = rtbSource.Lines(lineIndex).Length
        rtbSource.Select(firstChar, lineLength)
        rtbSource.SelectionBackColor = color
        rtbSource.ScrollToCaret()
        rtbSource.Refresh()
        Application.DoEvents()
    End Sub

    Private Sub ApplyBreakpointHighlights()
        If Not CanUseControl(rtbSource) Then Exit Sub

        Dim selectionStart = rtbSource.SelectionStart
        Dim selectionLength = rtbSource.SelectionLength

        For Each lineIndex In _breakpointLines
            If lineIndex >= 0 AndAlso lineIndex < rtbSource.Lines.Length Then
                Dim firstChar = rtbSource.GetFirstCharIndexFromLine(lineIndex)
                If firstChar >= 0 Then
                    rtbSource.Select(firstChar, rtbSource.Lines(lineIndex).Length)
                    rtbSource.SelectionBackColor = Color.LightCoral
                End If
            End If
        Next

        Dim safeStart = Math.Min(selectionStart, rtbSource.TextLength)
        Dim safeLength = Math.Min(selectionLength, Math.Max(0, rtbSource.TextLength - safeStart))
        rtbSource.Select(safeStart, safeLength)
        rtbSource.Refresh()
    End Sub

    Private Sub ToggleBreakpointAtLine(lineIndex As Integer)
        If lineIndex < 0 OrElse lineIndex >= rtbSource.Lines.Length Then Exit Sub

        If _breakpointLines.Contains(lineIndex) Then
            _breakpointLines.Remove(lineIndex)
        Else
            _breakpointLines.Add(lineIndex)
        End If

        ResetSourceHighlight()
        HighlightSourceLine(lineIndex, If(_breakpointLines.Contains(lineIndex), Color.LightCoral, rtbSource.BackColor))
        SetDebugText("BREAKPOINT:" & Environment.NewLine &
            $"  line {lineIndex + 1}: " & If(_breakpointLines.Contains(lineIndex), "enabled", "disabled"))
    End Sub

    Private Function CheckBreakpointHit(lineIndex As Integer, label As String) As Boolean
        If lineIndex < 0 OrElse Not _breakpointLines.Contains(lineIndex) Then
            Return False
        End If

        HighlightSourceLine(lineIndex, Color.OrangeRed)
        _stopRequested = True
        _runtime.RequestExecutionStop()
        SetRandomPlayEnabled(False)
        SetDebugText("BREAK:" & Environment.NewLine &
            $"  stopped at line {lineIndex + 1}: {label}")

        If Diagnostics.Debugger.IsAttached Then
            Diagnostics.Debugger.Break()
        End If

        Return True
    End Function

    Private Function FindBreakpointLineForPredicate(predicateName As String) As Integer
        For Each lineIndex In _breakpointLines
            If lineIndex >= 0 AndAlso lineIndex < rtbSource.Lines.Length Then
                Dim lineText = rtbSource.Lines(lineIndex).TrimStart()
                If lineText.StartsWith(predicateName & "(", StringComparison.Ordinal) OrElse
                   lineText.StartsWith(predicateName & ".", StringComparison.Ordinal) Then
                    Return lineIndex
                End If
            End If
        Next

        If _predicateLineMap.ContainsKey(predicateName) Then
            Return _predicateLineMap(predicateName)
        End If

        Return -1
    End Function

    Private Sub HighlightSelectedWordOccurrences()
        Dim word = rtbSource.SelectedText
        If String.IsNullOrWhiteSpace(word) Then
            Return
        End If

        HighlightWordEverywhere(word)
    End Sub

    Private Sub HighlightWordEverywhere(word As String)
        If String.IsNullOrWhiteSpace(word) Then
            Return
        End If

        _globalHighlightWord = word.Trim()
        ApplyGlobalHighlightToRichTextBox(rtbSource)
        ApplyBreakpointHighlights()
        ApplyGlobalHighlightToRichTextBox(rtbDebug)
        ApplyGlobalHighlightToRichTextBox(_guideRichTextBox)
        ApplyGlobalHighlightToRichTextBox(_whyAnswerTextBox)
        ApplyGlobalHighlightToRichTextBox(_computerExplanationTextBox)
        ApplyGlobalHighlightToTreeView()
        ApplyGlobalHighlightToGrids()
    End Sub

    Private Sub ApplyGlobalHighlightToGrids()
        If String.IsNullOrWhiteSpace(_globalHighlightWord) Then Return

        For Each grid In New DataGridView() {dgvTrace, dgvMoves, dgvCanGo, dgvCanEat, dgvCanBeEaten}
            If Not CanUseControl(grid) Then Continue For
            Dim firstMatchingRow As DataGridViewRow = Nothing

            For Each row As DataGridViewRow In grid.Rows
                If row.IsNewRow Then Continue For
                Dim matches = False
                For Each cell As DataGridViewCell In row.Cells
                    If cell.Value IsNot Nothing AndAlso cell.Value.ToString().Contains(_globalHighlightWord, StringComparison.OrdinalIgnoreCase) Then
                        matches = True
                        Exit For
                    End If
                Next
                row.DefaultCellStyle.BackColor = If(matches, Color.LightSteelBlue, grid.DefaultCellStyle.BackColor)
                If matches AndAlso firstMatchingRow Is Nothing Then
                    firstMatchingRow = row
                End If
            Next

            If firstMatchingRow IsNot Nothing Then
                Try
                    grid.FirstDisplayedScrollingRowIndex = firstMatchingRow.Index
                Catch ex As Exception
                    ' Ignore layout timing exceptions during fast updates
                End Try
            End If
            grid.Refresh()
        Next
    End Sub

    Private Sub ApplyGlobalHighlightToRichTextBox(box As RichTextBox)
        If Not CanUseControl(box) Then Return

        Dim selectionStart = box.SelectionStart
        Dim selectionLength = box.SelectionLength
        Dim firstMatchIndex As Integer = -1

        box.SelectAll()
        box.SelectionBackColor = box.BackColor

        If String.IsNullOrWhiteSpace(_globalHighlightWord) Then
            box.Select(selectionStart, selectionLength)
            Return
        End If

        Dim text = box.Text
        Dim index = 0

        While index < text.Length
            index = text.IndexOf(_globalHighlightWord, index, StringComparison.OrdinalIgnoreCase)
            If index < 0 Then Exit While

            If IsWholeWordMatch(text, index, _globalHighlightWord.Length) Then
                box.Select(index, _globalHighlightWord.Length)
                box.SelectionBackColor = Color.LightSteelBlue
                If firstMatchIndex < 0 Then firstMatchIndex = index
            End If

            index += Math.Max(_globalHighlightWord.Length, 1)
        End While

        ' Bring the first highlighted match into full view
        If firstMatchIndex >= 0 Then
            box.Select(firstMatchIndex, _globalHighlightWord.Length)
            box.ScrollToCaret()
        Else
            box.Select(selectionStart, selectionLength)
        End If
        box.Refresh()
    End Sub

    Private Function IsWholeWordMatch(text As String, index As Integer, length As Integer) As Boolean
        Dim beforeOk = index = 0 OrElse Not IsIdentifierChar(text(index - 1))
        Dim afterIndex = index + length
        Dim afterOk = afterIndex >= text.Length OrElse Not IsIdentifierChar(text(afterIndex))

        Return beforeOk AndAlso afterOk
    End Function

    Private Function IsIdentifierChar(ch As Char) As Boolean
        Return Char.IsLetterOrDigit(ch) OrElse ch = "_"c
    End Function

    Private Function GetWordAtRichTextPoint(box As RichTextBox, point As Point) As String
        Dim index = box.GetCharIndexFromPosition(point)
        If index < 0 OrElse index >= box.TextLength Then
            Return ""
        End If

        Dim text = box.Text
        If Not IsIdentifierChar(text(index)) Then
            Return ""
        End If

        Dim start = index
        While start > 0 AndAlso IsIdentifierChar(text(start - 1))
            start -= 1
        End While

        Dim finish = index
        While finish + 1 < text.Length AndAlso IsIdentifierChar(text(finish + 1))
            finish += 1
        End While

        Return text.Substring(start, finish - start + 1)
    End Function

    Private Function GetWordAtListPoint(text As String, x As Integer) As String
        If text = "" Then
            Return ""
        End If

        Dim approximateIndex = Math.Max(0, Math.Min(text.Length - 1, x \ Math.Max(1, TextRenderer.MeasureText("M", lstCommands.Font).Width)))
        If Not IsIdentifierChar(text(approximateIndex)) Then
            Return ""
        End If

        Dim start = approximateIndex
        While start > 0 AndAlso IsIdentifierChar(text(start - 1))
            start -= 1
        End While

        Dim finish = approximateIndex
        While finish + 1 < text.Length AndAlso IsIdentifierChar(text(finish + 1))
            finish += 1
        End While

        Return text.Substring(start, finish - start + 1)
    End Function

    Private Sub ApplyGlobalHighlightToTreeView()
        If String.IsNullOrWhiteSpace(_globalHighlightWord) Then Return

        For Each tree In New TreeView() {lstCommands, _callHierarchyTree}
            If Not CanUseControl(tree) Then Continue For
            Dim firstMatch As TreeNode = Nothing
            For Each node As TreeNode In tree.Nodes
                ApplyGlobalHighlightToTreeNode(node, firstMatch)
            Next
            If firstMatch IsNot Nothing Then
                firstMatch.EnsureVisible()
            End If
            tree.Refresh()
        Next
    End Sub

    Private Sub ApplyGlobalHighlightToTreeNode(node As TreeNode, Optional ByRef firstMatch As TreeNode = Nothing)
        If node Is Nothing Then Return

        Dim matches = _globalHighlightWord <> "" AndAlso ContainsWholeWord(node.Text, _globalHighlightWord)
        node.BackColor = If(matches, Color.LightSteelBlue, lstCommands.BackColor)

        If matches AndAlso firstMatch Is Nothing Then
            firstMatch = node
        End If

        For Each child As TreeNode In node.Nodes
            ApplyGlobalHighlightToTreeNode(child, firstMatch)
        Next
    End Sub

    Private Function GetStatementStartLines(lines As String()) As List(Of Integer)
        Dim statementLines As New List(Of Integer)()
        Dim currentStart = -1

        For i = 0 To lines.Length - 1
            If lines(i).Trim() <> "" AndAlso currentStart = -1 Then
                currentStart = i
            End If

            If currentStart <> -1 AndAlso lines(i).Contains(".") Then
                statementLines.Add(currentStart)
                currentStart = -1
            End If
        Next

        Return statementLines
    End Function

    Private Sub BuildPredicateLineMap(terms As List(Of Term), statementLines As List(Of Integer))
        _predicateLineMap.Clear()

        For i = 0 To Math.Min(terms.Count, statementLines.Count) - 1
            Dim predicateName = GetTermPredicateName(terms(i))
            If predicateName <> "" AndAlso Not _predicateLineMap.ContainsKey(predicateName) Then
                _predicateLineMap(predicateName) = statementLines(i)
            End If
        Next
    End Sub

    Private Function GetTermPredicateName(term As Term) As String
        Dim clause = TryCast(term, ClauseTerm)
        If clause IsNot Nothing Then
            Return GetTermPredicateName(clause.Head)
        End If

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

    Private Sub Runtime_TraceGoal(predicateName As String, displayText As String, depth As Integer) Handles _runtime.TraceGoal
        If _isClosing Then Exit Sub

        RecordWhyCall(displayText, depth)

        If _predicateLineMap.ContainsKey(predicateName) Then
            Dim sourceLine = FindBreakpointLineForPredicate(predicateName)
            HighlightSourceLine(sourceLine, Color.LightGreen)
            If CheckBreakpointHit(sourceLine, displayText) Then
                Return
            End If
        End If

        AddTraceTreeNode(displayText, depth)
        AddInspectorTraceRow(predicateName, displayText, depth)
        UpdateTurnLabel()
        Application.DoEvents()
    End Sub

    Private Sub RecordWhyCall(displayText As String, depth As Integer)
        If displayText.StartsWith("success:", StringComparison.OrdinalIgnoreCase) OrElse
           displayText.StartsWith("fail", StringComparison.OrdinalIgnoreCase) Then
            Exit Sub
        End If

        Dim safeDepth = Math.Max(0, depth)
        While _whyCallPath.Count > safeDepth
            _whyCallPath.RemoveAt(_whyCallPath.Count - 1)
        End While
        While _whyCallPath.Count <= safeDepth
            _whyCallPath.Add("")
        End While
        _whyCallPath(safeDepth) = displayText
    End Sub

    Private Sub Runtime_MoveCommitted(description As String, decisionReason As String) Handles _runtime.MoveCommitted
        SetPauseReplayEnabled(False)
        _lastWhyMove = description
        _lastWhyDecision = decisionReason
        _lastWhyClauses.Clear()
        For Each clauseCall In _whyCallPath
            If clauseCall <> "" AndAlso (_lastWhyClauses.Count = 0 OrElse _lastWhyClauses(_lastWhyClauses.Count - 1) <> clauseCall) Then
                _lastWhyClauses.Add(clauseCall)
            End If
        Next

        _stopRequested = True
        _pausedAfterMove = True
        Dim moveTerm = _runtime.GetLastCommittedMoveTerm()
        If moveTerm IsNot Nothing Then
            _currentKibo = New CompoundTerm("kibo", New List(Of Term) From {_currentKibo, moveTerm})
            _lastMoveTerm = moveTerm
            Dim move = TryCast(moveTerm, CompoundTerm)
            Dim side = If(move Is Nothing OrElse move.Arguments.Count = 0,
                Nothing, TryCast(move.Arguments(0), AtomTerm))
            _lastMoveWasHuman = side IsNot Nothing AndAlso
                side.Name = HumanSideAtom AndAlso
                Not _computerMovingForHuman
            If _lastMoveWasHuman Then
                _newlyRecordedExpertFacts.Clear()
                _pendingInsertionCandidates.Clear()
                _pendingExpertPurpose = ""
                _lastPurposeInterpretation = ""
                _humanExplanationRequired = True
                If CanUseControl(_computerExplanationTextBox) Then
                    _computerExplanationTextBox.Text = "Waiting for the computer's next move."
                End If
                If CanUseControl(_whyQuestionTextBox) Then _whyQuestionTextBox.Enabled = True
                If CanUseControl(_askWhyButton) Then
                    _askWhyButton.Enabled = True
                    _askWhyButton.Text = "Record Explanation Now"
                End If
                If CanUseControl(_newTermTextBox) Then _newTermTextBox.Enabled = True
                If CanUseControl(_addTermButton) Then _addTermButton.Enabled = True
            Else
                If CanUseControl(_computerExplanationTextBox) Then
                    _computerExplanationTextBox.Text = _lastWhyDecision
                End If
                If CanUseControl(_whyQuestionTextBox) Then
                    _whyQuestionTextBox.Clear()
                    _whyQuestionTextBox.Enabled = False
                End If
                If CanUseControl(_askWhyButton) Then
                    _askWhyButton.Enabled = False
                    _askWhyButton.Text = "Computer Reason Shown Below"
                End If
                If CanUseControl(_newTermTextBox) Then _newTermTextBox.Enabled = False
                If CanUseControl(_addTermButton) Then _addTermButton.Enabled = False
            End If
        End If
        SetRandomPlayEnabled(False)
        _runtime.SaveJanggiGameState()
        ReplayLastMoveOnce()

        UpdateFactsAndClausesPane()
        ' Do not forcibly switch selected tab when a new move is created;
        ' preserve whatever tab/view the user is currently looking at.

        If _lastMoveWasHuman AndAlso CanUseControl(_whyQuestionTextBox) Then
            _whyQuestionTextBox.Clear()
            _whyQuestionTextBox.Focus()
        End If

        SaveUiPaneSnapshot()
        UpdateTurnLabel()
        SetPauseReplayEnabled(True)
        SetOpponentMoveBlinkEnabled(True)
    End Sub

    Private Sub AddInspectorTraceRow(predicateName As String, displayText As String, depth As Integer)
        If Not CanUseControl(dgvTrace) Then Exit Sub

        _traceSeq += 1
        dgvTrace.Rows.Add(_traceSeq, depth, predicateName, displayText)
        TrimGridRows(dgvTrace, MaxTraceRows)
        ScrollGridToLastRow(dgvTrace)
    End Sub

    Private Sub Runtime_IsThereReset() Handles _runtime.IsThereReset
        If Not CanUseControl(dgvMoves) Then Exit Sub

        dgvMoves.Rows.Clear()
    End Sub

    Private Sub Runtime_IsThereChanged(pieceId As Integer, kind As String, pieceType As String, row As Integer, col As Integer, living As Integer) Handles _runtime.IsThereChanged
        If Not CanUseControl(dgvMoves) Then Exit Sub

        Dim targetRow As DataGridViewRow = Nothing
        For Each gridRow As DataGridViewRow In dgvMoves.Rows
            If gridRow.Tag IsNot Nothing AndAlso CInt(gridRow.Tag) = pieceId Then
                targetRow = gridRow
                Exit For
            End If
        Next

        If targetRow Is Nothing Then
            Dim rowIndex = dgvMoves.Rows.Add(pieceId, "isThere", kind, pieceType, GetInspectorPieceValue(pieceType), row, col, living)
            targetRow = dgvMoves.Rows(rowIndex)
            targetRow.Tag = pieceId
        Else
            targetRow.Cells("Kind").Value = kind
            targetRow.Cells("Type").Value = pieceType
            targetRow.Cells("Value").Value = GetInspectorPieceValue(pieceType)
            targetRow.Cells("Row").Value = row
            targetRow.Cells("Col").Value = col
            targetRow.Cells("Detail").Value = living
        End If

        targetRow.DefaultCellStyle.ForeColor = If(living = 1, Color.Black, Color.Gray)
        TrimGridRows(dgvMoves, MaxMoveRows)
        dgvMoves.Sort(dgvMoves.Columns("Value"), ComponentModel.ListSortDirection.Descending)
        If dgvMoves.Rows.Count > 0 Then
            dgvMoves.ClearSelection()
            dgvMoves.Rows(0).Selected = True
            dgvMoves.FirstDisplayedScrollingRowIndex = 0
        End If
    End Sub

    Private Function GetInspectorPieceValue(pieceType As String) As Integer
        Select Case pieceType.ToLowerInvariant()
            Case "king"
                Return 1000
            Case "chariot"
                Return 90
            Case "cannon"
                Return 50
            Case "horse"
                Return 40
            Case "elephant"
                Return 30
            Case "guard"
                Return 20
            Case "soldier"
                Return 10
            Case Else
                Return 0
        End Select
    End Function

    Private Sub Runtime_CanGoReset() Handles _runtime.CanGoReset
        If Not CanUseControl(dgvCanGo) Then Exit Sub

        dgvCanGo.Rows.Clear()
    End Sub

    Private Sub Runtime_CanGoChanged(kind As String, piece As String, fromPos As String, toPos As String) Handles _runtime.CanGoChanged
        If Not CanUseControl(dgvCanGo) Then Exit Sub

        dgvCanGo.Rows.Add("canGo", kind, piece, fromPos, toPos)
        TrimGridRows(dgvCanGo, MaxMoveRows)
        ScrollGridToLastRow(dgvCanGo)
    End Sub

    Private Sub Runtime_CanEatReset() Handles _runtime.CanEatReset
        If Not CanUseControl(dgvCanEat) Then Exit Sub

        dgvCanEat.Rows.Clear()
    End Sub

    Private Sub Runtime_CanEatChanged(kind As String, piece As String, fromPos As String, toPos As String, targetKind As String, targetPiece As String) Handles _runtime.CanEatChanged
        If Not CanUseControl(dgvCanEat) Then Exit Sub

        dgvCanEat.Rows.Add("canEat", kind, piece, fromPos, toPos, targetKind, targetPiece)
        TrimGridRows(dgvCanEat, MaxMoveRows)
        ScrollGridToLastRow(dgvCanEat)
    End Sub

    Private Sub Runtime_CanBeEatenReset() Handles _runtime.CanBeEatenReset
        If Not CanUseControl(dgvCanBeEaten) Then Exit Sub

        dgvCanBeEaten.Rows.Clear()
    End Sub

    Private Sub Runtime_CanBeEatenChanged(kind As String, piece As String, atPos As String, byKind As String, byPiece As String, fromPos As String) Handles _runtime.CanBeEatenChanged
        If Not CanUseControl(dgvCanBeEaten) Then Exit Sub

        dgvCanBeEaten.Rows.Add("canBeEaten", kind, piece, atPos, byKind, byPiece, fromPos)
        TrimGridRows(dgvCanBeEaten, MaxMoveRows)
        ScrollGridToLastRow(dgvCanBeEaten)
    End Sub

    Private Sub TrimGridRows(grid As DataGridView, maxRows As Integer)
        While grid.Rows.Count > maxRows
            grid.Rows.RemoveAt(0)
        End While
    End Sub

    Private Sub TrimTreeRows(tree As TreeView, maxRows As Integer)
        While CountTreeNodes(tree.Nodes) > maxRows AndAlso tree.Nodes.Count > 0
            tree.Nodes.RemoveAt(0)
        End While
    End Sub

    Private Function CountTreeNodes(nodes As TreeNodeCollection) As Integer
        Dim total = 0

        For Each node As TreeNode In nodes
            total += 1 + CountTreeNodes(node.Nodes)
        Next

        Return total
    End Function

    Private Sub AddCommandTreeNode(text As String)
        If Not CanUseControl(lstCommands) Then Exit Sub

        Dim node = lstCommands.Nodes.Add(text)
        node.Tag = text
        lstCommands.SelectedNode = node
        ApplyGlobalHighlightToTreeNode(node)
    End Sub

    Private Sub AddTraceTreeNode(text As String, depth As Integer)
        If Not CanUseControl(lstCommands) Then Exit Sub

        While _traceNodeStack.Count > depth
            _traceNodeStack.RemoveAt(_traceNodeStack.Count - 1)
        End While

        Dim parentNode As TreeNode = Nothing
        For i As Integer = _traceNodeStack.Count - 1 To 0 Step -1
            If _traceNodeStack(i) IsNot Nothing Then
                parentNode = _traceNodeStack(i)
                Exit For
            End If
        Next

        Dim node As TreeNode
        If depth <= 0 OrElse parentNode Is Nothing Then
            node = lstCommands.Nodes.Add(text)
        Else
            node = parentNode.Nodes.Add(text)
        End If

        node.Tag = text
        While _traceNodeStack.Count <= depth
            _traceNodeStack.Add(Nothing)
        End While
        _traceNodeStack(depth) = node
        node.EnsureVisible()
        lstCommands.SelectedNode = node
        ApplyGlobalHighlightToTreeNode(node)
        TrimTreeRows(lstCommands, MaxListRows)
        lstCommands.Refresh()

        ' Also update the Call Hierarchy tab tree
        If CanUseControl(_callHierarchyTree) Then
            While _hierarchyNodeStack.Count > depth
                _hierarchyNodeStack.RemoveAt(_hierarchyNodeStack.Count - 1)
            End While

            Dim hParentNode As TreeNode = Nothing
            For i As Integer = _hierarchyNodeStack.Count - 1 To 0 Step -1
                If _hierarchyNodeStack(i) IsNot Nothing Then
                    hParentNode = _hierarchyNodeStack(i)
                    Exit For
                End If
            Next

            Dim hNode As TreeNode
            If depth <= 0 OrElse hParentNode Is Nothing Then
                hNode = _callHierarchyTree.Nodes.Add(text)
            Else
                hNode = hParentNode.Nodes.Add(text)
            End If

            hNode.Tag = text
            While _hierarchyNodeStack.Count <= depth
                _hierarchyNodeStack.Add(Nothing)
            End While
            _hierarchyNodeStack(depth) = hNode

            If text.StartsWith("success:", StringComparison.OrdinalIgnoreCase) Then
                hNode.ForeColor = Color.DarkGreen
            ElseIf text.StartsWith("fail", StringComparison.OrdinalIgnoreCase) Then
                hNode.ForeColor = Color.Firebrick
            Else
                hNode.ForeColor = Color.DarkBlue
            End If

            TrimTreeRows(_callHierarchyTree, MaxListRows * 2)
        End If

        ' Also mirror into Graphical Hierarchy Canvas
        If CanUseControl(_graphicalHierarchyCanvas) Then
            While _graphicalGraphicStack.Count > depth
                _graphicalGraphicStack.RemoveAt(_graphicalGraphicStack.Count - 1)
            End While

            Dim statusType = "goal"
            If text.StartsWith("success:", StringComparison.OrdinalIgnoreCase) Then
                statusType = "success"
            ElseIf text.StartsWith("fail", StringComparison.OrdinalIgnoreCase) Then
                statusType = "fail"
            End If

            Dim gNode As New HierarchyGraphicNode With {
                .Text = text,
                .Depth = depth,
                .Status = statusType
            }

            If depth <= 0 OrElse _graphicalGraphicStack.Count = 0 Then
                _graphicalHierarchyCanvas.RootNodes.Add(gNode)
            Else
                _graphicalGraphicStack(_graphicalGraphicStack.Count - 1).Children.Add(gNode)
            End If

            While _graphicalGraphicStack.Count <= depth
                _graphicalGraphicStack.Add(Nothing)
            End While
            _graphicalGraphicStack(depth) = gNode
            _graphicalHierarchyCanvas.RecalculateLayout()
            _graphicalHierarchyCanvas.Invalidate()
        End If
    End Sub

    Private Sub ConfigureInspectorGrids()
        ConfigureGrid(dgvTrace)
        dgvTrace.Columns.Clear()
        dgvTrace.Columns.Add("Seq", "#")
        dgvTrace.Columns.Add("Depth", "Depth")
        dgvTrace.Columns.Add("Predicate", "Predicate")
        dgvTrace.Columns.Add("Text", "Text")
        dgvTrace.Columns("Text").AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill

        ConfigureGrid(dgvMoves)
        dgvMoves.Columns.Clear()
        dgvMoves.Columns.Add("PieceId", "Id")
        dgvMoves.Columns.Add("Predicate", "Predicate")
        dgvMoves.Columns.Add("Kind", "Kind")
        dgvMoves.Columns.Add("Type", "Type")
        dgvMoves.Columns.Add("Value", "Value")
        dgvMoves.Columns.Add("Row", "Row")
        dgvMoves.Columns.Add("Col", "Col")
        dgvMoves.Columns.Add("Detail", "Detail")
        dgvMoves.Columns("PieceId").Visible = False
        dgvMoves.Columns("Detail").AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill

        ConfigureGrid(dgvCanGo)
        dgvCanGo.Columns.Clear()
        dgvCanGo.Columns.Add("Predicate", "Predicate")
        dgvCanGo.Columns.Add("Kind", "Kind")
        dgvCanGo.Columns.Add("Piece", "Piece")
        dgvCanGo.Columns.Add("Pos1", "pos1")
        dgvCanGo.Columns.Add("Pos2", "pos2")
        dgvCanGo.Columns("Pos2").AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill

        ConfigureGrid(dgvCanEat)
        dgvCanEat.Columns.Clear()
        dgvCanEat.Columns.Add("Predicate", "Predicate")
        dgvCanEat.Columns.Add("Kind", "Kind")
        dgvCanEat.Columns.Add("Piece", "Piece")
        dgvCanEat.Columns.Add("Pos1", "pos1")
        dgvCanEat.Columns.Add("Pos2", "pos2")
        dgvCanEat.Columns.Add("TargetKind", "TargetKind")
        dgvCanEat.Columns.Add("TargetPiece", "TargetPiece")
        dgvCanEat.Columns("TargetPiece").AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill

        ConfigureGrid(dgvCanBeEaten)
        dgvCanBeEaten.Columns.Clear()
        dgvCanBeEaten.Columns.Add("Predicate", "Predicate")
        dgvCanBeEaten.Columns.Add("Kind", "Kind")
        dgvCanBeEaten.Columns.Add("Piece", "Piece")
        dgvCanBeEaten.Columns.Add("AtPos", "at")
        dgvCanBeEaten.Columns.Add("ByKind", "ByKind")
        dgvCanBeEaten.Columns.Add("ByPiece", "ByPiece")
        dgvCanBeEaten.Columns.Add("FromPos", "from")
        dgvCanBeEaten.Columns("FromPos").AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
    End Sub

    Private Sub InitializeWhyInspector()
        If _whyTab IsNot Nothing Then Exit Sub

        _whyTab = New TabPage("Expert Dialogue")

        ' --- Guide panel (live list of known KB terms) ---
        _guideRichTextBox = New RichTextBox() With {
            .BackColor = Color.FromArgb(240, 248, 255),
            .BorderStyle = BorderStyle.FixedSingle,
            .Dock = DockStyle.Fill,
            .Font = New Font("Consolas", 9),
            .ReadOnly = True,
            .ScrollBars = RichTextBoxScrollBars.Vertical,
            .Text = "Known terms will appear here after logic is committed."
        }

        ' --- Human explanation ---
        _whyQuestionTextBox = New TextBox() With {
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 11),
            .Multiline = True,
            .ScrollBars = ScrollBars.Vertical,
            .PlaceholderText = "Type your explanation here using terms from the guide above..."
        }

        ' --- Record button ---
        _askWhyButton = New Button() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .Padding = New Padding(8, 4, 8, 4),
            .Text = "Record Explanation Now"
        }

        ' --- New term input + Add button ---
        _newTermTextBox = New TextBox() With {
            .Dock = DockStyle.Fill,
            .Font = New Font("Consolas", 10),
            .PlaceholderText = "e.g.  purposeDictionary(control_center).   or   myRule(iKind, oPos) :- canGo(iKind, _, _, oPos)."
        }
        _addTermButton = New Button() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .Padding = New Padding(10, 4, 10, 4),
            .Text = "Add to KB"
        }

        ' --- Computer explanation ---
        _computerExplanationTextBox = New RichTextBox() With {
            .BackColor = Color.FromArgb(242, 247, 255),
            .BorderStyle = BorderStyle.FixedSingle,
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 10),
            .ReadOnly = True,
            .Text = "The computer's explanation will appear after its move."
        }

        ' --- Corresponding facts pane ---
        _whyAnswerTextBox = New RichTextBox() With {
            .BackColor = Color.FromArgb(250, 250, 246),
            .BorderStyle = BorderStyle.FixedSingle,
            .Dock = DockStyle.Fill,
            .Font = New Font("Consolas", 10),
            .HideSelection = False,
            .ReadOnly = True,
            .Text = "Make a legal Blue move. Corresponding facts and clauses will appear here."
        }

        ' --- Labels ---
        Dim guideLabel As New Label() With {
            .AutoSize = True, .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .Padding = New Padding(0, 4, 0, 1),
            .Text = "Guide  --  explain your move using the terms listed below:",
            .TextAlign = ContentAlignment.MiddleLeft
        }
        Dim explanationLabel As New Label() With {
            .AutoSize = True, .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .Padding = New Padding(0, 6, 0, 1),
            .Text = "Your explanation  (Human expert  --  use terms from the guide):",
            .TextAlign = ContentAlignment.MiddleLeft
        }
        Dim newTermLabel As New Label() With {
            .AutoSize = True, .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .Padding = New Padding(0, 6, 0, 1),
            .Text = "Define a new fact or rule -- then click Add to KB  (Ctrl+Enter):",
            .TextAlign = ContentAlignment.MiddleLeft
        }
        Dim computerExplanationLabel As New Label() With {
            .AutoSize = True, .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .Padding = New Padding(0, 6, 0, 1),
            .Text = "Why the computer chose its move:",
            .TextAlign = ContentAlignment.MiddleLeft
        }
        Dim factsLabel As New Label() With {
            .AutoSize = True, .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .Padding = New Padding(0, 6, 0, 1),
            .Text = "Corresponding facts and clauses:",
            .TextAlign = ContentAlignment.MiddleLeft
        }

        ' New-term row:  [_newTermTextBox  |  Add to KB]
        Dim newTermPanel As New TableLayoutPanel() With {
            .ColumnCount = 2, .Dock = DockStyle.Fill,
            .Margin = New Padding(0), .RowCount = 1
        }
        newTermPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        newTermPanel.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        newTermPanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        newTermPanel.Controls.Add(_newTermTextBox, 0, 0)
        newTermPanel.Controls.Add(_addTermButton, 1, 0)

        ' Main layout  (11 rows)
        '   0 AutoSize  guideLabel
        '   1 25%       _guideRichTextBox
        '   2 AutoSize  explanationLabel
        '   3 15%       _whyQuestionTextBox
        '   4 AutoSize  _askWhyButton
        '   5 AutoSize  newTermLabel
        '   6 AutoSize  newTermPanel
        '   7 AutoSize  computerExplanationLabel
        '   8 14%       _computerExplanationTextBox
        '   9 AutoSize  factsLabel
        '  10 46%       _whyAnswerTextBox
        Dim whyLayout As New TableLayoutPanel() With {
            .ColumnCount = 1, .Dock = DockStyle.Fill,
            .Padding = New Padding(8), .RowCount = 11
        }
        whyLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        whyLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        whyLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 25.0F))
        whyLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        whyLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 15.0F))
        whyLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        whyLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        whyLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        whyLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        whyLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 14.0F))
        whyLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        whyLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 46.0F))
        whyLayout.Controls.Add(guideLabel, 0, 0)
        whyLayout.Controls.Add(_guideRichTextBox, 0, 1)
        whyLayout.Controls.Add(explanationLabel, 0, 2)
        whyLayout.Controls.Add(_whyQuestionTextBox, 0, 3)
        whyLayout.Controls.Add(_askWhyButton, 0, 4)
        whyLayout.Controls.Add(newTermLabel, 0, 5)
        whyLayout.Controls.Add(newTermPanel, 0, 6)
        whyLayout.Controls.Add(computerExplanationLabel, 0, 7)
        whyLayout.Controls.Add(_computerExplanationTextBox, 0, 8)
        whyLayout.Controls.Add(factsLabel, 0, 9)
        whyLayout.Controls.Add(_whyAnswerTextBox, 0, 10)
        _whyTab.Controls.Add(whyLayout)
        tabInspector.TabPages.Add(_whyTab)
        AddHandler _askWhyButton.Click, AddressOf AskWhyButton_Click
        AddHandler _whyQuestionTextBox.KeyDown, AddressOf WhyQuestionTextBox_KeyDown
        AddHandler _addTermButton.Click, AddressOf AddTermButton_Click
        AddHandler _newTermTextBox.KeyDown, AddressOf NewTermTextBox_KeyDown
        AddHandler _guideRichTextBox.MouseDown, AddressOf RichTextBox_RightClickToDefinition
        AddHandler _whyAnswerTextBox.MouseDown, AddressOf RichTextBox_RightClickToDefinition
        AddHandler _computerExplanationTextBox.MouseDown, AddressOf RichTextBox_RightClickToDefinition

        InitializeCallHierarchyTab()
    End Sub

    ' ── Call Hierarchy Tab ──────────────────────────────────────────────────

    Private Sub InitializeCallHierarchyTab()
        If _callHierarchyTab IsNot Nothing Then Exit Sub

        _callHierarchyTab = New TabPage("Call Hierarchy")

        _callHierarchyTree = New TreeView() With {
            .BackColor = Color.FromArgb(252, 253, 255),
            .BorderStyle = BorderStyle.FixedSingle,
            .Dock = DockStyle.Fill,
            .Font = New Font("Consolas", 9.5F),
            .FullRowSelect = True,
            .HideSelection = False,
            .HotTracking = True,
            .ShowLines = True,
            .ShowPlusMinus = True,
            .ShowRootLines = True
        }

        _txtHierarchyFilter = New TextBox() With {
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 9.5F),
            .PlaceholderText = "Filter call hierarchy..."
        }

        _btnExpandHierarchy = New Button() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Font = New Font("Segoe UI", 9.0F),
            .Padding = New Padding(4, 1, 4, 1),
            .Text = "Expand All"
        }

        _btnCollapseHierarchy = New Button() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Font = New Font("Segoe UI", 9.0F),
            .Padding = New Padding(4, 1, 4, 1),
            .Text = "Collapse All"
        }

        _btnRefreshHierarchy = New Button() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .Padding = New Padding(4, 1, 4, 1),
            .Text = "Build KB Structure"
        }

        _btnToggleHierarchyView = New Button() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .Padding = New Padding(4, 1, 4, 1),
            .Text = "🎨 Switch to Tree List View"
        }

        _graphicalHierarchyCanvas = New GraphicalHierarchyCanvas()
        _graphicalPanel = New Panel() With {
            .AutoScroll = True,
            .BackColor = Color.FromArgb(246, 248, 252),
            .BorderStyle = BorderStyle.FixedSingle,
            .Dock = DockStyle.Fill
        }
        _graphicalPanel.Controls.Add(_graphicalHierarchyCanvas)

        Dim topToolbar As New TableLayoutPanel() With {
            .ColumnCount = 5,
            .Dock = DockStyle.Top,
            .Height = 32,
            .Margin = New Padding(0),
            .Padding = New Padding(4, 2, 4, 2)
        }
        topToolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        topToolbar.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        topToolbar.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        topToolbar.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        topToolbar.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        topToolbar.Controls.Add(_txtHierarchyFilter, 0, 0)
        topToolbar.Controls.Add(_btnExpandHierarchy, 1, 0)
        topToolbar.Controls.Add(_btnCollapseHierarchy, 2, 0)
        topToolbar.Controls.Add(_btnRefreshHierarchy, 3, 0)
        topToolbar.Controls.Add(_btnToggleHierarchyView, 4, 0)

        Dim mainLayout As New Panel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(2)
        }
        mainLayout.Controls.Add(_graphicalPanel)
        mainLayout.Controls.Add(_callHierarchyTree)
        mainLayout.Controls.Add(topToolbar)

        _callHierarchyTree.Visible = False
        _graphicalPanel.Visible = True

        _callHierarchyTab.Controls.Add(mainLayout)
        tabInspector.TabPages.Add(_callHierarchyTab)

        AddHandler _btnExpandHierarchy.Click, Sub(s, e) If CanUseControl(_callHierarchyTree) Then _callHierarchyTree.ExpandAll()
        AddHandler _btnCollapseHierarchy.Click, Sub(s, e) If CanUseControl(_callHierarchyTree) Then _callHierarchyTree.CollapseAll()
        AddHandler _btnRefreshHierarchy.Click, Sub(s, e) BuildSourceCodeCallHierarchy()
        AddHandler _btnToggleHierarchyView.Click, AddressOf ToggleHierarchyView_Click
        AddHandler _txtHierarchyFilter.TextChanged, AddressOf HierarchyFilter_TextChanged
        AddHandler _callHierarchyTree.AfterSelect, AddressOf CallHierarchyTree_AfterSelect
        AddHandler _callHierarchyTree.NodeMouseClick, AddressOf TreeView_NodeRightClickToDefinition
        AddHandler lstCommands.NodeMouseClick, AddressOf TreeView_NodeRightClickToDefinition
        AddHandler _graphicalHierarchyCanvas.NodeSelected, AddressOf GraphicalHierarchyCanvas_NodeSelected
        AddHandler _graphicalHierarchyCanvas.NodeRightClicked, AddressOf GraphicalHierarchyCanvas_NodeRightClicked
    End Sub

    Private Sub ToggleHierarchyView_Click(sender As Object, e As EventArgs)
        If Not CanUseControl(_graphicalPanel) OrElse Not CanUseControl(_callHierarchyTree) Then Return
        If _graphicalPanel.Visible Then
            _graphicalPanel.Visible = False
            _callHierarchyTree.Visible = True
            _btnToggleHierarchyView.Text = "🎨 Switch to Graphical Diagram"
        Else
            _graphicalPanel.Visible = True
            _callHierarchyTree.Visible = False
            _btnToggleHierarchyView.Text = "📋 Switch to Tree List View"
        End If
    End Sub

    ''' <summary>Parses the Prolog source code to generate a clean structural tree of rules,
    ''' clause bodies, and facts.</summary>
    Private Sub BuildSourceCodeCallHierarchy()
        If Not CanUseControl(_callHierarchyTree) OrElse Not CanUseControl(rtbSource) Then Return

        _callHierarchyTree.BeginUpdate()
        _callHierarchyTree.Nodes.Clear()

        Dim rootNode = _callHierarchyTree.Nodes.Add("Prolog Source Code Call Hierarchy & Rule Structure")
        rootNode.NodeFont = New Font("Segoe UI", 10.0F, FontStyle.Bold)

        Dim rulesNode = rootNode.Nodes.Add("Rules & Clause Derivations")
        rulesNode.NodeFont = New Font("Segoe UI", 9.5F, FontStyle.Bold)

        Dim factsNode = rootNode.Nodes.Add("Facts & Knowledge Base")
        factsNode.NodeFont = New Font("Segoe UI", 9.5F, FontStyle.Bold)

        Dim lines = rtbSource.Lines
        For lineIdx = 0 To lines.Length - 1
            Dim line = lines(lineIdx).Trim()
            If line = "" OrElse line.StartsWith("%") Then Continue For

            If line.Contains(":-") Then
                Dim parts = line.Split(New String() {":-"}, StringSplitOptions.None)
                Dim head = parts(0).Trim()
                Dim body = parts(1).Trim()

                Dim rNode = rulesNode.Nodes.Add("[Rule]  " & head)
                rNode.Tag = lineIdx + 1
                rNode.ForeColor = Color.Navy

                Dim goals = body.Split(New String() {",", "."}, StringSplitOptions.RemoveEmptyEntries)
                For Each g In goals
                    Dim trimmedG = g.Trim()
                    If trimmedG <> "" Then
                        Dim bNode = rNode.Nodes.Add("  ├─ " & trimmedG)
                        bNode.Tag = lineIdx + 1
                        bNode.ForeColor = Color.DarkSlateGray
                    End If
                Next
            ElseIf line.EndsWith(".") AndAlso Not line.StartsWith("?-") Then
                Dim fNode = factsNode.Nodes.Add("[Fact]  " & line)
                fNode.Tag = lineIdx + 1
                fNode.ForeColor = Color.DarkGreen
            End If
        Next

        rootNode.Expand()
        rulesNode.ExpandAll()
        _callHierarchyTree.EndUpdate()

        ' Build matching Graphical Hierarchy Canvas nodes
        If CanUseControl(_graphicalHierarchyCanvas) Then
            _graphicalHierarchyCanvas.RootNodes.Clear()

            Dim graphicRulesNode As New HierarchyGraphicNode With {
                .Text = "Rules & Clause Derivations",
                .Status = "rule",
                .Depth = 0
            }
            Dim graphicFactsNode As New HierarchyGraphicNode With {
                .Text = "Facts & Knowledge Base",
                .Status = "fact",
                .Depth = 0
            }

            For lineIdx = 0 To lines.Length - 1
                Dim line = lines(lineIdx).Trim()
                If line = "" OrElse line.StartsWith("%") Then Continue For

                If line.Contains(":-") Then
                    Dim parts = line.Split(New String() {":-"}, StringSplitOptions.None)
                    Dim head = parts(0).Trim()
                    Dim body = parts(1).Trim()

                    Dim rNode As New HierarchyGraphicNode With {
                        .Text = "[Rule]  " & head,
                        .LineNumber = lineIdx + 1,
                        .Status = "rule",
                        .Depth = 1
                    }

                    Dim goals = body.Split(New String() {",", "."}, StringSplitOptions.RemoveEmptyEntries)
                    For Each g In goals
                        Dim trimmedG = g.Trim()
                        If trimmedG <> "" Then
                            rNode.Children.Add(New HierarchyGraphicNode With {
                                .Text = trimmedG,
                                .LineNumber = lineIdx + 1,
                                .Status = "goal",
                                .Depth = 2
                            })
                        End If
                    Next
                    graphicRulesNode.Children.Add(rNode)
                ElseIf line.EndsWith(".") AndAlso Not line.StartsWith("?-") Then
                    graphicFactsNode.Children.Add(New HierarchyGraphicNode With {
                        .Text = "[Fact]  " & line,
                        .LineNumber = lineIdx + 1,
                        .Status = "fact",
                        .Depth = 1
                    })
                End If
            Next

            _graphicalHierarchyCanvas.RootNodes.Add(graphicRulesNode)
            _graphicalHierarchyCanvas.RootNodes.Add(graphicFactsNode)
            _graphicalHierarchyCanvas.RecalculateLayout()
            _graphicalHierarchyCanvas.Invalidate()
        End If
    End Sub

    Private Sub CallHierarchyTree_AfterSelect(sender As Object, e As TreeViewEventArgs)
        If e.Node Is Nothing Then Return

        Dim lineNum As Integer = 0
        If e.Node.Tag IsNot Nothing AndAlso Integer.TryParse(e.Node.Tag.ToString(), lineNum) AndAlso lineNum > 0 Then
            HighlightSourceLine(lineNum, Color.LightYellow)
        End If

        Dim targetText = e.Node.Text.Trim()
        For Each prefix In New String() {"[Rule]", "[Fact]", "✓", "✗", "success:", "fail:", "  ├─ "}
            targetText = targetText.Replace(prefix, "").Trim()
        Next

        Dim targetWord As String = targetText
        Dim parenIdx = targetWord.IndexOf("("c)
        If parenIdx > 0 Then
            targetWord = targetWord.Substring(0, parenIdx).Trim()
        End If

        If lineNum <= 0 AndAlso targetWord <> "" AndAlso _predicateLineMap.ContainsKey(targetWord) Then
            HighlightSourceLine(_predicateLineMap(targetWord), Color.LightYellow)
        End If

        If targetWord <> "" Then
            HighlightWordEverywhere(targetWord)
        End If
    End Sub

    Private Sub GraphicalHierarchyCanvas_NodeSelected(node As HierarchyGraphicNode)
        If node Is Nothing Then Return

        ' 1. Highlight source line & scroll to line in Prolog source editor
        If node.LineNumber > 0 AndAlso CanUseControl(rtbSource) Then
            HighlightSourceLine(node.LineNumber, Color.LightYellow)
        End If

        ' 2. Extract target predicate or key term name from clicked item
        Dim targetText = node.Text.Trim()
        For Each prefix In New String() {"[Rule]", "[Fact]", "✓", "✗", "success:", "fail:", "  ├─ "}
            targetText = targetText.Replace(prefix, "").Trim()
        Next

        Dim targetWord As String = targetText
        Dim parenIdx = targetWord.IndexOf("("c)
        If parenIdx > 0 Then
            targetWord = targetWord.Substring(0, parenIdx).Trim()
        End If

        ' If line number was not specified, resolve line from predicate map
        If node.LineNumber <= 0 AndAlso targetWord <> "" AndAlso _predicateLineMap.ContainsKey(targetWord) Then
            HighlightSourceLine(_predicateLineMap(targetWord), Color.LightYellow)
        End If

        ' 3. Bring graphical node into view inside scrollable canvas panel
        If CanUseControl(_graphicalHierarchyCanvas) Then
            _graphicalHierarchyCanvas.ScrollNodeIntoView(node)
        End If

        ' 4. Synchronize and highlight across ALL text panes, logs, trees, and database grids
        If targetWord <> "" Then
            HighlightWordEverywhere(targetWord)
        End If
    End Sub

    Private Sub GraphicalHierarchyCanvas_NodeRightClicked(node As HierarchyGraphicNode)
        If node Is Nothing Then Return
        If node.LineNumber > 0 Then
            HighlightSourceLine(node.LineNumber - 1, Color.LightYellow)
            Return
        End If
        ScrollToPredicateDefinition(node.Text)
    End Sub

    ''' <summary>Scrolls rtbSource directly to the line where targetName is defined
    ''' (rule head, fact, or clause statement) and highlights it.</summary>
    Private Function ScrollToPredicateDefinition(predicateOrTermName As String) As Boolean
        If Not CanUseControl(rtbSource) OrElse String.IsNullOrWhiteSpace(predicateOrTermName) Then Return False

        Dim targetName = predicateOrTermName.Trim()
        For Each prefix In New String() {"[Rule]", "[Fact]", "✓", "✗", "success:", "fail:", "  ├─ "}
            targetName = targetName.Replace(prefix, "").Trim()
        Next

        Dim parenIdx = targetName.IndexOf("("c)
        If parenIdx > 0 Then
            targetName = targetName.Substring(0, parenIdx).Trim()
        End If

        If String.IsNullOrWhiteSpace(targetName) Then Return False

        ' 1. Look up in _predicateLineMap for exact definition line
        If _predicateLineMap.ContainsKey(targetName) Then
            Dim targetLine = _predicateLineMap(targetName)
            HighlightSourceLine(targetLine, Color.LightYellow)
            HighlightWordEverywhere(targetName)
            Return True
        End If

        ' 2. Scan source lines for rule or fact definition: "targetName(" or "targetName :-" or "targetName."
        Dim lines = rtbSource.Lines
        For lineIdx = 0 To lines.Length - 1
            Dim line = lines(lineIdx).Trim()
            If line.StartsWith("%") Then Continue For

            If line.StartsWith(targetName & "(", StringComparison.OrdinalIgnoreCase) OrElse
               line.StartsWith(targetName & " :-", StringComparison.OrdinalIgnoreCase) OrElse
               line.StartsWith(targetName & ".", StringComparison.OrdinalIgnoreCase) OrElse
               line.StartsWith(targetName & " ", StringComparison.OrdinalIgnoreCase) Then

                HighlightSourceLine(lineIdx, Color.LightYellow)
                HighlightWordEverywhere(targetName)
                Return True
            End If
        Next

        ' 3. Fallback: Search for any line containing targetName as a whole word
        For lineIdx = 0 To lines.Length - 1
            If ContainsWholeWord(lines(lineIdx), targetName) Then
                HighlightSourceLine(lineIdx, Color.LightYellow)
                HighlightWordEverywhere(targetName)
                Return True
            End If
        Next

        Return False
    End Function

    Private Sub RichTextBox_RightClickToDefinition(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Right Then Return
        Dim box = TryCast(sender, RichTextBox)
        If box Is Nothing Then Return

        Dim word = GetWordAtRichTextPoint(box, e.Location)
        If Not String.IsNullOrWhiteSpace(word) Then
            ScrollToPredicateDefinition(word)
        End If
    End Sub

    Private Sub TreeView_NodeRightClickToDefinition(sender As Object, e As TreeNodeMouseClickEventArgs)
        If e.Button <> MouseButtons.Right OrElse e.Node Is Nothing Then Return
        Dim lineNum As Integer = 0
        If e.Node.Tag IsNot Nothing AndAlso Integer.TryParse(e.Node.Tag.ToString(), lineNum) AndAlso lineNum > 0 Then
            HighlightSourceLine(lineNum - 1, Color.LightYellow)
            Return
        End If

        ScrollToPredicateDefinition(e.Node.Text)
    End Sub

    Private Sub HierarchyFilter_TextChanged(sender As Object, e As EventArgs)
        If Not CanUseControl(_callHierarchyTree) OrElse Not CanUseControl(_txtHierarchyFilter) Then Return

        Dim query = _txtHierarchyFilter.Text.Trim().ToLowerInvariant()
        _callHierarchyTree.BeginUpdate()
        If query = "" Then
            ResetTreeNodeStyles(_callHierarchyTree.Nodes)
        Else
            FilterTreeNodes(_callHierarchyTree.Nodes, query)
        End If
        _callHierarchyTree.EndUpdate()
    End Sub

    Private Function FilterTreeNodes(nodes As TreeNodeCollection, query As String) As Boolean
        Dim anyMatch = False
        For Each node As TreeNode In nodes
            Dim selfMatch = node.Text.ToLowerInvariant().Contains(query)
            Dim childMatch = FilterTreeNodes(node.Nodes, query)

            If selfMatch OrElse childMatch Then
                node.BackColor = Color.Yellow
                node.ForeColor = Color.Black
                node.Expand()
                anyMatch = True
            Else
                node.BackColor = _callHierarchyTree.BackColor
                node.ForeColor = Color.Black
            End If
        Next
        Return anyMatch
    End Function

    Private Sub ResetTreeNodeStyles(nodes As TreeNodeCollection)
        For Each node As TreeNode In nodes
            node.BackColor = _callHierarchyTree.BackColor
            ResetTreeNodeStyles(node.Nodes)
        Next
    End Sub

    ' ── Guide refresh ────────────────────────────────────────────────────────

    Private Sub RefreshGuide()
        If Not CanUseControl(_guideRichTextBox) Then Return
        _guideRichTextBox.Text = BuildGuideText()
    End Sub

    ''' <summary>Builds the guide text by scanning the committed Prolog source for known
    ''' purpose terms, strategic goals, how-to-win strategies, piece values, and
    ''' relation predicates.  Also shows the variable naming convention and
    ''' examples for defining new terms.</summary>
    Private Function BuildGuideText() As String
        If Not CanUseControl(rtbSource) Then Return ""
        Dim source = rtbSource.Text
        Dim sb As New StringBuilder()

        sb.AppendLine("Explain your move using the known terms below.")
        sb.AppendLine("Use the exact term names from each section.")
        sb.AppendLine("Need a new term?  Type it in the 'Define' field and click Add to KB.")
        sb.AppendLine()

        ' -- Purpose terms (purposeDictionary + purposePolarity) --
        Dim purposes = ExtractFirstAtomArgs(source, "purposeDictionary")
        Dim allPolarityPairs = ExtractArgPairs(source, "purposePolarity")
        Dim offensivePurposes = allPolarityPairs.Where(Function(pair) pair(1).Trim() = "offensive").Select(Function(pair) pair(0)).ToList()
        Dim defensivePurposes = allPolarityPairs.Where(Function(pair) pair(1).Trim() = "defensive").Select(Function(pair) pair(0)).ToList()
        Dim allPurposes As New List(Of String)(purposes)
        For Each pair In allPolarityPairs
            If Not allPurposes.Contains(pair(0)) Then allPurposes.Add(pair(0))
        Next

        If allPurposes.Count > 0 Then
            sb.AppendLine("-- PURPOSE TERMS (name your intention) -----------")
            For Each p In allPurposes
                Dim tag = ""
                If offensivePurposes.Contains(p) Then tag = "  [offensive]"
                If defensivePurposes.Contains(p) Then tag = "  [defensive]"
                sb.AppendLine("  o " & p & tag)
            Next
            sb.AppendLine()
        End If

        ' -- Strategic and tactical goals from whyNode --
        Dim whyNodePairs = ExtractArgPairs(source, "whyNode")
        Dim ultimateGoals = whyNodePairs.Where(Function(pair) pair(1).Trim() = "ultimate_goal").Select(Function(pair) pair(0)).ToList()
        Dim strategicGoals = whyNodePairs.Where(Function(pair) pair(1).Trim() = "strategic_goal").Select(Function(pair) pair(0)).ToList()
        Dim tacticalTerms = whyNodePairs.Where(Function(pair) pair(1).Trim() = "tactical_purpose" OrElse pair(1).Trim() = "positional_purpose").Select(Function(pair) pair(0)).Where(Function(t) Not allPurposes.Contains(t)).ToList()

        If ultimateGoals.Count > 0 OrElse strategicGoals.Count > 0 Then
            sb.AppendLine("-- STRATEGIC GOALS --------------------------------")
            For Each g In ultimateGoals
                sb.AppendLine("  o " & g & "  [ultimate]")
            Next
            For Each g In strategicGoals
                sb.AppendLine("  o " & g)
            Next
            sb.AppendLine()
        End If

        If tacticalTerms.Count > 0 Then
            sb.AppendLine("-- TACTICAL / POSITIONAL PURPOSES -----------------")
            For Each t In tacticalTerms
                sb.AppendLine("  o " & t)
            Next
            sb.AppendLine()
        End If

        ' -- How-to-win strategies --
        Dim strategies = ExtractFirstAtomArgs(source, "how_to_win")
        If strategies.Count > 0 Then
            sb.AppendLine("-- HOW-TO-WIN STRATEGIES --------------------------")
            For Each s In strategies
                sb.AppendLine("  o " & s)
            Next
            sb.AppendLine()
        End If

        ' -- Piece types with values --
        Dim piecePairs = ExtractArgPairs(source, "pieceValue")
        If piecePairs.Count > 0 Then
            sb.AppendLine("-- PIECE TYPES AND VALUES -------------------------")
            For Each pair In piecePairs
                sb.AppendLine("  " & pair(0).PadRight(12) & "= " & pair(1) & " pts")
            Next
            sb.AppendLine()
        End If

        ' -- Relation predicates --
        sb.AppendLine("-- RELATION PREDICATES (describe piece roles) ------")
        sb.AppendLine("  isBlocking(piece, blockedPiece)")
        sb.AppendLine("  isGuarding(piece, guardedPiece)")
        sb.AppendLine("  isSupporting(piece, supportedPiece)")
        sb.AppendLine("  isSteppingStone(piece, forPiece)")
        sb.AppendLine()

        ' -- Variable naming convention --
        sb.AppendLine("-- VARIABLE NAMING CONVENTION ----------------------")
        sb.AppendLine("  iName   = input  (must be bound before the call)")
        sb.AppendLine("  oName   = output (will be unified by the predicate)")
        sb.AppendLine("  xName   = local  (internal working variable)")
        sb.AppendLine("  ALLCAPS = atom constant  e.g. RED  BLUE  WIN")
        sb.AppendLine()

        ' -- Examples for defining new terms --
        sb.AppendLine("-- EXAMPLES: ADDING NEW FACTS / RULES --------------")
        sb.AppendLine("  purposeDictionary(control_center).")
        sb.AppendLine("  purposePolarity(control_center, offensive).")
        sb.AppendLine("  whyNode(control_center, tactical_purpose).")
        sb.AppendLine("  whyEdge(control_center, improve_position, contributes_to).")
        sb.AppendLine("  myCapture(iKind, oPiece, iFrom, oTo) :-")
        sb.AppendLine("      canEat(iKind, oPiece, iFrom, oTo).")

        Return sb.ToString().TrimEnd()
    End Function

    ''' <summary>Extracts the first atom argument from every non-commented occurrence
    ''' of predicateName(...) in the source text, deduplicated.</summary>
    Private Function ExtractFirstAtomArgs(source As String, predicateName As String) As List(Of String)
        Dim results As New List(Of String)()
        Dim marker = predicateName & "("
        Dim searchFrom = 0

        While searchFrom < source.Length
            Dim pos = source.IndexOf(marker, searchFrom, StringComparison.Ordinal)
            If pos < 0 Then Exit While

            ' Skip occurrences that are inside a comment on the same line
            Dim lineStart = pos
            While lineStart > 0 AndAlso source(lineStart - 1) <> Chr(10)
                lineStart -= 1
            End While
            Dim prefix = source.Substring(lineStart, pos - lineStart)
            If prefix.IndexOf("%"c) >= 0 Then
                searchFrom = pos + marker.Length
                Continue While
            End If

            Dim argStart = pos + marker.Length
            Dim argEnd = argStart
            While argEnd < source.Length
                Dim ch = source(argEnd)
                If ch = ","c OrElse ch = ")"c OrElse ch = Chr(13) OrElse ch = Chr(10) Then Exit While
                argEnd += 1
            End While

            Dim arg = source.Substring(argStart, argEnd - argStart).Trim()
            If arg <> "" AndAlso Not results.Contains(arg) Then results.Add(arg)
            searchFrom = pos + marker.Length
        End While

        Return results
    End Function

    ''' <summary>Extracts the first two atom arguments from every non-commented occurrence
    ''' of predicateName(arg1, arg2, ...) in the source text.</summary>
    Private Function ExtractArgPairs(source As String, predicateName As String) As List(Of String())
        Dim results As New List(Of String())()
        Dim marker = predicateName & "("
        Dim searchFrom = 0

        While searchFrom < source.Length
            Dim pos = source.IndexOf(marker, searchFrom, StringComparison.Ordinal)
            If pos < 0 Then Exit While

            ' Skip commented lines
            Dim lineStart = pos
            While lineStart > 0 AndAlso source(lineStart - 1) <> Chr(10)
                lineStart -= 1
            End While
            Dim prefix = source.Substring(lineStart, pos - lineStart)
            If prefix.IndexOf("%"c) >= 0 Then
                searchFrom = pos + marker.Length
                Continue While
            End If

            Dim start = pos + marker.Length

            ' Read arg1
            Dim a1End = start
            While a1End < source.Length
                Dim ch = source(a1End)
                If ch = ","c OrElse ch = ")"c OrElse ch = Chr(13) OrElse ch = Chr(10) Then Exit While
                a1End += 1
            End While
            Dim arg1 = source.Substring(start, a1End - start).Trim()

            ' Read arg2
            Dim arg2 = ""
            If a1End < source.Length AndAlso source(a1End) = ","c Then
                Dim a2Start = a1End + 1
                Dim a2End = a2Start
                While a2End < source.Length
                    Dim ch = source(a2End)
                    If ch = ","c OrElse ch = ")"c OrElse ch = Chr(13) OrElse ch = Chr(10) Then Exit While
                    a2End += 1
                End While
                arg2 = source.Substring(a2Start, a2End - a2Start).Trim()
            End If

            If arg1 <> "" AndAlso arg2 <> "" Then results.Add(New String() {arg1, arg2})
            searchFrom = pos + marker.Length
        End While

        Return results
    End Function

    ' ── New-term input handlers ───────────────────────────────────────────────

    Private Sub AddTermButton_Click(sender As Object, e As EventArgs)
        AddTermToKnowledgeBase()
    End Sub

    Private Sub NewTermTextBox_KeyDown(sender As Object, e As KeyEventArgs)
        If e.Control AndAlso e.KeyCode = Keys.Enter Then
            e.SuppressKeyPress = True
            AddTermToKnowledgeBase()
        End If
    End Sub

    ''' <summary>Validates the new fact/rule typed in _newTermTextBox, inserts it
    ''' before the main query in the source editor, re-commits the logic, and
    ''' refreshes the guide panel.  Reverts on any parse or commit error.</summary>
    Private Sub AddTermToKnowledgeBase()
        If Not CanUseControl(_newTermTextBox) OrElse Not CanUseControl(rtbSource) Then Return

        Dim newEntry = _newTermTextBox.Text.Trim()
        If newEntry = "" Then
            If CanUseControl(_whyAnswerTextBox) Then
                _whyAnswerTextBox.AppendText(
                    Environment.NewLine & Environment.NewLine &
                    "Enter a fact or rule to add (e.g. purposeDictionary(my_new_purpose).).")
            End If
            Return
        End If

        ' Ensure trailing period
        If Not newEntry.EndsWith(".", StringComparison.Ordinal) Then newEntry &= "."

        ' Validate: must parse as exactly one non-query term
        Try
            Dim testLexer As New Lexer(newEntry)
            Dim testTokens = testLexer.Tokenize()
            Dim testParser As New Parser(testTokens)
            Dim testTerms = testParser.ParseProgram()
            If testTerms.Count = 0 OrElse TypeOf testTerms(0) Is QueryTerm Then
                If CanUseControl(_whyAnswerTextBox) Then
                    _whyAnswerTextBox.AppendText(
                        Environment.NewLine & Environment.NewLine &
                        "Cannot add: queries (?-) are not allowed here. Enter a fact or rule.")
                End If
                Return
            End If
        Catch ex As Exception
            If CanUseControl(_whyAnswerTextBox) Then
                _whyAnswerTextBox.AppendText(
                    Environment.NewLine & Environment.NewLine &
                    "Parse error: " & ex.Message)
            End If
            Return
        End Try

        ' Already present?
        If rtbSource.Text.Contains(newEntry, StringComparison.Ordinal) Then
            If CanUseControl(_whyAnswerTextBox) Then
                _whyAnswerTextBox.AppendText(
                    Environment.NewLine & Environment.NewLine &
                    "Already in KB: " & newEntry)
            End If
            _newTermTextBox.Clear()
            Return
        End If

        ' Insert just before the final ?- main(...) query
        Dim oldSource = rtbSource.Text
        Const queryAnchor As String = "?- main("
        If oldSource.Contains(queryAnchor, StringComparison.Ordinal) Then
            rtbSource.Text = oldSource.Replace(
                queryAnchor,
                newEntry & Environment.NewLine & queryAnchor,
                StringComparison.Ordinal)
        Else
            rtbSource.Text = oldSource.TrimEnd() &
                Environment.NewLine & Environment.NewLine & newEntry & Environment.NewLine
        End If

        If CommitCurrentLogic(False) Then
            _newTermTextBox.Clear()
            RefreshGuide()
            If CanUseControl(_whyAnswerTextBox) Then
                _whyAnswerTextBox.AppendText(
                    Environment.NewLine & Environment.NewLine &
                    "Added to KB: " & newEntry & Environment.NewLine &
                    "The guide above has been refreshed.")
            End If
        Else
            ' Revert on failure
            rtbSource.Text = oldSource
            UpdateLogicCommitStatus()
            If CanUseControl(_whyAnswerTextBox) Then
                _whyAnswerTextBox.AppendText(
                    Environment.NewLine & Environment.NewLine &
                    "Add failed -- commit error. Check the debug pane for details. Source reverted.")
            End If
        End If
    End Sub

    ' ─────────────────────────────────────────────────────────────────────────

    Private Sub InitializeLogicCommitUi()
        If _commitLogicButton IsNot Nothing Then Exit Sub

        _commitLogicButton = New ToolStripButton() With {
            .DisplayStyle = ToolStripItemDisplayStyle.Text,
            .Text = "Commit Logic",
            .ToolTipText = "Validate and install the edited Prolog clauses without changing the board (Ctrl+Enter)."
        }
        Dim insertAt = Math.Max(0, ToolStrip1.Items.IndexOf(btnSaveScript) + 1)
        ToolStrip1.Items.Insert(insertAt, _commitLogicButton)
        AddHandler _commitLogicButton.Click, AddressOf CommitLogicButton_Click
        AddHandler rtbSource.TextChanged, AddressOf PrologSource_TextChanged
    End Sub

    Private Sub PrologSource_TextChanged(sender As Object, e As EventArgs)
        UpdateLogicCommitStatus()
    End Sub

    Private Sub UpdateLogicCommitStatus()
        If Not CanUseToolStripItem(_commitLogicButton) OrElse Not CanUseControl(rtbSource) Then Exit Sub

        If rtbSource.Text = _committedSourceText Then
            _commitLogicButton.Text = "Logic Committed"
            _commitLogicButton.ForeColor = Color.DarkGreen
            _commitLogicButton.ToolTipText = "The editor matches the logic currently installed in the interpreter."
        Else
            _commitLogicButton.Text = "Commit Logic *"
            _commitLogicButton.ForeColor = Color.DarkOrange
            _commitLogicButton.ToolTipText = "The editor has uncommitted changes. Click to validate and install them (Ctrl+Enter)."
        End If
    End Sub

    Private Sub CommitLogicButton_Click(sender As Object, e As EventArgs)
        CommitCurrentLogic(True)
    End Sub

    Private Function CommitCurrentLogic(showResult As Boolean) As Boolean
        If _isRunningScript OrElse Not CanUseControl(rtbSource) Then Return False

        Try
            Dim source = rtbSource.Text
            Dim lexer As New Lexer(source)
            Dim tokens = lexer.Tokenize()
            Dim parser As New Parser(tokens)
            Dim terms = parser.ParseProgram()
            Dim clauseCount = _runtime.CommitProgram(terms)
            Dim queryCount = 0
            For Each term In terms
                If TypeOf term Is QueryTerm Then queryCount += 1
            Next

            _committedSourceText = source
            BuildPredicateLineMap(terms, GetStatementStartLines(rtbSource.Lines))
            ApplyFactRuleFontStyles(rtbSource)
            UpdateLogicCommitStatus()
            RefreshGuide()
            BuildSourceCodeCallHierarchy()

            If showResult Then
                SetDebugText("COMMIT LOGIC:" & Environment.NewLine &
                    $"  success: {clauseCount} clauses/facts installed" & Environment.NewLine &
                    $"  queries retained in editor: {queryCount}" & Environment.NewLine &
                    "  current board: preserved" & Environment.NewLine &
                    "  press Run or Go Again to test one move")
            End If
            Return True
        Catch ex As Exception
            If CanUseToolStripItem(_commitLogicButton) Then
                _commitLogicButton.Text = "Commit Failed *"
                _commitLogicButton.ForeColor = Color.Firebrick
                _commitLogicButton.ToolTipText = "The edit was not installed; the previous committed logic remains active."
            End If
            SetDebugText("COMMIT LOGIC ERROR:" & Environment.NewLine &
                "  " & ex.Message & Environment.NewLine &
                "  previous committed logic is still active")
            Return False
        End Try
    End Function

    Private Sub WhyQuestionTextBox_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode <> Keys.Enter OrElse Not e.Control Then Exit Sub

        e.SuppressKeyPress = True
        AskWhyButton_Click(sender, EventArgs.Empty)
    End Sub

    Private Sub AskWhyButton_Click(sender As Object, e As EventArgs)
        If Not CanUseControl(_whyAnswerTextBox) Then Exit Sub

        If _lastWhyMove = "" Then
            _whyAnswerTextBox.Text = "No move has been made yet. The human plays Blue first."
            Exit Sub
        End If

        If Not SaveHumanExplanation(False) Then Exit Sub

        UpdateFactsAndClausesPane()
    End Sub

    Private Function SaveHumanExplanation(allowMissing As Boolean) As Boolean
        If Not _lastMoveWasHuman OrElse Not _humanExplanationRequired Then Return True

        Dim explanation = If(CanUseControl(_whyQuestionTextBox), _whyQuestionTextBox.Text.Trim(), "")
        If explanation = "" AndAlso Not allowMissing Then
            _whyAnswerTextBox.Text = "Enter the purpose of your move, or click Computer Move to continue without an explanation."
            _whyQuestionTextBox.Focus()
            Return False
        End If

        Dim purpose = If(explanation = "", "not_provided", ClassifyExpertPurpose(explanation))
        _lastPurposeInterpretation = purpose
        Dim evidenceFact = $"expertExplanation({_lastMoveTerm}, purpose({purpose}))."
        If Not _expertExplanationFacts.Contains(evidenceFact) Then
            _expertExplanationFacts.Add(evidenceFact)
            _newlyRecordedExpertFacts.Add(evidenceFact)
        End If

        Dim purposeClaim = BuildPurposeClaimFact(purpose, _lastMoveTerm)
        If purposeClaim <> "" Then
            _pendingInsertionCandidates.Clear()
            _pendingExpertPurpose = ""
            If Not _expertExplanationFacts.Contains(purposeClaim) Then
                _expertExplanationFacts.Add(purposeClaim)
                _newlyRecordedExpertFacts.Add(purposeClaim)
            End If
            InsertLearnedFactIntoLogic(purposeClaim)
        ElseIf explanation <> "" Then
            _pendingExpertPurpose = purpose
            PopulateLegalInsertionCandidates()
        End If

        _humanExplanationRequired = False
        _askWhyButton.Text = If(explanation = "", "No Explanation Entered", "Explanation Saved")
        Return True
    End Function

    Private Function ClassifyExpertPurpose(explanation As String) As String
        Dim normalized = ToPrologAtom(explanation)
        If normalized.Contains("captur", StringComparison.Ordinal) AndAlso
           (normalized.Contains("opportun", StringComparison.Ordinal) OrElse
            normalized.Contains("chance", StringComparison.Ordinal)) Then
            Return "gain_more_capturing_opportunity"
        End If

        Return normalized
    End Function

    Private Function BuildPurposeClaimFact(purpose As String, moveTerm As Term) As String
        If purpose <> "gain_more_capturing_opportunity" OrElse moveTerm Is Nothing Then Return ""

        ' The expert is stating an intention. The physical move alone does not prove
        ' that the intended effect was achieved.
        Return $"purposeClaim({_currentKibo}, {moveTerm}, {purpose})."
    End Function

    Private Function InsertLearnedFactIntoLogic(fact As String) As Boolean
        If fact = "" OrElse Not CanUseControl(rtbSource) Then Return False
        If rtbSource.Text.Contains(fact, StringComparison.Ordinal) Then Return True

        Const anchor As String = "purposeDictionary(gain_more_capturing_opportunity)."
        Dim oldSource = rtbSource.Text
        Dim insertion = anchor & Environment.NewLine & fact
        If oldSource.Contains(anchor, StringComparison.Ordinal) Then
            rtbSource.Text = oldSource.Replace(anchor, insertion, StringComparison.Ordinal)
        Else
            rtbSource.Text = insertion & Environment.NewLine & Environment.NewLine & oldSource
        End If

        If CommitCurrentLogic(False) Then Return True

        rtbSource.Text = oldSource
        UpdateLogicCommitStatus()
        Return False
    End Function

    Private Sub PopulateLegalInsertionCandidates()
        _pendingInsertionCandidates.Clear()
        If Not CanUseControl(rtbSource) Then Return

        AddInsertionCandidate("Purpose dictionary entry", "after", "purposeDictionary(gain_more_capturing_opportunity).",
            "define the new purpose's meaning, horizon, evidence, and success measure")
        AddInsertionCandidate("Purpose claim/example", "after", "purposeRequiresEvidence(gain_more_capturing_opportunity, compare_environment_before_after).",
            "store what the expert intended without claiming that the move proved it")
        AddInsertionCandidate("Move-selection strategy", "before", "safeFallbackMove(iKind, oPiece, iFrom, oTo).",
            "give the new rule a priority immediately before the fallback move")
        AddInsertionCandidate("Safety rule", "before", "canGoSafe(iKind, iPiece, Pos1, Pos2) :-",
            "use the new knowledge to accept or reject a candidate move")
        AddInsertionCandidate("Evaluation rule", "before", "pieceValue(king, 1000).",
            "use it when comparing or scoring board situations")
        AddInsertionCandidate("General rule/fact", "before", "?- main(emptyKibo, oNewPos, oResult).",
            "keep it available to queries without assigning a strategy role yet")
    End Sub

    Private Sub AddInsertionCandidate(kind As String, relation As String, anchor As String, meaning As String)
        Dim lineNumber = FindSourceLine(anchor)
        If lineNumber <= 0 Then Return
        _pendingInsertionCandidates.Add($"{kind}: {relation} line {lineNumber} ({anchor}) — {meaning}.")
    End Sub

    Private Function FindSourceLine(fragment As String) As Integer
        If Not CanUseControl(rtbSource) Then Return 0
        For index = 0 To rtbSource.Lines.Length - 1
            If rtbSource.Lines(index).Contains(fragment, StringComparison.Ordinal) Then
                Return index + 1
            End If
        Next
        Return 0
    End Function

    Private Function ToPrologAtom(text As String) As String
        Dim normalized As New StringBuilder()
        For Each ch In text.Trim().ToLowerInvariant()
            If Char.IsLetterOrDigit(ch) Then
                normalized.Append(ch)
            ElseIf normalized.Length > 0 AndAlso normalized(normalized.Length - 1) <> "_"c Then
                normalized.Append("_"c)
            End If
        Next

        Dim result = normalized.ToString().Trim("_"c)
        Return If(result = "", "purpose_unknown", result)
    End Function

    Private Sub UpdateFactsAndClausesPane()
        If Not CanUseControl(_whyAnswerTextBox) Then Exit Sub
        RefreshGuide()

        If _lastMoveTerm Is Nothing Then
            _whyAnswerTextBox.Text = "Human plays Blue. Drag a Blue piece to begin."
            Exit Sub
        End If

        Dim answer As New StringBuilder()
        answer.AppendLine(If(_lastMoveWasHuman, "HUMAN MOVE", "COMPUTER MOVE"))
        answer.AppendLine(_lastWhyMove)
        answer.AppendLine()
        answer.AppendLine("KIBO")
        answer.AppendLine("  " & _currentKibo.ToString())
        answer.AppendLine()
        answer.AppendLine("CORRESPONDING FACTS")
        Dim physicalMove = FormatPhysicalMove(_lastMoveTerm)
        answer.AppendLine("  " & physicalMove & ".")
        answer.AppendLine("  committedToBoard(" & physicalMove & ").")

        If _expertExplanationFacts.Count > 0 Then
            answer.AppendLine()
            answer.AppendLine("EXPERT FACTS (newly recorded entries are inverted)")
            For Each fact In _expertExplanationFacts
                answer.AppendLine("  " & fact)
            Next
        End If

        If _lastPurposeInterpretation = "gain_more_capturing_opportunity" Then
            answer.AppendLine()
            answer.AppendLine("PURPOSE DICTIONARY")
            answer.AppendLine("  purposeDictionary(gain_more_capturing_opportunity).")
            answer.AppendLine("  purposeDomain(gain_more_capturing_opportunity, tactical_positioning).")
            answer.AppendLine("  purposeTimeHorizon(gain_more_capturing_opportunity, next_turn).")
            answer.AppendLine("  purposeDesiredEffect(gain_more_capturing_opportunity, increase_capture_options).")
            answer.AppendLine("  purposeSuccessMeasure(gain_more_capturing_opportunity, capture_opportunity_count).")
            answer.AppendLine("  purposeRequiresEvidence(gain_more_capturing_opportunity, compare_environment_before_after).")
            answer.AppendLine("  purposeWhyNode(gain_more_capturing_opportunity, gain_more_capturing_opportunity).")
            answer.AppendLine()
            answer.AppendLine("PURPOSE STATUS")
            answer.AppendLine("  claimed_by_human_expert; not_yet_verified_from_the_physical_move.")
            answer.AppendLine("  Verification requires iEnvBefore and iEnvAfter.")
        End If

        AppendWhyTree(answer)

        If _pendingInsertionCandidates.Count > 0 Then
            answer.AppendLine()
            answer.AppendLine("INSERTION NEEDS REVIEW")
            answer.AppendLine($"  The purpose '{_pendingExpertPurpose}' is understood as text,")
            answer.AppendLine("  but its fact/rule role is ambiguous. No clause was inserted.")
            answer.AppendLine()
            answer.AppendLine("POSSIBLE LEGAL INSERT POINTS")
            For index = 0 To _pendingInsertionCandidates.Count - 1
                answer.AppendLine($"  {index + 1}. {_pendingInsertionCandidates(index)}")
            Next
        End If

        answer.AppendLine()
        answer.AppendLine("CORRESPONDING CLAUSES")
        answer.AppendLine("  turnFromKibo(kibo(_, move(LastTurn, _, _, _)), Turn) :-")
        answer.AppendLine("      opponent(LastTurn, Turn).")
        If _lastMoveWasHuman Then
            answer.AppendLine("  legalPhysicalMove(iEnv, iPiece, iFrom, iTo).")
            answer.AppendLine("  askHumanExpertWhy(iMove, oPurpose).")
            If _humanExplanationRequired Then
                answer.AppendLine()
                answer.AppendLine("Waiting for the human expert's explanation.")
            Else
                answer.AppendLine()
                answer.AppendLine("Explanation saved. Click Computer Move when ready.")
            End If
        Else
            answer.AppendLine("  Reason: " & _lastWhyDecision)
            If _lastWhyClauses.Count = 0 Then
                answer.AppendLine("  (no Prolog clause path was recorded)")
            Else
                For i = 0 To _lastWhyClauses.Count - 1
                    answer.AppendLine($"  {i + 1}. {_lastWhyClauses(i)}")
                Next
            End If
            answer.AppendLine()
            answer.AppendLine("Human (Blue) may now make the next move.")
        End If

        _whyAnswerTextBox.Text = answer.ToString().TrimEnd()
        ApplyFactRuleFontStyles(_whyAnswerTextBox)
        ApplyLatestMoveHighlight(physicalMove)
        ApplyNewExpertFactHighlight()
        _whyAnswerTextBox.SelectionStart = 0
        _whyAnswerTextBox.SelectionLength = 0
        _whyAnswerTextBox.ScrollToCaret()
    End Sub

    Private Sub ApplyLatestMoveHighlight(physicalMove As String)
        If Not CanUseControl(_whyAnswerTextBox) Then Return

        ' Every committed move reaches this pane through Runtime_MoveCommitted,
        ' whether it was dragged by the human or generated by the computer.
        Dim moveBanner = If(_lastMoveWasHuman, "HUMAN MOVE", "COMPUTER MOVE") &
            Environment.NewLine & _lastWhyMove
        ApplyInvertedWhyText(moveBanner)
        ApplyInvertedWhyText("  " & physicalMove & ".")
        ApplyInvertedWhyText("  committedToBoard(" & physicalMove & ").")
    End Sub

    Private Sub ApplyNewExpertFactHighlight()
        If Not CanUseControl(_whyAnswerTextBox) OrElse _newlyRecordedExpertFacts.Count = 0 Then Return

        For Each fact In _newlyRecordedExpertFacts
            ApplyInvertedWhyText(fact)
        Next
    End Sub

    Private Sub ApplyInvertedWhyText(text As String)
        If Not CanUseControl(_whyAnswerTextBox) OrElse String.IsNullOrEmpty(text) Then Return

        Dim searchFrom = 0
        Do
            Dim start = _whyAnswerTextBox.Text.IndexOf(text, searchFrom, StringComparison.Ordinal)
            If start < 0 Then Exit Do

            _whyAnswerTextBox.Select(start, text.Length)
            _whyAnswerTextBox.SelectionBackColor = Color.Black
            _whyAnswerTextBox.SelectionColor = Color.White
            searchFrom = start + text.Length
        Loop
    End Sub

    Private Sub ApplyFactRuleFontStyles(box As RichTextBox)
        If Not CanUseControl(box) OrElse box.TextLength = 0 Then Return

        Dim savedStart = box.SelectionStart
        Dim savedLength = box.SelectionLength
        Dim baseFont = box.Font

        Try
            Using regularFont As New Font(baseFont.FontFamily, baseFont.Size, FontStyle.Regular, baseFont.Unit),
                  factFont As New Font(baseFont.FontFamily, baseFont.Size, FontStyle.Bold Or FontStyle.Italic, baseFont.Unit)
                box.SelectAll()
                box.SelectionFont = regularFont

                Dim statementStart = -1
                Dim statementHasRule = False
                Dim statementIsQuery = False
                Dim parenthesisDepth = 0

                For lineIndex = 0 To box.Lines.Length - 1
                    Dim line = box.Lines(lineIndex)
                    Dim firstChar = box.GetFirstCharIndexFromLine(lineIndex)
                    If firstChar < 0 Then Continue For

                    Dim relevantText = line
                    If statementStart < 0 Then
                        Dim startOffset = FindPrologStatementStart(line)
                        If startOffset < 0 Then Continue For

                        statementStart = firstChar + startOffset
                        relevantText = line.Substring(startOffset)
                        statementIsQuery = relevantText.StartsWith("?-", StringComparison.Ordinal)
                        statementHasRule = relevantText.Contains(":-", StringComparison.Ordinal)
                        parenthesisDepth = ParenthesisDelta(relevantText)
                    Else
                        statementHasRule = statementHasRule OrElse
                            relevantText.Contains(":-", StringComparison.Ordinal)
                        parenthesisDepth += ParenthesisDelta(relevantText)
                    End If

                    Dim codeOnly = RemoveInlinePrologComment(relevantText).TrimEnd()
                    If parenthesisDepth <= 0 AndAlso codeOnly.EndsWith(".", StringComparison.Ordinal) Then
                        If Not statementHasRule AndAlso Not statementIsQuery Then
                            Dim statementEnd = firstChar + line.Length
                            box.Select(statementStart, Math.Max(0, statementEnd - statementStart))
                            box.SelectionFont = factFont
                        End If

                        statementStart = -1
                        statementHasRule = False
                        statementIsQuery = False
                        parenthesisDepth = 0
                    End If
                Next
            End Using
        Finally
            Dim safeStart = Math.Min(savedStart, box.TextLength)
            Dim safeLength = Math.Min(savedLength, Math.Max(0, box.TextLength - safeStart))
            box.Select(safeStart, safeLength)
        End Try
    End Sub

    Private Function FindPrologStatementStart(line As String) As Integer
        If String.IsNullOrWhiteSpace(line) Then Return -1

        Dim offset = line.Length - line.TrimStart().Length
        If line.AsSpan(offset).StartsWith("loaded:".AsSpan(), StringComparison.OrdinalIgnoreCase) Then
            offset += "loaded:".Length
            While offset < line.Length AndAlso Char.IsWhiteSpace(line(offset))
                offset += 1
            End While
        End If

        If offset >= line.Length Then Return -1
        If line.AsSpan(offset).StartsWith("?-".AsSpan(), StringComparison.Ordinal) Then Return offset
        If Not Char.IsLower(line(offset)) Then Return -1

        Dim index = offset + 1
        While index < line.Length AndAlso
              (Char.IsLetterOrDigit(line(index)) OrElse line(index) = "_"c)
            index += 1
        End While
        While index < line.Length AndAlso Char.IsWhiteSpace(line(index))
            index += 1
        End While

        If index < line.Length AndAlso (line(index) = "("c OrElse line(index) = "."c) Then
            Return offset
        End If
        Return -1
    End Function

    Private Function ParenthesisDelta(text As String) As Integer
        Dim delta = 0
        For Each ch In RemoveInlinePrologComment(text)
            If ch = "("c Then delta += 1
            If ch = ")"c Then delta -= 1
        Next
        Return delta
    End Function

    Private Function RemoveInlinePrologComment(text As String) As String
        Dim percentIndex = text.IndexOf("%"c)
        Dim slashIndex = text.IndexOf("////", StringComparison.Ordinal)
        Dim commentIndex = -1
        If percentIndex >= 0 Then commentIndex = percentIndex
        If slashIndex >= 0 AndAlso (commentIndex < 0 OrElse slashIndex < commentIndex) Then
            commentIndex = slashIndex
        End If
        Return If(commentIndex < 0, text, text.Substring(0, commentIndex))
    End Function

    Private Sub AppendWhyTree(answer As StringBuilder)
        answer.AppendLine()
        answer.AppendLine("WHY TREE (PROVISIONAL)")
        answer.AppendLine("  win_game [ultimate goal]")
        answer.AppendLine("  +-- defeat_opponent_king [strategic goal]")
        answer.AppendLine("  |   +-- create_attack [tactical purpose]")
        answer.AppendLine("  +-- gain_material_advantage [strategic goal]")
        answer.AppendLine("  |   +-- gain_more_capturing_opportunity [tactical purpose]")
        answer.AppendLine("  |       +-- capture_opportunity_count_increased [required evidence]")
        answer.AppendLine("  +-- preserve_own_force [strategic goal]")
        answer.AppendLine("  |   +-- protect_threatened_piece [tactical purpose]")
        answer.AppendLine("  +-- improve_position [strategic goal]")
        answer.AppendLine("      +-- increase_mobility [positional purpose]")
        answer.AppendLine("      +-- control_important_area [positional purpose]")
        answer.AppendLine()
        answer.AppendLine("META-RULE CROSS-LINK")
        answer.AppendLine("  increase_mobility --may_enable--> gain_more_capturing_opportunity")
        answer.AppendLine("  required evidence: a newly legal destination must expose a prospective target.")
        answer.AppendLine()
        answer.AppendLine("META-RULE MOVE QUALITY")
        answer.AppendLine("  defensive purpose + offensive purpose --> dual_purpose_move")
        answer.AppendLine("  dual_purpose_move --> better move candidate")
        answer.AppendLine("  both purposes must belong to the same Kibo move.")
        answer.AppendLine()
        answer.AppendLine("ESCAPE SAFETY RULE")
        answer.AppendLine("  saving one threatened piece must not newly expose another friendly piece")
        answer.AppendLine("  when that newly exposed piece has a higher material value.")
        answer.AppendLine()
        answer.AppendLine("HOW_TO_WIN KNOWLEDGE BASE")
        answer.AppendLine("  capture_higher_valued_pieces --> gain_material_advantage")
        answer.AppendLine("  facilitate_more_mobility --> improve_position")
        answer.AppendLine("  prefer_multiple_beneficial_purposes --> better_move")
        answer.AppendLine("  use_offensive_purpose --> defeat_opponent_king")
        answer.AppendLine("  use_defensive_purpose --> preserve_own_force")
        answer.AppendLine("  a move may infer more than one how_to_win strategy.")
        answer.AppendLine()
        answer.AppendLine("UNIVERSAL CAPTURE STRATEGY")
        answer.AppendLine("  capture target A")
        answer.AppendLine("  +-- directCapture(A)")
        answer.AppendLine("  +-- indirectCapture(A, B): capture an accessible B only when removing B enables A.")
        answer.AppendLine("  B may be at hand or easier to capture, but the causal link to A is required.")

        If _lastPurposeInterpretation = "gain_more_capturing_opportunity" Then
            answer.AppendLine()
            answer.AppendLine("CURRENT DOTS")
            answer.AppendLine("  physicalMove -> purposeClaim [connected by human explanation]")
            answer.AppendLine("  purposeClaim -> gain_more_capturing_opportunity [dictionary match]")
            answer.AppendLine("  physicalMove -> capture_opportunity_count_increased [NOT CONNECTED YET]")
            answer.AppendLine("  missing: compare iEnvBefore with iEnvAfter, then verify or reject the purpose.")
        End If
    End Sub

    Private Function FormatPhysicalMove(moveTerm As Term) As String
        Dim text = If(moveTerm, New AtomTerm("unknownMove")).ToString()
        If text.StartsWith("move(", StringComparison.Ordinal) Then
            Return "physicalMove" & text.Substring(4)
        End If
        Return "physicalMove(" & text & ")"
    End Function

    Private Sub ConfigureGrid(grid As DataGridView)
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.MultiSelect = False
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.RowHeadersVisible = False
    End Sub

    Private Sub ConfigureHumanVsComputerUi()
        Text = "Janggi Expert System — Human (Blue) vs Computer (Red)"

        btnRun.Text = "Computer Move"
        btnRun.ToolTipText = "Let the computer make one Red move."
        If _moveForMeButton Is Nothing Then
            _moveForMeButton = New ToolStripButton() With {
                .DisplayStyle = ToolStripItemDisplayStyle.Text,
                .Text = "Move For Me",
                .ToolTipText = "Ask the move generator to make one Blue move on your behalf."
            }
            ToolStrip1.Items.Insert(ToolStrip1.Items.IndexOf(btnRun) + 1, _moveForMeButton)
            AddHandler _moveForMeButton.Click, AddressOf MoveForMeButton_Click
        End If
        btnClear.Text = "New Game"
        btnClear.ToolTipText = "Start a new Human-versus-Computer game."
        btnStop.Visible = False
        btnGoAgain.Visible = False
        btnRandomPlay.Visible = False
        btnSaveScript.Visible = False
        ToolStripSeparator1.Visible = False
        If _commitLogicButton IsNot Nothing Then _commitLogicButton.Visible = False

        ' Leave the bottom panel (source / debug) visible and resizable.
        ' Remove the lexical/debug output pane window at the far right of the bottom pane.
        SplitContainer3.Panel2Collapsed = True
        tabInspector.TabPages.Clear()
        tabInspector.TabPages.Add(_whyTab)
        If _callHierarchyTab IsNot Nothing Then tabInspector.TabPages.Add(_callHierarchyTab)
        tabInspector.SelectedTab = _whyTab

        _currentKibo = New AtomTerm("emptyKibo")
        _humanExplanationRequired = False
        _lastMoveWasHuman = False
        _lastMoveTerm = Nothing
        _expertExplanationFacts.Clear()
        _newlyRecordedExpertFacts.Clear()
        _pendingInsertionCandidates.Clear()
        _pendingExpertPurpose = ""
        _lastPurposeInterpretation = ""
        _runtime.ResetJanggiGame(picCanvas)
        _runtime.SaveJanggiGameState()
        _whyQuestionTextBox.Clear()
        _whyQuestionTextBox.Enabled = False
        _computerExplanationTextBox.Text = "The computer's explanation will appear after its move."
        _askWhyButton.Text = "Record Explanation Now"
        _askWhyButton.Enabled = False
        UpdateFactsAndClausesPane()
        UpdateTurnLabel()
        ApplyResponsiveLayout()
    End Sub

    Private Function ExtractPredicateArgs(text As String, predicateName As String) As List(Of String)
        Dim marker = predicateName & "("
        Dim start = text.IndexOf(marker, StringComparison.Ordinal)
        If start < 0 Then Return New List(Of String)()

        start += marker.Length
        Dim finish = text.LastIndexOf(")"c)
        If finish < start Then Return New List(Of String)()

        Return SplitTopLevelArgs(text.Substring(start, finish - start))
    End Function

    Private Function SplitTopLevelArgs(text As String) As List(Of String)
        Dim args As New List(Of String)()
        Dim depth = 0
        Dim start = 0

        For i = 0 To text.Length - 1
            Dim ch = text(i)
            If ch = "("c Then
                depth += 1
            ElseIf ch = ")"c Then
                depth -= 1
            ElseIf ch = ","c AndAlso depth = 0 Then
                args.Add(text.Substring(start, i - start).Trim())
                start = i + 1
            End If
        Next

        args.Add(text.Substring(start).Trim())
        Return args
    End Function

    Private Sub ScrollGridToLastRow(grid As DataGridView)
        If grid.Rows.Count = 0 Then Exit Sub

        Dim lastIndex = grid.Rows.Count - 1
        grid.ClearSelection()
        grid.Rows(lastIndex).Selected = True
        grid.FirstDisplayedScrollingRowIndex = lastIndex
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        lstCommands.Nodes.Clear()
        AddCommandTreeNode("board(9,10).")
        AddCommandTreeNode("line(20,20,340,380).")
        AddCommandTreeNode("showJanggi.")
        AddCommandTreeNode("make_picture_size_sync.")
        AddCommandTreeNode("showPieces.")
        AddCommandTreeNode("randomPlay.")
        AddCommandTreeNode("stopRandomPlay.")

        picCanvas.BackColor = Color.White
        _runtime.ClearPictureBox(picCanvas)
        UpdateTurnLabel()
        resizeTimer.Interval = 150
        randomPlayTimer.Interval = 750
        automaticPlayTimer.Interval = 1000
        pauseReplayTimer.Interval = 450
        opponentMoveBlinkTimer.Interval = 320

        rtbSource.Font = New Font("Consolas", 10)
        rtbDebug.Font = New Font("Consolas", 10)
        ConfigureInspectorGrids()
        InitializeWhyInspector()
        InitializeCallHierarchyTab()
        tabMoves.Text = "Database"

        rtbSource.Text =
"main(iKibo, oNewPos, oResult) :-
    turnFromKibo(iKibo, xTurn),
    generateAnyGoodMove(xTurn, xPiece, xOldPos, oNewPos),
    checkCheckMate(xTurn, oNewPos, oResult).

turnFromKibo(emptyKibo, BLUE).

turnFromKibo(kibo(iEarlierKibo, move(iLastTurn, iPiece, iFrom, iTo)), oTurn) :-
    opponent(iLastTurn, oTurn).

checkCheckMate(iTurn, iNewPos, WIN) :-
    checkMate(iTurn, iNewPos, WIN),
    !,
    declareGameOver(iTurn, iNewPos, WIN).

checkCheckMate(iTurn, iNewPos, CONTINUE).

generateAnyGoodMove(iKind, oPiece, iFrom, oTo) :-
    canEat(iKind, oPiece, iFrom, oTo),
    not(exposesKing(iKind, oPiece, iFrom, oTo)),
    applyMove(iKind, oPiece, iFrom, oTo).

generateAnyGoodMove(iKind, oPiece, iFrom, oTo) :-
    equalOrGreaterMove(iKind, oPiece, iFrom, oTo),
    not(exposesKing(iKind, oPiece, iFrom, oTo)),
    applyMove(iKind, oPiece, iFrom, oTo).

generateAnyGoodMove(iKind, oPiece, iFrom, oTo) :-
    takeCareBeingEaten(iKind, oPiece, iFrom, oTo),
    not(exposesKing(iKind, oPiece, iFrom, oTo)),
    applyMove(iKind, oPiece, iFrom, oTo).

generateAnyGoodMove(iKind, oPiece, iFrom, oTo) :-
    moveToPromising(iKind, oPiece, iFrom, oTo),
    not(exposesKing(iKind, oPiece, iFrom, oTo)),
    applyMove(iKind, oPiece, iFrom, oTo).

generateAnyGoodMove(iKind, oPiece, iFrom, oTo) :-
    safeFallbackMove(iKind, oPiece, iFrom, oTo),
    not(exposesKing(iKind, oPiece, iFrom, oTo)),
    applyMove(iKind, oPiece, iFrom, oTo).

escapeSafetyRule(reject_escape_that_newly_exposes_higher_valued_piece).
metaWhyRule(local_escape_must_not_create_greater_material_loss).

canEat(iKind, iPiece, Pos1, Pos2) :-
    bestCaptureMove(iKind, iPiece, Pos1, Pos2).

bestCaptureMove(iKind, oPiece, iFrom, oTo) :-
    canGo(iKind, oPiece, iFrom, oTo),
    targetValue(iKind, oTo, xTargetPiece, xTargetVal),
    highestValueCapture(iKind, xTargetVal).

highestValueCapture(iKind, iTargetVal) :-
    not(hasHigherValuedCapture(iKind, iTargetVal)).

hasHigherValuedCapture(iKind, iTargetVal) :-
    canGo(iKind, xOtherPiece, xOtherFrom, xOtherTo),
    targetValue(iKind, xOtherTo, xOtherTarget, xOtherVal),
    greaterOrEqual(xOtherVal, iTargetVal).

equalOrGreaterMove(iKind, iPiece, Pos1, Pos2) :-
    equalOrGreaterCaptureMove(iKind, iPiece, Pos1, Pos2).

takeCareBeingEaten(iKind, oPiece, iFrom, oTo) :-
    isThere(iKind, oPiece, xRow, xCol),
    equal(iFrom, pos(xCol, xRow)),
    opponent(iKind, oOppoKind),
    canGo(oOppoKind, xOppoPiece, xOppoFrom, iFrom),
    canGo(iKind, oPiece, iFrom, oTo),
    not(canBeEaten(iKind, oPiece, iFrom, oTo)).

canGoSafe(iKind, iPiece, Pos1, Pos2) :-
    not(canBeEaten(iKind, iPiece, Pos1, Pos2)).

canBeEaten(iKind, iPiece, Pos1, Pos2) :-
    canGo(iKind, iPiece, Pos1, Pos2),
    opponent(iKind, oOppoKind),
    canGo(oOppoKind, xOppoPiece, xOppoFrom, Pos2).

selfValue(iKind, iPiece, Pos1, oValue) :-
    isThere(iKind, iPiece, Pos1:x, Pos1:y),
    pieceValue(iPiece, oValue).

targetValue(iKind, Pos2, oTargetPiece, oValue) :-
    opponent(iKind, oOppoKind),
    isThere(oOppoKind, oTargetPiece, Pos2:x, Pos2:y),
    pieceValue(oTargetPiece, oValue).

declareGameOver(iKind, iNewPos, WIN).

betterPosition(iKind, iPiece, Pos1, Pos2) :-
    situationCounts(iKind, xOldCanGo, xOldCanEat, xOldCanBeEaten),
    canGo(iKind, iPiece, Pos1, Pos2),
    situationCounts(iKind, xNewCanGo, xNewCanEat, xNewCanBeEaten),
    betteringSituation(iKind, xOldCanGo, xOldCanEat, xOldCanBeEaten, xNewCanGo, xNewCanEat, xNewCanBeEaten).

situationCounts(iKind, oCanGoCount, oCanEatCount, oCanBeEatenCount) :-
    countCanGo(iKind, oCanGoCount),
    countCanEat(iKind, oCanEatCount),
    countCanBeEaten(iKind, oCanBeEatenCount).

betteringSituation(iKind, iOldCanGo, iOldCanEat, iOldCanBeEaten, iNewCanGo, iNewCanEat, iNewCanBeEaten) :-
    greaterOrEqual(iNewCanGo, iOldCanGo),
    greaterOrEqual(iNewCanEat, iOldCanEat),
    greaterOrEqual(iOldCanBeEaten, iNewCanBeEaten).

isBlocking(iPiece, iBlockedPiece).
isGuarding(iPiece, iGuardedPiece).
isSupporting(iPiece, iSupportedPiece).
isSteppingStone(iPiece, iForPiece).

purposeDictionary(gain_more_capturing_opportunity).
purposeDomain(gain_more_capturing_opportunity, tactical_positioning).
purposeTimeHorizon(gain_more_capturing_opportunity, next_turn).
purposeDesiredEffect(gain_more_capturing_opportunity, increase_capture_options).
purposeSuccessMeasure(gain_more_capturing_opportunity, capture_opportunity_count).
purposeRequiresEvidence(gain_more_capturing_opportunity, compare_environment_before_after).
purposeWhyNode(gain_more_capturing_opportunity, gain_more_capturing_opportunity).

whyTreeStatus(provisional).

whyNode(win_game, ultimate_goal).
whyNode(defeat_opponent_king, strategic_goal).
whyNode(gain_material_advantage, strategic_goal).
whyNode(preserve_own_force, strategic_goal).
whyNode(improve_position, strategic_goal).
whyNode(create_attack, tactical_purpose).
whyNode(gain_more_capturing_opportunity, tactical_purpose).
whyNode(protect_threatened_piece, tactical_purpose).
whyNode(increase_mobility, positional_purpose).
whyNode(control_important_area, positional_purpose).
whyNode(capture_opportunity_count_increased, evidence).

whyEdge(defeat_opponent_king, win_game, achieves).
whyEdge(gain_material_advantage, win_game, contributes_to).
whyEdge(preserve_own_force, win_game, contributes_to).
whyEdge(improve_position, win_game, contributes_to).
whyEdge(create_attack, defeat_opponent_king, contributes_to).
whyEdge(gain_more_capturing_opportunity, gain_material_advantage, may_contribute_to).
whyEdge(protect_threatened_piece, preserve_own_force, contributes_to).
whyEdge(increase_mobility, improve_position, contributes_to).
whyEdge(control_important_area, improve_position, contributes_to).
whyEdge(capture_opportunity_count_increased, gain_more_capturing_opportunity, verifies).
whyEdge(increase_mobility, gain_more_capturing_opportunity, may_enable).

metaWhyRule(increased_mobility_may_create_capture_opportunity).

captureOpportunityFromMobility(
    iEnvBefore,
    iEnvAfter,
    iKind,
    iPiece,
    oTargetPiece,
    oTargetPos
) :-
    newLegalDestination(
        iEnvBefore,
        iEnvAfter,
        iKind,
        iPiece,
        xNewPiecePos
    ),
    prospectiveCaptureFromPosition(
        iEnvAfter,
        iKind,
        iPiece,
        xNewPiecePos,
        oTargetPiece,
        oTargetPos
    ).

purposePolarity(protect_threatened_piece, defensive).
purposePolarity(preserve_own_force, defensive).
purposePolarity(create_attack, offensive).
purposePolarity(gain_more_capturing_opportunity, offensive).
purposePolarity(prepare_chariot_interception, offensive).

metaWhyRule(move_serving_defensive_and_offensive_purposes_is_better).

moveServesPurposeDomain(iKibo, iMove, oDomain) :-
    purposeClaim(iKibo, iMove, xPurpose),
    purposePolarity(xPurpose, oDomain).

dualPurposeMove(iKibo, iMove) :-
    moveServesPurposeDomain(iKibo, iMove, defensive),
    moveServesPurposeDomain(iKibo, iMove, offensive).

moveQuality(iKibo, iMove, better, dual_defensive_offensive) :-
    dualPurposeMove(iKibo, iMove).

whyNode(dual_purpose_move, meta_purpose).
whyNode(better_move, move_evaluation).
whyEdge(dual_purpose_move, better_move, qualifies_as).
whyEdge(better_move, win_game, contributes_to).

how_to_win(capture_higher_valued_pieces).
how_to_win(facilitate_more_mobility).
how_to_win(prefer_multiple_beneficial_purposes).
how_to_win(use_offensive_purpose).
how_to_win(use_defensive_purpose).

how_to_win_supports(capture_higher_valued_pieces, gain_material_advantage).
how_to_win_supports(facilitate_more_mobility, improve_position).
how_to_win_supports(prefer_multiple_beneficial_purposes, better_move).
how_to_win_supports(use_offensive_purpose, defeat_opponent_king).
how_to_win_supports(use_defensive_purpose, preserve_own_force).

whyRelatedOrSame(iPurpose, iPurpose).

whyRelatedOrSame(iLowerPurpose, oHigherPurpose) :-
    whyAncestor(iLowerPurpose, oHigherPurpose).

moveSupportsHowToWin(
    iEnvBefore,
    iKibo,
    iMove,
    capture_higher_valued_pieces
) :-
    capturedHigherValuedPiece(
        iEnvBefore,
        iMove,
        xTargetPiece,
        xTargetValue
    ).

moveSupportsHowToWin(
    iEnvBefore,
    iKibo,
    iMove,
    facilitate_more_mobility
) :-
    purposeClaim(iKibo, iMove, xPurpose),
    whyRelatedOrSame(xPurpose, increase_mobility).

moveSupportsHowToWin(
    iEnvBefore,
    iKibo,
    iMove,
    prefer_multiple_beneficial_purposes
) :-
    moveQuality(iKibo, iMove, better, dual_defensive_offensive).

moveSupportsHowToWin(
    iEnvBefore,
    iKibo,
    iMove,
    use_offensive_purpose
) :-
    moveServesPurposeDomain(iKibo, iMove, offensive).

moveSupportsHowToWin(
    iEnvBefore,
    iKibo,
    iMove,
    use_defensive_purpose
) :-
    moveServesPurposeDomain(iKibo, iMove, defensive).

winningMoveCandidate(iEnvBefore, iKibo, iMove) :-
    moveSupportsHowToWin(iEnvBefore, iKibo, iMove, xStrategy),
    how_to_win(xStrategy).

whyNode(capture_higher_valued_pieces, winning_method).
whyNode(facilitate_more_mobility, winning_method).
whyNode(prefer_multiple_beneficial_purposes, winning_method).
whyNode(use_offensive_purpose, winning_method).
whyNode(use_defensive_purpose, winning_method).

whyEdge(capture_higher_valued_pieces, gain_material_advantage, how_to_win).
whyEdge(facilitate_more_mobility, improve_position, how_to_win).
whyEdge(prefer_multiple_beneficial_purposes, better_move, how_to_win).
whyEdge(use_offensive_purpose, defeat_opponent_king, how_to_win).
whyEdge(use_defensive_purpose, preserve_own_force, how_to_win).

universalGameStrategy(capture_target_by_direct_or_indirect_plan).
metaWhyRule(capture_a_directly_or_capture_b_to_enable_a).

capture(iEnv, iKind, iTargetA, plan(direct, oMove)) :-
    directCapture(iEnv, iKind, iTargetA, oMove).

capture(
    iEnv,
    iKind,
    iTargetA,
    plan(indirect, oTargetB, oFirstMove)
) :-
    indirectCapture(
        iEnv,
        iKind,
        iTargetA,
        oTargetB,
        oFirstMove
    ).

directCapture(iEnv, iKind, iTargetA, oMove) :-
    legalCaptureMove(iEnv, iKind, oMove, iTargetA).

intermediateCaptureTarget(iEnv, iKind, iTargetA, oTargetB) :-
    targetAtHand(iEnv, iKind, oTargetB),
    captureOfTargetEnablesTarget(iEnv, iKind, oTargetB, iTargetA).

intermediateCaptureTarget(iEnv, iKind, iTargetA, oTargetB) :-
    easierToCapture(iEnv, iKind, oTargetB, iTargetA),
    captureOfTargetEnablesTarget(iEnv, iKind, oTargetB, iTargetA).

indirectCapture(
    iEnv,
    iKind,
    iTargetA,
    oTargetB,
    oFirstMove
) :-
    intermediateCaptureTarget(iEnv, iKind, iTargetA, oTargetB),
    legalCaptureMove(iEnv, iKind, oFirstMove, oTargetB),
    applyLogicalMove(iEnv, oFirstMove, xEnvAfterB),
    captureEnabledByRemoving(
        xEnvAfterB,
        iKind,
        oTargetB,
        iTargetA
    ),
    directCapture(xEnvAfterB, iKind, iTargetA, xSecondMove).

how_to_win(use_indirect_capture_plan).
how_to_win_supports(use_indirect_capture_plan, gain_material_advantage).

whyNode(capture_target, tactical_goal).
whyNode(direct_capture, capture_method).
whyNode(indirect_capture, capture_method).
whyNode(capture_intermediate_target, enabling_action).
whyEdge(direct_capture, capture_target, achieves).
whyEdge(indirect_capture, capture_target, achieves_later).
whyEdge(capture_intermediate_target, indirect_capture, enables).
whyEdge(use_indirect_capture_plan, gain_material_advantage, how_to_win).

whyGap(gain_more_capturing_opportunity, compare_environment_before_after).
whyGap(indirect_capture, logical_board_simulation_required).
whyGap(increase_mobility, prospective_capture_from_new_destination_not_yet_evaluated).

whyAncestor(iLowerPurpose, oHigherPurpose) :-
    whyEdge(iLowerPurpose, oHigherPurpose, xRelation).

whyAncestor(iLowerPurpose, oHigherPurpose) :-
    whyEdge(iLowerPurpose, xMiddlePurpose, xRelation),
    whyAncestor(xMiddlePurpose, oHigherPurpose).

opponent(BLUE, RED).
opponent(RED, BLUE).

pieceValue(king, 1000).
pieceValue(chariot, 90).
pieceValue(cannon, 50).
pieceValue(horse, 40).
pieceValue(elephant, 30).
pieceValue(guard, 20).
pieceValue(soldier, 10).

isThere(RED, king, 1, 4).
isThere(RED, chariot, 0, 0).
isThere(RED, chariot, 0, 8).
isThere(RED, cannon, 2, 1).
isThere(RED, cannon, 2, 7).
isThere(RED, horse, 0, 2).
isThere(RED, horse, 0, 7).
isThere(RED, elephant, 0, 1).
isThere(RED, elephant, 0, 6).
isThere(RED, guard, 0, 3).
isThere(RED, guard, 0, 5).
isThere(RED, soldier, 3, 0).
isThere(RED, soldier, 3, 2).
isThere(RED, soldier, 3, 4).
isThere(RED, soldier, 3, 6).
isThere(RED, soldier, 3, 8).

isThere(BLUE, king, 8, 4).
isThere(BLUE, chariot, 9, 0).
isThere(BLUE, chariot, 9, 8).
isThere(BLUE, cannon, 7, 1).
isThere(BLUE, cannon, 7, 7).
isThere(BLUE, horse, 9, 2).
isThere(BLUE, horse, 9, 7).
isThere(BLUE, elephant, 9, 1).
isThere(BLUE, elephant, 9, 6).
isThere(BLUE, guard, 9, 3).
isThere(BLUE, guard, 9, 5).
isThere(BLUE, soldier, 6, 0).
isThere(BLUE, soldier, 6, 2).
isThere(BLUE, soldier, 6, 4).
isThere(BLUE, soldier, 6, 6).
isThere(BLUE, soldier, 6, 8).
equal(iX, iX).

lessThan(iX, iY) :-
    greaterOrEqual(iY, iX),
    not(equal(iX, iY)).

isBetween(iMiddle, iVal1, iVal2) :-
    lessThan(iVal1, iMiddle),
    lessThan(iMiddle, iVal2).

isBetween(iMiddle, iVal1, iVal2) :-
    lessThan(iVal2, iMiddle),
    lessThan(iMiddle, iVal1).

isKingPiece(king).

getKingPos(iKind, iPiece, Pos1, Pos2, Pos2) :-
    isKingPiece(iPiece),
    !.
getKingPos(iKind, iPiece, Pos1, Pos2, pos(xKingCol, xKingRow)) :-
    isThere(iKind, king, xKingRow, xKingCol).

isPiecePresentAfterMove(iPiece, Pos1, Pos2, xRow, xCol, iPiece) :-
    equal(Pos2, pos(xCol, xRow)).
isPiecePresentAfterMove(iPiece, Pos1, Pos2, xRow, xCol, xPieceType) :-
    isThere(xSide, xPieceType, xRow, xCol),
    not(equal(Pos1, pos(xCol, xRow))),
    not(equal(Pos2, pos(xCol, xRow))).

hasPieceBetweenColRowAfterMove(iPiece, Pos1, Pos2, iCol, iRow1, iRow2) :-
    isPiecePresentAfterMove(iPiece, Pos1, Pos2, xRow, iCol, xPieceType),
    isBetween(xRow, iRow1, iRow2).

hasPieceBetweenRowColAfterMove(iPiece, Pos1, Pos2, iRow, iCol1, iCol2) :-
    isPiecePresentAfterMove(iPiece, Pos1, Pos2, iRow, xCol, xPieceType),
    isBetween(xCol, iCol1, iCol2).

exactlyOneNonCannonPieceBetweenColRowAfterMove(iPiece, Pos1, Pos2, iCol, iRow1, iRow2) :-
    isPiecePresentAfterMove(iPiece, Pos1, Pos2, xRow1Piece, iCol, xScreenPieceType),
    not(equal(xScreenPieceType, cannon)),
    isBetween(xRow1Piece, iRow1, iRow2),
    not(hasOtherPieceBetweenColRowAfterMove(iPiece, Pos1, Pos2, iCol, iRow1, iRow2, xRow1Piece)).

hasOtherPieceBetweenColRowAfterMove(iPiece, Pos1, Pos2, iCol, iRow1, iRow2, iFirstRow) :-
    isPiecePresentAfterMove(iPiece, Pos1, Pos2, xRow2Piece, iCol, xPieceType),
    isBetween(xRow2Piece, iRow1, iRow2),
    not(equal(xRow2Piece, iFirstRow)).

exactlyOneNonCannonPieceBetweenRowColAfterMove(iPiece, Pos1, Pos2, iRow, iCol1, iCol2) :-
    isPiecePresentAfterMove(iPiece, Pos1, Pos2, iRow, xCol1Piece, xScreenPieceType),
    not(equal(xScreenPieceType, cannon)),
    isBetween(xCol1Piece, iCol1, iCol2),
    not(hasOtherPieceBetweenRowColAfterMove(iPiece, Pos1, Pos2, iRow, iCol1, iCol2, xCol1Piece)).

hasOtherPieceBetweenRowColAfterMove(iPiece, Pos1, Pos2, iRow, iCol1, iCol2, iFirstCol) :-
    isPiecePresentAfterMove(iPiece, Pos1, Pos2, iRow, xCol2Piece, xPieceType),
    isBetween(xCol2Piece, iCol1, iCol2),
    not(equal(xCol2Piece, iFirstCol)).

opponentChariotChecksAfterMove(iKind, iPiece, Pos1, Pos2, KingPos) :-
    opponent(iKind, oOppoKind),
    isThere(oOppoKind, chariot, xOppoRow, xOppoCol),
    not(equal(Pos2, pos(xOppoCol, xOppoRow))),
    equal(KingPos, pos(xOppoCol, xKingRow)),
    not(hasPieceBetweenColRowAfterMove(iPiece, Pos1, Pos2, xOppoCol, xOppoRow, xKingRow)).

opponentChariotChecksAfterMove(iKind, iPiece, Pos1, Pos2, KingPos) :-
    opponent(iKind, oOppoKind),
    isThere(oOppoKind, chariot, xOppoRow, xOppoCol),
    not(equal(Pos2, pos(xOppoCol, xOppoRow))),
    equal(KingPos, pos(xKingCol, xOppoRow)),
    not(hasPieceBetweenRowColAfterMove(iPiece, Pos1, Pos2, xOppoRow, xOppoCol, xKingCol)).

opponentCannonChecksAfterMove(iKind, iPiece, Pos1, Pos2, KingPos) :-
    opponent(iKind, oOppoKind),
    isThere(oOppoKind, cannon, xOppoRow, xOppoCol),
    not(equal(Pos2, pos(xOppoCol, xOppoRow))),
    equal(KingPos, pos(xOppoCol, xKingRow)),
    exactlyOneNonCannonPieceBetweenColRowAfterMove(iPiece, Pos1, Pos2, xOppoCol, xOppoRow, xKingRow).

opponentCannonChecksAfterMove(iKind, iPiece, Pos1, Pos2, KingPos) :-
    opponent(iKind, oOppoKind),
    isThere(oOppoKind, cannon, xOppoRow, xOppoCol),
    not(equal(Pos2, pos(xOppoCol, xOppoRow))),
    equal(KingPos, pos(xKingCol, xOppoRow)),
    exactlyOneNonCannonPieceBetweenRowColAfterMove(iPiece, Pos1, Pos2, xOppoRow, xOppoCol, xKingCol).

exposesKing(iKind, iPiece, Pos1, Pos2) :-
    getKingPos(iKind, iPiece, Pos1, Pos2, xKingPos),
    opponentChariotChecksAfterMove(iKind, iPiece, Pos1, Pos2, xKingPos),
    !.

exposesKing(iKind, iPiece, Pos1, Pos2) :-
    getKingPos(iKind, iPiece, Pos1, Pos2, xKingPos),
    opponentCannonChecksAfterMove(iKind, iPiece, Pos1, Pos2, xKingPos),
    !.

?- main(emptyKibo, oNewPos, oResult)."

        InitializeLogicCommitUi()
        CommitCurrentLogic(False)
        ConfigureHumanVsComputerUi()
    End Sub

    Private Sub btnRun_Click(sender As Object, e As EventArgs) Handles btnRun.Click
        SetPauseReplayEnabled(False)
        If _runtime.CurrentTurnAtom <> ComputerSideAtom Then
            UpdateFactsAndClausesPane()
            If CanUseControl(_whyAnswerTextBox) Then
                _whyAnswerTextBox.AppendText(Environment.NewLine & Environment.NewLine &
                    "It is the human's Blue turn. Drag a Blue piece on the board.")
            End If
            Exit Sub
        End If

        ' The explanation is valuable expert-system input, but it must not block play.
        ' Save what the human typed; if the box is empty, preserve that fact explicitly.
        If _humanExplanationRequired Then SaveHumanExplanation(True)

        If rtbSource.Text <> _committedSourceText AndAlso Not CommitCurrentLogic(True) Then Exit Sub

        SetDebugText("COMPUTER MOVE:" & Environment.NewLine & "  using saved board and Kibo")
        Application.DoEvents()
        ExecuteSource(True)
    End Sub

    Private Sub MoveForMeButton_Click(sender As Object, e As EventArgs)
        SetPauseReplayEnabled(False)
        If _runtime.CurrentTurnAtom <> HumanSideAtom Then
            UpdateFactsAndClausesPane()
            If CanUseControl(_whyAnswerTextBox) Then
                _whyAnswerTextBox.AppendText(Environment.NewLine & Environment.NewLine &
                    "It is the computer's Red turn. Use Computer Move first.")
            End If
            Exit Sub
        End If

        If rtbSource.Text <> _committedSourceText AndAlso Not CommitCurrentLogic(True) Then Exit Sub

        _computerMovingForHuman = True
        Try
            SetDebugText("MOVE FOR ME:" & Environment.NewLine &
                "  generating one Blue move from the current board and Kibo")
            Application.DoEvents()
            ExecuteSource(True)
        Finally
            _computerMovingForHuman = False
        End Try
    End Sub

    Private Sub btnStop_Click(sender As Object, e As EventArgs) Handles btnStop.Click
        SetPauseReplayEnabled(False)
        _stopRequested = True
        _runtime.RequestExecutionStop()
        SetRandomPlayEnabled(False)
        SetAutomaticPlayEnabled(False)
        Dim saved = _runtime.SaveJanggiGameState()
        SetDebugText(rtbDebug.Text & Environment.NewLine &
            "STOP:" & Environment.NewLine &
            "  requested" & Environment.NewLine &
            "  board state: " & If(saved, "saved", "nothing to save"))
        SaveUiPaneSnapshot()
        UpdateTurnLabel()
    End Sub

    Private Sub btnGoAgain_Click(sender As Object, e As EventArgs) Handles btnGoAgain.Click
        SetPauseReplayEnabled(False)
        If _isRunningScript Then
            _stopRequested = True
            _runtime.RequestExecutionStop()
            Exit Sub
        End If

        If rtbSource.Text <> _committedSourceText AndAlso Not CommitCurrentLogic(True) Then Exit Sub

        RestoreUiPaneSnapshot(True)
        ExecuteSource(True)
    End Sub

    Private Sub btnSaveScript_Click(sender As Object, e As EventArgs) Handles btnSaveScript.Click
        Using dlg As New SaveFileDialog()
            dlg.Title = "Save Prolog Script"
            dlg.FileName = "myScript.txt"
            dlg.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
            dlg.DefaultExt = "txt"
            dlg.AddExtension = True
            dlg.OverwritePrompt = True

            If dlg.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            File.WriteAllText(dlg.FileName, rtbSource.Text, Encoding.UTF8)
            Dim savedText = File.ReadAllText(dlg.FileName, Encoding.UTF8)
            SetDebugText("SAVE SCRIPT:" & Environment.NewLine &
                "  saved: " & dlg.FileName & Environment.NewLine &
                "  read back: " & savedText.Length.ToString() & " characters")
        End Using
    End Sub

    Private Sub btnClear_Click(sender As Object, e As EventArgs) Handles btnClear.Click
        SetPauseReplayEnabled(False)
        _stopRequested = True
        _pausedAfterMove = False
        _currentKibo = New AtomTerm("emptyKibo")
        _humanExplanationRequired = False
        _lastMoveWasHuman = False
        _lastMoveTerm = Nothing
        _expertExplanationFacts.Clear()
        _newlyRecordedExpertFacts.Clear()
        _pendingInsertionCandidates.Clear()
        _pendingExpertPurpose = ""
        _lastPurposeInterpretation = ""
        SetRandomPlayEnabled(False)
        SetAutomaticPlayEnabled(False)
        rtbDebug.Clear()
        _runtime.ResetJanggiGame(picCanvas)
        _runtime.SaveJanggiGameState()
        If CanUseControl(_whyQuestionTextBox) Then _whyQuestionTextBox.Clear()
        If CanUseControl(_whyQuestionTextBox) Then _whyQuestionTextBox.Enabled = False
        If CanUseControl(_computerExplanationTextBox) Then
            _computerExplanationTextBox.Text = "The computer's explanation will appear after its move."
        End If
        If CanUseControl(_askWhyButton) Then
            _askWhyButton.Text = "Record Explanation Now"
            _askWhyButton.Enabled = False
        End If
        UpdateFactsAndClausesPane()
        UpdateTurnLabel()
    End Sub

    Private Sub btnRandomPlay_Click(sender As Object, e As EventArgs) Handles btnRandomPlay.Click
        SetPauseReplayEnabled(False)
        If randomPlayTimer.Enabled Then
            SetRandomPlayEnabled(False)
            SetDebugText("RANDOM:" & Environment.NewLine & "  stopped")
            Exit Sub
        End If

        _runtime.Execute(New AtomTerm("showJanggi"), picCanvas)
        SetRandomPlayEnabled(True)
        UpdateTurnLabel()
        SetDebugText("RANDOM:" & Environment.NewLine & "  started")
    End Sub

    Private Sub randomPlayTimer_Tick(sender As Object, e As EventArgs) Handles randomPlayTimer.Tick
        If _isClosing Then Exit Sub
        If _isDraggingPiece Then Exit Sub

        _whyCallPath.Clear()
        If Not _runtime.MakeRandomMove(picCanvas) Then
            SetRandomPlayEnabled(False)
        End If

        SetDebugText("RANDOM:" & Environment.NewLine & "  " & _runtime.LastDragMessage)
        UpdateTurnLabel()
    End Sub

    Private Sub btnAutomaticPlay_Click(sender As Object, e As EventArgs) Handles btnAutomaticPlay.Click
        SetPauseReplayEnabled(False)
        If automaticPlayTimer.Enabled Then
            SetAutomaticPlayEnabled(False)
            SetDebugText("AUTO PLAY:" & Environment.NewLine & "  stopped")
            Exit Sub
        End If

        _runtime.Execute(New AtomTerm("showJanggi"), picCanvas)
        SetAutomaticPlayEnabled(True)
        UpdateTurnLabel()
        SetDebugText("AUTO PLAY:" & Environment.NewLine & "  started")
    End Sub

    Private Sub automaticPlayTimer_Tick(sender As Object, e As EventArgs) Handles automaticPlayTimer.Tick
        If _isClosing Then Exit Sub
        If _isDraggingPiece Then Exit Sub

        If _runtime.IsGameOver Then
            SetAutomaticPlayEnabled(False)
            UpdateTurnLabel()
            Exit Sub
        End If

        _whyCallPath.Clear()
        Dim mainQuery As New QueryTerm(New List(Of Term) From {
            New CompoundTerm("main", New List(Of Term) From {
                _currentKibo,
                New VariableTerm("oNewPos"),
                New VariableTerm("oResult")
            })
        })

        _computerMovingForHuman = True
        Dim queryResult As String = ""
        Try
            queryResult = _runtime.ExecuteFirstQuery(mainQuery, picCanvas)
        Finally
            _computerMovingForHuman = False
        End Try

        If queryResult.TrimEnd().EndsWith("=> no", StringComparison.Ordinal) Then
            SetAutomaticPlayEnabled(False)
            SetDebugText("AUTO PLAY:" & Environment.NewLine & "  stopped: no purposeful move found")
        Else
            SetDebugText("AUTO PLAY:" & Environment.NewLine & "  move: " & queryResult)
        End If
        UpdateTurnLabel()
    End Sub

    Private Sub lstCommands_AfterSelect(sender As Object, e As TreeViewEventArgs) Handles lstCommands.AfterSelect

    End Sub

    Private Function ContainsWholeWord(text As String, word As String) As Boolean
        Dim index = 0

        While index < text.Length
            index = text.IndexOf(word, index, StringComparison.Ordinal)
            If index < 0 Then
                Return False
            End If

            If IsWholeWordMatch(text, index, word.Length) Then
                Return True
            End If

            index += Math.Max(word.Length, 1)
        End While

        Return False
    End Function

    Private Sub lstCommands_DoubleClick(sender As Object, e As EventArgs) Handles lstCommands.DoubleClick
        If lstCommands.SelectedNode IsNot Nothing Then
            Dim cmd As String = lstCommands.SelectedNode.Text.Trim()

            If cmd = "" Then Exit Sub

            rtbSource.Text = cmd
            ExecuteSource()
        End If
    End Sub

    Private Sub Form1_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        rtbSource.ReadOnly = False
        rtbSource.Focus()
        rtbSource.SelectionStart = rtbSource.TextLength
    End Sub
    Private Sub ExecuteSource(Optional resumeSavedBoard As Boolean = False)
        If _isRunningScript Then Exit Sub

        Try
            _isRunningScript = True
            _stopRequested = False
            _pausedAfterMove = False
            btnRun.Enabled = False
            btnGoAgain.Enabled = False
            btnStop.Enabled = True
            If CanUseToolStripItem(_moveForMeButton) Then _moveForMeButton.Enabled = False
            ResetSourceHighlight()

            Dim source = rtbSource.Text
            Dim statementLines = GetStatementStartLines(rtbSource.Lines)

            Dim lexer As New Lexer(source)
            Dim tokens = lexer.Tokenize()

            Dim parser As New Parser(tokens)
            Dim terms = parser.ParseProgram()

            Dim debugText As New System.Text.StringBuilder()
            If resumeSavedBoard AndAlso _uiSnapshot IsNot Nothing Then
                debugText.AppendLine(rtbDebug.Text.TrimEnd())
                debugText.AppendLine()
                debugText.AppendLine("GO AGAIN:")
                debugText.AppendLine("  appending from saved panes")
            End If
            BuildPredicateLineMap(terms, statementLines)
            _traceNodeStack.Clear()
            If resumeSavedBoard AndAlso _uiSnapshot IsNot Nothing Then
                AddCommandTreeNode("TRACE RESUME:")
            Else
                lstCommands.Nodes.Clear()
                AddCommandTreeNode("TRACE:")
                _traceSeq = 0
                dgvTrace.Rows.Clear()
                dgvMoves.Rows.Clear()
                dgvCanGo.Rows.Clear()
                dgvCanEat.Rows.Clear()
                dgvCanBeEaten.Rows.Clear()
            End If

            debugText.AppendLine("TOKENS:")
            For Each tk In tokens
                debugText.AppendLine("  " & tk.ToString())
            Next

            debugText.AppendLine()
            debugText.AppendLine("AST:")
            For Each term In terms
                debugText.AppendLine("  " & term.ToString())
            Next

            debugText.AppendLine()
            debugText.AppendLine("RUNTIME:")
            _runtime.BeginScriptExecution()
            For termIndex = 0 To terms.Count - 1
                Dim term = terms(termIndex)
                Dim atom = TryCast(term, AtomTerm)

                If termIndex < statementLines.Count Then
                    HighlightSourceLine(statementLines(termIndex), Color.LightGoldenrodYellow)
                    If CheckBreakpointHit(statementLines(termIndex), term.ToString()) Then
                        debugText.AppendLine($"  break: line {statementLines(termIndex) + 1}")
                        SetDebugText(debugText.ToString())
                        Exit For
                    End If
                End If

                If TypeOf term Is QueryTerm Then
                    debugText.AppendLine("  skipped query: " & term.ToString())
                    SetDebugText(debugText.ToString())
                    Continue For
                End If

                If atom IsNot Nothing AndAlso atom.Name = "make_picture_size_sync" Then
                    _pictureSizeSyncEnabled = True
                    SyncPictureSizeToParent()
                    debugText.AppendLine("  setup: make_picture_size_sync/0 executed")
                ElseIf atom IsNot Nothing AndAlso atom.Name = "randomPlay" Then
                    _runtime.Execute(New AtomTerm("showJanggi"), picCanvas)
                    SetRandomPlayEnabled(True)
                    debugText.AppendLine("  setup: randomPlay/0 started")
                ElseIf atom IsNot Nothing AndAlso atom.Name = "stopRandomPlay" Then
                    SetRandomPlayEnabled(False)
                    debugText.AppendLine("  setup: stopRandomPlay/0 executed")
                Else
                    Dim result = _runtime.Execute(term, picCanvas)
                    debugText.AppendLine("  " & result)
                End If
                SetDebugText(debugText.ToString())
                Application.DoEvents()
            Next

            If resumeSavedBoard AndAlso _runtime.RestoreSavedJanggiGame(picCanvas) Then
                debugText.AppendLine("  setup: restored saved Janggi board")
            Else
                _currentKibo = New AtomTerm("emptyKibo")
                _runtime.ResetJanggiGame(picCanvas)
                debugText.AppendLine("  setup: reset Janggi board")
            End If
            If _stopRequested Then
                SetDebugText(debugText.ToString())
                UpdateTurnLabel()
                Return
            End If

            For i = 0 To terms.Count - 1
                If terms(i).ToString().StartsWith("main(") AndAlso i < statementLines.Count Then
                    HighlightSourceLine(statementLines(i), Color.LightSkyBlue)
                    Exit For
                End If
            Next

            For turnIndex = 1 To MaxRunTurns
                If _stopRequested Then
                    If _pausedAfterMove Then
                        debugText.AppendLine("  paused after move: ask Why, then press Go Again")
                    Else
                        debugText.AppendLine("  stopped: requested by user")
                    End If
                    Exit For
                End If

                If Not _runtime.IsGameOver AndAlso Not _runtime.HasLegalMoveForCurrentTurn() Then
                    debugText.AppendLine("  stopped: no legal move for " & _runtime.CurrentTurnAtom)
                    Exit For
                End If

                Dim turnAtom = _runtime.CurrentTurnAtom
                _whyCallPath.Clear()
                Dim mainQuery As New QueryTerm(New List(Of Term) From {
                    New CompoundTerm("main", New List(Of Term) From {
                        _currentKibo,
                        New VariableTerm("oNewPos"),
                        New VariableTerm("oResult")
                    })
                })

                Dim kiboBeforeMove = _currentKibo.ToString()
                Dim queryResult = _runtime.ExecuteFirstQuery(mainQuery, picCanvas)
                debugText.AppendLine($"  entry {turnIndex} ({turnAtom}, kibo={kiboBeforeMove}): " & queryResult)
                If queryResult.TrimEnd().EndsWith("=> no", StringComparison.Ordinal) AndAlso Not _pausedAfterMove Then
                    debugText.AppendLine("  stopped: generateAnyGoodMove failed; no purposeful move was selected")
                    Exit For
                End If
                If _runtime.IsGameOver Then Exit For
                SetDebugText(debugText.ToString())
                UpdateTurnLabel()
                Application.DoEvents()
            Next

            If Not _runtime.IsGameOver AndAlso Not _stopRequested Then
                debugText.AppendLine($"  stopped: safety cap reached ({MaxRunTurns} turns)")
            End If

            If _runtime.IsGameOver Then
                debugText.AppendLine("  game over: " & _runtime.GameOverText)
            End If

            SetDebugText(debugText.ToString())
            UpdateTurnLabel()

        Catch ex As Exception
            SetDebugText("ERROR:" & Environment.NewLine & ex.Message)
            UpdateTurnLabel()
        Finally
            btnRun.Enabled = True
            btnGoAgain.Enabled = True
            btnStop.Enabled = False
            If CanUseToolStripItem(_moveForMeButton) Then _moveForMeButton.Enabled = True
            _isRunningScript = False
            If _pausedAfterMove AndAlso _lastWhyMove <> "" Then
                SaveUiPaneSnapshot()
            End If
        End Try
    End Sub

    Private Sub picCanvas_Resize(sender As Object, e As EventArgs) Handles picCanvas.Resize
        If _isClosing OrElse _isRunningScript Then Exit Sub

        If Me.IsHandleCreated Then
            resizeTimer.Stop()
            resizeTimer.Start()
        End If
    End Sub

    Private Sub picCanvas_MouseDown(sender As Object, e As MouseEventArgs) Handles picCanvas.MouseDown
        If e.Button <> MouseButtons.Left Then Exit Sub

        If _runtime.CurrentTurnAtom <> HumanSideAtom Then
            UpdateFactsAndClausesPane()
            If CanUseControl(_whyAnswerTextBox) Then
                _whyAnswerTextBox.AppendText(Environment.NewLine & Environment.NewLine &
                    "It is the computer's Red turn. Click Computer Move.")
            End If
            Exit Sub
        End If

        SetPauseReplayEnabled(False)
        SetRandomPlayEnabled(False)
        _whyCallPath.Clear()
        _isDraggingPiece = _runtime.BeginPieceDrag(picCanvas, e.Location)
        If _isDraggingPiece Then
            picCanvas.Cursor = Cursors.Hand
            picCanvas.Capture = True
        ElseIf _runtime.LastDragMessage <> "" Then
            SetDebugText("DRAG:" & Environment.NewLine & "  " & _runtime.LastDragMessage)
        End If

        UpdateTurnLabel()
    End Sub

    Private Sub picCanvas_MouseMove(sender As Object, e As MouseEventArgs) Handles picCanvas.MouseMove
        If Not _isDraggingPiece Then Exit Sub

        _runtime.MovePieceDrag(picCanvas, e.Location)
    End Sub

    Private Sub picCanvas_MouseUp(sender As Object, e As MouseEventArgs) Handles picCanvas.MouseUp
        If Not _isDraggingPiece Then Exit Sub

        _runtime.EndPieceDrag(picCanvas, e.Location)
        _isDraggingPiece = False
        picCanvas.Cursor = Cursors.Default
        picCanvas.Capture = False
        SetDebugText("DRAG:" & Environment.NewLine & "  " & _runtime.LastDragMessage)
        UpdateTurnLabel()
    End Sub

    Private Sub rtbSource_MouseDoubleClick(sender As Object, e As MouseEventArgs) Handles rtbSource.MouseDoubleClick
        If e.Button <> MouseButtons.Left Then Return

        Dim charIndex = rtbSource.GetCharIndexFromPosition(e.Location)
        ToggleBreakpointAtLine(rtbSource.GetLineFromCharIndex(charIndex))

        Dim word = GetWordAtRichTextPoint(rtbSource, e.Location)
        If word <> "" Then
            HighlightWordEverywhere(word)
        End If
    End Sub

    Private Sub rtbSource_KeyDown(sender As Object, e As KeyEventArgs) Handles rtbSource.KeyDown
        If e.Control AndAlso e.KeyCode = Keys.Enter Then
            CommitCurrentLogic(True)
            e.SuppressKeyPress = True
            Return
        End If

        If e.KeyCode = Keys.F3 Then
            Dim searchWord = If(_globalHighlightWord <> "", _globalHighlightWord, rtbSource.SelectedText.Trim())
            If searchWord <> "" Then
                JumpToNextOccurrenceInSource(searchWord, rtbSource.SelectionStart + rtbSource.SelectionLength)
            End If
            e.SuppressKeyPress = True
            Return
        End If

        If e.KeyCode <> Keys.F9 Then Return

        ToggleBreakpointAtLine(rtbSource.GetLineFromCharIndex(rtbSource.SelectionStart))
        e.SuppressKeyPress = True
    End Sub

    Private Sub rtbSource_MouseDown(sender As Object, e As MouseEventArgs) Handles rtbSource.MouseDown
        If e.Button = MouseButtons.Right Then
            Dim word = GetWordAtRichTextPoint(rtbSource, e.Location)
            If Not String.IsNullOrWhiteSpace(word) Then
                ScrollToPredicateDefinition(word)
            End If
        End If
    End Sub

    Private Sub rtbSource_MouseClick(sender As Object, e As MouseEventArgs) Handles rtbSource.MouseClick
        If e.Button <> MouseButtons.Left OrElse Not CanUseControl(rtbSource) Then Return

        Dim charIdx = rtbSource.GetCharIndexFromPosition(e.Location)
        If charIdx < 0 OrElse charIdx >= rtbSource.TextLength Then Return

        Dim lineNum = rtbSource.GetLineFromCharIndex(charIdx) + 1
        Dim clickedWord = GetWordAtRichTextPoint(rtbSource, e.Location)

        ' Reverse Synchronization: Sync clicked clause/word to tree controls & graphical diagram
        SyncSourceClickToTreeControls(lineNum, clickedWord)

        If String.IsNullOrWhiteSpace(clickedWord) Then Return

        If Not String.IsNullOrWhiteSpace(_globalHighlightWord) AndAlso
           clickedWord.Equals(_globalHighlightWord, StringComparison.OrdinalIgnoreCase) Then
            JumpToNextOccurrenceInSource(_globalHighlightWord, charIdx + clickedWord.Length)
        Else
            HighlightWordEverywhere(clickedWord)
        End If
    End Sub

    Private Sub SyncSourceClickToTreeControls(lineNum As Integer, word As String)
        ' 1. Reverse Sync to Graphical Hierarchy Canvas
        If CanUseControl(_graphicalHierarchyCanvas) Then
            _graphicalHierarchyCanvas.SelectAndScrollToNodeByLineOrText(lineNum, word)
        End If

        ' 2. Reverse Sync to Call Hierarchy Tree List
        If CanUseControl(_callHierarchyTree) Then
            Dim matchNode = FindTreeNodeByLineOrText(_callHierarchyTree.Nodes, lineNum, word)
            If matchNode IsNot Nothing Then
                _callHierarchyTree.SelectedNode = matchNode
                matchNode.EnsureVisible()
            End If
        End If

        ' 3. Reverse Sync to Command Tree
        If CanUseControl(lstCommands) Then
            Dim matchNode = FindTreeNodeByLineOrText(lstCommands.Nodes, lineNum, word)
            If matchNode IsNot Nothing Then
                lstCommands.SelectedNode = matchNode
                matchNode.EnsureVisible()
            End If
        End If
    End Sub

    Private Function FindTreeNodeByLineOrText(nodes As TreeNodeCollection, lineNum As Integer, word As String) As TreeNode
        If nodes Is Nothing Then Return Nothing

        For Each node As TreeNode In nodes
            If lineNum > 0 AndAlso node.Tag IsNot Nothing Then
                Dim nLine As Integer = 0
                If Integer.TryParse(node.Tag.ToString(), nLine) AndAlso nLine = lineNum Then Return node
            End If
            If word <> "" AndAlso ContainsWholeWord(node.Text, word) Then Return node

            Dim childHit = FindTreeNodeByLineOrText(node.Nodes, lineNum, word)
            If childHit IsNot Nothing Then Return childHit
        Next
        Return Nothing
    End Function

    Private Sub JumpToNextOccurrenceInSource(word As String, startFromCharIndex As Integer)
        If Not CanUseControl(rtbSource) OrElse String.IsNullOrWhiteSpace(word) Then Return

        Dim text = rtbSource.Text
        Dim searchWord = word.Trim()
        Dim searchStart = Math.Max(0, startFromCharIndex)

        ' 1. Search forward from current position
        Dim nextIdx = FindNextWholeWordMatch(text, searchWord, searchStart)

        ' 2. If no next match after current position, wrap around to start of file
        If nextIdx < 0 Then
            nextIdx = FindNextWholeWordMatch(text, searchWord, 0)
        End If

        ' 3. Select, highlight, and scroll caret into view
        If nextIdx >= 0 Then
            rtbSource.Select(nextIdx, searchWord.Length)
            rtbSource.SelectionBackColor = Color.Orange
            rtbSource.ScrollToCaret()
            rtbSource.Refresh()
        End If
    End Sub

    Private Function FindNextWholeWordMatch(text As String, word As String, startIdx As Integer) As Integer
        Dim idx = startIdx
        While idx < text.Length
            idx = text.IndexOf(word, idx, StringComparison.OrdinalIgnoreCase)
            If idx < 0 Then Exit While

            If IsWholeWordMatch(text, idx, word.Length) Then
                Return idx
            End If
            idx += Math.Max(word.Length, 1)
        End While
        Return -1
    End Function

    Private Sub rtbDebug_MouseDoubleClick(sender As Object, e As MouseEventArgs) Handles rtbDebug.MouseDoubleClick
        If e.Button <> MouseButtons.Left Then Return

        Dim word = GetWordAtRichTextPoint(rtbDebug, e.Location)
        If word <> "" Then
            HighlightWordEverywhere(word)
        End If
    End Sub

    Private Sub lstCommands_MouseDoubleClickGlobal(sender As Object, e As MouseEventArgs) Handles lstCommands.MouseDoubleClick
        Dim node = lstCommands.GetNodeAt(e.Location)
        If node Is Nothing Then Return

        Dim nodeX = Math.Max(0, e.X - node.Bounds.Left)
        Dim word = GetWordAtListPoint(node.Text, nodeX)
        If word <> "" Then
            HighlightWordEverywhere(word)
        End If
    End Sub

    Private Sub Form1_Resize(sender As Object, e As EventArgs) Handles Me.Resize
        If _isClosing Then Exit Sub

        resizeTimer.Stop()
        resizeTimer.Start()
    End Sub

    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        _isClosing = True
        _stopRequested = True
        SetPauseReplayEnabled(False)
        SetRandomPlayEnabled(False)
        If resizeTimer IsNot Nothing Then
            resizeTimer.Stop()
        End If
    End Sub

    Private Sub picCanvas_Click(sender As Object, e As EventArgs) Handles picCanvas.Click

    End Sub
End Class

' ── Graphical Tree Control Classes for Call Hierarchy ───────────────────────

Public Class HierarchyGraphicNode
    Public Property Text As String = ""
    Public Property LineNumber As Integer = 0
    Public Property Depth As Integer = 0
    Public Property Status As String = "normal" ' "normal", "rule", "fact", "goal", "success", "fail"
    Public Property Children As New List(Of HierarchyGraphicNode)()
    Public Property ParentNode As HierarchyGraphicNode
    Public Property Bounds As RectangleF
    Public Property IsSelected As Boolean = False
    Public Property IsHovered As Boolean = False
End Class

Public Class GraphicalHierarchyCanvas
    Inherits Control

    Public Event NodeSelected(node As HierarchyGraphicNode)
    Public Event NodeRightClicked(node As HierarchyGraphicNode)

    Private _rootNodes As New List(Of HierarchyGraphicNode)()
    Private _selectedNode As HierarchyGraphicNode = Nothing
    Private _hoveredNode As HierarchyGraphicNode = Nothing
    Private _tooltip As New ToolTip()

    Public Sub New()
        Me.DoubleBuffered = True
        Me.BackColor = Color.FromArgb(246, 248, 252)
        Me.Font = New Font("Segoe UI", 9.0F)
    End Sub

    <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)>
    Public Property RootNodes As List(Of HierarchyGraphicNode)
        Get
            Return _rootNodes
        End Get
        Set(value As List(Of HierarchyGraphicNode))
            _rootNodes = value
            RecalculateLayout()
            Invalidate()
        End Set
    End Property

    Public Sub ScrollNodeIntoView(node As HierarchyGraphicNode)
        If node Is Nothing OrElse Me.Parent Is Nothing Then Return

        Dim scrollPanel = TryCast(Me.Parent, Panel)
        If scrollPanel IsNot Nothing Then
            Dim targetY = CInt(Math.Max(0.0F, node.Bounds.Top - 20.0F))
            Dim targetX = CInt(Math.Max(0.0F, node.Bounds.Left - 20.0F))
            scrollPanel.AutoScrollPosition = New Point(targetX, targetY)
        End If
    End Sub

    Public Function SelectAndScrollToNodeByLineOrText(lineNum As Integer, word As String) As HierarchyGraphicNode
        Dim match = FindNodeByLineOrText(_rootNodes, lineNum, word)
        If match IsNot Nothing Then
            _selectedNode = match
            ScrollNodeIntoView(match)
            Invalidate()
        End If
        Return match
    End Function

    Private Function FindNodeByLineOrText(nodes As List(Of HierarchyGraphicNode), lineNum As Integer, word As String) As HierarchyGraphicNode
        If nodes Is Nothing Then Return Nothing

        For Each n In nodes
            If lineNum > 0 AndAlso n.LineNumber = lineNum Then Return n
            If word <> "" AndAlso n.Text.Contains(word, StringComparison.OrdinalIgnoreCase) Then Return n

            Dim childHit = FindNodeByLineOrText(n.Children, lineNum, word)
            If childHit IsNot Nothing Then Return childHit
        Next
        Return Nothing
    End Function

    Public Sub RecalculateLayout()
        Dim startY As Single = 20.0F
        Dim currentY As Single = startY
        Dim cardWidth As Single = 875.0F ' 2.5X wider than original 350px for ample text display
        Dim cardHeight As Single = 44.0F
        Dim indentX As Single = 45.0F

        For Each root In _rootNodes
            currentY = LayoutNode(root, 20.0F, currentY, cardWidth, cardHeight, indentX) + 16.0F
        Next

        Dim parentW As Integer = If(Me.Parent IsNot Nothing, Me.Parent.Width, 1200)
        Dim parentH As Integer = If(Me.Parent IsNot Nothing, Me.Parent.Height, 600)
        Dim totalWidth = Math.Max(parentW, CInt(indentX * 8 + cardWidth + 100))
        Dim totalHeight = Math.Max(parentH, CInt(currentY + 60))
        Me.Size = New Size(totalWidth, totalHeight)
    End Sub

    Private Function LayoutNode(node As HierarchyGraphicNode, x As Single, y As Single, width As Single, height As Single, indent As Single) As Single
        node.Bounds = New RectangleF(x, y, width, height)
        Dim nextY = y + height + 16.0F

        For Each child In node.Children
            child.ParentNode = node
            nextY = LayoutNode(child, x + indent, nextY, Math.Max(650.0F, width - 30.0F), height, indent)
        Next

        Return nextY
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Dim g = e.Graphics
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit

        ' Draw connector lines first
        For Each root In _rootNodes
            DrawConnections(g, root)
        Next

        ' Draw node card boxes on top
        For Each root In _rootNodes
            DrawNodeCard(g, root)
        Next
    End Sub

    Private Sub DrawConnections(g As Graphics, node As HierarchyGraphicNode)
        If node.Children.Count > 0 Then
            Using pen As New Pen(Color.FromArgb(150, 175, 200), 2.0F)
                Dim parentPt = New PointF(node.Bounds.Left + 25.0F, node.Bounds.Bottom)

                For Each child In node.Children
                    Dim childPt = New PointF(child.Bounds.Left, child.Bounds.Top + child.Bounds.Height / 2.0F)
                    Dim cp1 = New PointF(parentPt.X, parentPt.Y + (childPt.Y - parentPt.Y) * 0.5F)
                    Dim cp2 = New PointF(childPt.X - 20.0F, childPt.Y)
                    g.DrawBezier(pen, parentPt, cp1, cp2, childPt)

                    g.FillEllipse(Brushes.SteelBlue, childPt.X - 3.0F, childPt.Y - 3.0F, 6.0F, 6.0F)
                    DrawConnections(g, child)
                Next
            End Using
        End If
    End Sub

    Private Sub DrawNodeCard(g As Graphics, node As HierarchyGraphicNode)
        Dim rect = node.Bounds
        Dim isSel = (node Is _selectedNode)
        Dim isHov = (node Is _hoveredNode)

        Dim headerColor As Color = Color.FromArgb(70, 90, 120)
        Dim borderColor As Color = Color.FromArgb(180, 195, 215)
        Dim cardBg = Color.White
        Dim badgeText = "GOAL"

        If node.Status = "rule" OrElse node.Text.StartsWith("[Rule]") Then
            headerColor = Color.FromArgb(41, 74, 130)
            borderColor = Color.FromArgb(92, 131, 196)
            badgeText = "RULE"
        ElseIf node.Status = "fact" OrElse node.Text.StartsWith("[Fact]") Then
            headerColor = Color.FromArgb(34, 112, 70)
            borderColor = Color.FromArgb(78, 168, 118)
            badgeText = "FACT"
        ElseIf node.Status = "success" OrElse node.Text.StartsWith("success:") OrElse node.Text.StartsWith("✓") Then
            headerColor = Color.FromArgb(28, 120, 50)
            borderColor = Color.FromArgb(46, 184, 84)
            cardBg = Color.FromArgb(244, 253, 246)
            badgeText = "✓ PASS"
        ElseIf node.Status = "fail" OrElse node.Text.StartsWith("fail") OrElse node.Text.StartsWith("✗") Then
            headerColor = Color.FromArgb(160, 45, 45)
            borderColor = Color.FromArgb(215, 80, 80)
            cardBg = Color.FromArgb(254, 245, 245)
            badgeText = "✗ FAIL"
        End If

        Dim shadowRect = rect
        shadowRect.Offset(2.0F, 2.0F)
        Using shadowBrush As New SolidBrush(Color.FromArgb(25, 0, 0, 0))
            g.FillRectangle(shadowBrush, shadowRect)
        End Using

        Using bgBrush As New SolidBrush(cardBg)
            g.FillRectangle(bgBrush, rect)
        End Using

        Dim stripeRect = New RectangleF(rect.Left, rect.Top, 6.0F, rect.Height)
        Using stripeBrush As New SolidBrush(headerColor)
            g.FillRectangle(stripeBrush, stripeRect)
        End Using

        Dim penWidth = If(isSel, 2.8F, If(isHov, 2.0F, 1.2F))
        Dim drawBorderColor = If(isSel, Color.FromArgb(255, 140, 0), If(isHov, headerColor, borderColor))
        Using borderPen As New Pen(drawBorderColor, penWidth)
            g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height)
        End Using

        Dim badgeWidth As Single = 54.0F
        Dim badgeHeight As Single = 18.0F
        Dim badgeRect = New RectangleF(rect.Right - badgeWidth - 8.0F, rect.Top + 6.0F, badgeWidth, badgeHeight)
        Using badgeBrush As New SolidBrush(headerColor)
            g.FillRectangle(badgeBrush, badgeRect)
        End Using
        Using badgeFont As New Font("Segoe UI", 7.5F, FontStyle.Bold)
            Using sf As New StringFormat() With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
                g.DrawString(badgeText, badgeFont, Brushes.White, badgeRect, sf)
            End Using
        End Using

        Dim cleanText = node.Text
        For Each p In New String() {"[Rule]", "[Fact]", "  ├─ "}
            cleanText = cleanText.Replace(p, "").Trim()
        Next

        Dim textRect = New RectangleF(rect.Left + 12.0F, rect.Top + 4.0F, rect.Width - badgeWidth - 26.0F, rect.Height - 8.0F)
        Using textFont As New Font("Consolas", 9.0F, If(isSel, FontStyle.Bold, FontStyle.Regular))
            Using sf As New StringFormat() With {.LineAlignment = StringAlignment.Center, .Trimming = StringTrimming.EllipsisCharacter}
                g.DrawString(cleanText, textFont, Brushes.Black, textRect, sf)
            End Using
        End Using

        For Each child In node.Children
            DrawNodeCard(g, child)
        Next
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        Dim clickedNode = FindNodeAtPoint(_rootNodes, e.Location)
        If clickedNode IsNot Nothing Then
            _selectedNode = clickedNode
            Invalidate()
            If e.Button = MouseButtons.Left Then
                RaiseEvent NodeSelected(clickedNode)
            ElseIf e.Button = MouseButtons.Right Then
                RaiseEvent NodeRightClicked(clickedNode)
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim hovered = FindNodeAtPoint(_rootNodes, e.Location)
        If hovered IsNot _hoveredNode Then
            _hoveredNode = hovered
            Me.Cursor = If(hovered IsNot Nothing, Cursors.Hand, Cursors.Default)
            If hovered IsNot Nothing Then
                _tooltip.SetToolTip(Me, hovered.Text)
            Else
                _tooltip.Hide(Me)
            End If
            Invalidate()
        End If
    End Sub

    Private Function FindNodeAtPoint(nodes As List(Of HierarchyGraphicNode), pt As Point) As HierarchyGraphicNode
        For Each n In nodes
            If n.Bounds.Contains(pt) Then Return n
            Dim childHit = FindNodeAtPoint(n.Children, pt)
            If childHit IsNot Nothing Then Return childHit
        Next
        Return Nothing
    End Function
End Class
