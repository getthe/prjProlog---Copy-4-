<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Form1
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        components = New ComponentModel.Container()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(Form1))
        ToolStrip1 = New ToolStrip()
        btnRun = New ToolStripButton()

        btnStop = New ToolStripButton()
        btnGoAgain = New ToolStripButton()
        btnSaveScript = New ToolStripButton()
        btnClear = New ToolStripButton()
        tslTurn = New ToolStripLabel()
        ToolStripSeparator1 = New ToolStripSeparator()
        btnRandomPlay = New ToolStripButton()
        btnAutomaticPlay = New ToolStripButton()
        SplitContainer1 = New SplitContainer()
        SplitContainer4 = New SplitContainer()
        picCanvas = New PictureBox()
        tabInspector = New TabControl()
        tabMoves = New TabPage()
        dgvMoves = New DataGridView()
        tabIsEaten = New TabPage()
        dgvIsEaten = New DataGridView()
        tabCanGo = New TabPage()
        dgvCanGo = New DataGridView()
        tabCanEat = New TabPage()
        dgvCanEat = New DataGridView()
        tabCanBeEaten = New TabPage()
        dgvCanBeEaten = New DataGridView()
        tabTrace = New TabPage()
        dgvTrace = New DataGridView()
        SplitContainer2 = New SplitContainer()
        rtbSource = New RichTextBox()
        SplitContainer3 = New SplitContainer()
        lstCommands = New TreeView()
        rtbDebug = New RichTextBox()
        resizeTimer = New Timer(components)
        ToolStrip1.SuspendLayout()
        CType(SplitContainer1, ComponentModel.ISupportInitialize).BeginInit()
        SplitContainer1.Panel1.SuspendLayout()
        SplitContainer1.Panel2.SuspendLayout()
        SplitContainer1.SuspendLayout()
        CType(SplitContainer4, ComponentModel.ISupportInitialize).BeginInit()
        SplitContainer4.Panel1.SuspendLayout()
        SplitContainer4.Panel2.SuspendLayout()
        SplitContainer4.SuspendLayout()
        CType(picCanvas, ComponentModel.ISupportInitialize).BeginInit()
        tabInspector.SuspendLayout()
        tabMoves.SuspendLayout()
        CType(dgvMoves, ComponentModel.ISupportInitialize).BeginInit()
        tabIsEaten.SuspendLayout()
        CType(dgvIsEaten, ComponentModel.ISupportInitialize).BeginInit()
        tabCanGo.SuspendLayout()
        CType(dgvCanGo, ComponentModel.ISupportInitialize).BeginInit()
        tabCanEat.SuspendLayout()
        CType(dgvCanEat, ComponentModel.ISupportInitialize).BeginInit()
        tabCanBeEaten.SuspendLayout()
        CType(dgvCanBeEaten, ComponentModel.ISupportInitialize).BeginInit()
        tabTrace.SuspendLayout()
        CType(dgvTrace, ComponentModel.ISupportInitialize).BeginInit()
        CType(SplitContainer2, ComponentModel.ISupportInitialize).BeginInit()
        SplitContainer2.Panel1.SuspendLayout()
        SplitContainer2.Panel2.SuspendLayout()
        SplitContainer2.SuspendLayout()
        CType(SplitContainer3, ComponentModel.ISupportInitialize).BeginInit()
        SplitContainer3.Panel1.SuspendLayout()
        SplitContainer3.Panel2.SuspendLayout()
        SplitContainer3.SuspendLayout()
        SuspendLayout()
        ' 
        ' ToolStrip1
        ' 
        ToolStrip1.ImageScalingSize = New Size(48, 48)
        ToolStrip1.Items.AddRange(New ToolStripItem() {btnRun, btnStop, btnGoAgain, btnSaveScript, btnClear, tslTurn, ToolStripSeparator1, btnRandomPlay, btnAutomaticPlay})
        ToolStrip1.Location = New Point(0, 0)
        ToolStrip1.Name = "ToolStrip1"
        ToolStrip1.Padding = New Padding(0, 0, 3, 0)
        ToolStrip1.Size = New Size(2152, 42)
        ToolStrip1.TabIndex = 0
        ToolStrip1.Text = "ToolStrip1"
        ' 
        ' btnRun
        ' 
        btnRun.DisplayStyle = ToolStripItemDisplayStyle.Text
        btnRun.Image = CType(resources.GetObject("btnRun.Image"), Image)
        btnRun.ImageTransparentColor = Color.Magenta
        btnRun.Name = "btnRun"
        btnRun.Size = New Size(60, 36)
        btnRun.Text = "Run"
        ' 
        ' btnStop
        ' 
        btnStop.DisplayStyle = ToolStripItemDisplayStyle.Text
        btnStop.Enabled = False
        btnStop.Name = "btnStop"
        btnStop.Size = New Size(66, 36)
        btnStop.Text = "Stop"
        ' 
        ' btnGoAgain
        ' 
        btnGoAgain.DisplayStyle = ToolStripItemDisplayStyle.Text
        btnGoAgain.Name = "btnGoAgain"
        btnGoAgain.Size = New Size(116, 36)
        btnGoAgain.Text = "Go Again"
        ' 
        ' btnSaveScript
        ' 
        btnSaveScript.DisplayStyle = ToolStripItemDisplayStyle.Text
        btnSaveScript.Name = "btnSaveScript"
        btnSaveScript.Size = New Size(135, 36)
        btnSaveScript.Text = "Save Script"
        ' 
        ' btnClear
        ' 
        btnClear.DisplayStyle = ToolStripItemDisplayStyle.Text
        btnClear.Image = CType(resources.GetObject("btnClear.Image"), Image)
        btnClear.ImageTransparentColor = Color.Magenta
        btnClear.Name = "btnClear"
        btnClear.Size = New Size(72, 36)
        btnClear.Text = "Clear"
        ' 
        ' tslTurn
        ' 
        tslTurn.Font = New Font("Segoe UI", 9F, FontStyle.Bold Or FontStyle.Italic Or FontStyle.Underline)
        tslTurn.ForeColor = Color.Blue
        tslTurn.Name = "tslTurn"
        tslTurn.Size = New Size(123, 36)
        tslTurn.Text = "Blue Turn"
        ' 
        ' ToolStripSeparator1
        ' 
        ToolStripSeparator1.Name = "ToolStripSeparator1"
        ToolStripSeparator1.Size = New Size(6, 42)
        ' 
        ' btnRandomPlay
        ' 
        btnRandomPlay.DisplayStyle = ToolStripItemDisplayStyle.Text
        btnRandomPlay.Name = "btnRandomPlay"
        btnRandomPlay.Size = New Size(107, 36)
        btnRandomPlay.Text = "Random"
        ' 
        ' btnAutomaticPlay
        ' 
        btnAutomaticPlay.DisplayStyle = ToolStripItemDisplayStyle.Text
        btnAutomaticPlay.Name = "btnAutomaticPlay"
        btnAutomaticPlay.Size = New Size(107, 36)
        btnAutomaticPlay.Text = "Auto Play"
        ' 
        ' SplitContainer1
        ' 
        SplitContainer1.Dock = DockStyle.Fill
        SplitContainer1.Location = New Point(0, 42)
        SplitContainer1.Name = "SplitContainer1"
        SplitContainer1.Orientation = Orientation.Horizontal
        ' 
        ' SplitContainer1.Panel1
        ' 
        SplitContainer1.Panel1.Controls.Add(SplitContainer4)
        ' 
        ' SplitContainer1.Panel2
        ' 
        SplitContainer1.Panel2.Controls.Add(SplitContainer2)
        SplitContainer1.Size = New Size(2152, 1015)
        SplitContainer1.SplitterDistance = 334
        SplitContainer1.SplitterWidth = 3
        SplitContainer1.TabIndex = 1
        ' 
        ' SplitContainer4
        ' 
        SplitContainer4.Dock = DockStyle.Fill
        SplitContainer4.Location = New Point(0, 0)
        SplitContainer4.Name = "SplitContainer4"
        ' 
        ' SplitContainer4.Panel1
        ' 
        SplitContainer4.Panel1.Controls.Add(picCanvas)
        ' 
        ' SplitContainer4.Panel2
        ' 
        SplitContainer4.Panel2.Controls.Add(tabInspector)
        SplitContainer4.Size = New Size(2152, 334)
        SplitContainer4.SplitterDistance = 1538
        SplitContainer4.SplitterWidth = 3
        SplitContainer4.TabIndex = 0
        ' 
        ' picCanvas
        ' 
        picCanvas.BackColor = Color.White
        picCanvas.BorderStyle = BorderStyle.FixedSingle
        picCanvas.Dock = DockStyle.Fill
        picCanvas.Location = New Point(0, 0)
        picCanvas.Margin = New Padding(0)
        picCanvas.Name = "picCanvas"
        picCanvas.Size = New Size(1538, 334)
        picCanvas.TabIndex = 0
        picCanvas.TabStop = False
        ' 
        ' tabInspector
        ' 
        tabInspector.Controls.Add(tabMoves)
        tabInspector.Controls.Add(tabIsEaten)
        tabInspector.Controls.Add(tabCanGo)
        tabInspector.Controls.Add(tabCanEat)
        tabInspector.Controls.Add(tabCanBeEaten)
        tabInspector.Controls.Add(tabTrace)
        tabInspector.Dock = DockStyle.Fill
        tabInspector.Location = New Point(0, 0)
        tabInspector.Name = "tabInspector"
        tabInspector.SelectedIndex = 0
        tabInspector.Size = New Size(611, 334)
        tabInspector.TabIndex = 0
        ' 
        ' tabMoves
        ' 
        tabMoves.Controls.Add(dgvMoves)
        tabMoves.Location = New Point(8, 46)
        tabMoves.Name = "tabMoves"
        tabMoves.Padding = New Padding(3)
        tabMoves.Size = New Size(595, 280)
        tabMoves.TabIndex = 0
        tabMoves.Text = "Moves"
        tabMoves.UseVisualStyleBackColor = True
        ' 
        ' dgvMoves
        ' 
        dgvMoves.AllowUserToAddRows = False
        dgvMoves.AllowUserToDeleteRows = False
        dgvMoves.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        dgvMoves.Dock = DockStyle.Fill
        dgvMoves.Location = New Point(3, 3)
        dgvMoves.Name = "dgvMoves"
        dgvMoves.ReadOnly = True
        dgvMoves.RowHeadersVisible = False
        dgvMoves.RowHeadersWidth = 62
        dgvMoves.Size = New Size(589, 274)
        dgvMoves.TabIndex = 0
        ' 
        ' tabIsEaten
        ' 
        tabIsEaten.Controls.Add(dgvIsEaten)
        tabIsEaten.Location = New Point(8, 46)
        tabIsEaten.Name = "tabIsEaten"
        tabIsEaten.Padding = New Padding(3)
        tabIsEaten.Size = New Size(595, 280)
        tabIsEaten.TabIndex = 1
        tabIsEaten.Text = "isEaten"
        tabIsEaten.UseVisualStyleBackColor = True
        ' 
        ' dgvIsEaten
        ' 
        dgvIsEaten.AllowUserToAddRows = False
        dgvIsEaten.AllowUserToDeleteRows = False
        dgvIsEaten.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        dgvIsEaten.Dock = DockStyle.Fill
        dgvIsEaten.Location = New Point(3, 3)
        dgvIsEaten.Name = "dgvIsEaten"
        dgvIsEaten.ReadOnly = True
        dgvIsEaten.RowHeadersVisible = False
        dgvIsEaten.RowHeadersWidth = 62
        dgvIsEaten.Size = New Size(589, 274)
        dgvIsEaten.TabIndex = 0
        ' 
        ' tabCanGo
        ' 
        tabCanGo.Controls.Add(dgvCanGo)
        tabCanGo.Location = New Point(8, 46)
        tabCanGo.Name = "tabCanGo"
        tabCanGo.Padding = New Padding(3)
        tabCanGo.Size = New Size(595, 280)
        tabCanGo.TabIndex = 2
        tabCanGo.Text = "canGo"
        tabCanGo.UseVisualStyleBackColor = True
        ' 
        ' dgvCanGo
        ' 
        dgvCanGo.AllowUserToAddRows = False
        dgvCanGo.AllowUserToDeleteRows = False
        dgvCanGo.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        dgvCanGo.Dock = DockStyle.Fill
        dgvCanGo.Location = New Point(3, 3)
        dgvCanGo.Name = "dgvCanGo"
        dgvCanGo.ReadOnly = True
        dgvCanGo.RowHeadersVisible = False
        dgvCanGo.RowHeadersWidth = 62
        dgvCanGo.Size = New Size(589, 274)
        dgvCanGo.TabIndex = 0
        ' 
        ' tabCanEat
        ' 
        tabCanEat.Controls.Add(dgvCanEat)
        tabCanEat.Location = New Point(8, 46)
        tabCanEat.Name = "tabCanEat"
        tabCanEat.Padding = New Padding(3)
        tabCanEat.Size = New Size(595, 280)
        tabCanEat.TabIndex = 3
        tabCanEat.Text = "canEat"
        tabCanEat.UseVisualStyleBackColor = True
        ' 
        ' dgvCanEat
        ' 
        dgvCanEat.AllowUserToAddRows = False
        dgvCanEat.AllowUserToDeleteRows = False
        dgvCanEat.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        dgvCanEat.Dock = DockStyle.Fill
        dgvCanEat.Location = New Point(3, 3)
        dgvCanEat.Name = "dgvCanEat"
        dgvCanEat.ReadOnly = True
        dgvCanEat.RowHeadersVisible = False
        dgvCanEat.RowHeadersWidth = 62
        dgvCanEat.Size = New Size(589, 274)
        dgvCanEat.TabIndex = 0
        ' 
        ' tabCanBeEaten
        ' 
        tabCanBeEaten.Controls.Add(dgvCanBeEaten)
        tabCanBeEaten.Location = New Point(8, 46)
        tabCanBeEaten.Name = "tabCanBeEaten"
        tabCanBeEaten.Padding = New Padding(3)
        tabCanBeEaten.Size = New Size(595, 280)
        tabCanBeEaten.TabIndex = 4
        tabCanBeEaten.Text = "canBeEaten"
        tabCanBeEaten.UseVisualStyleBackColor = True
        ' 
        ' dgvCanBeEaten
        ' 
        dgvCanBeEaten.AllowUserToAddRows = False
        dgvCanBeEaten.AllowUserToDeleteRows = False
        dgvCanBeEaten.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        dgvCanBeEaten.Dock = DockStyle.Fill
        dgvCanBeEaten.Location = New Point(3, 3)
        dgvCanBeEaten.Name = "dgvCanBeEaten"
        dgvCanBeEaten.ReadOnly = True
        dgvCanBeEaten.RowHeadersVisible = False
        dgvCanBeEaten.RowHeadersWidth = 62
        dgvCanBeEaten.Size = New Size(589, 274)
        dgvCanBeEaten.TabIndex = 0
        ' 
        ' tabTrace
        ' 
        tabTrace.Controls.Add(dgvTrace)
        tabTrace.Location = New Point(8, 46)
        tabTrace.Name = "tabTrace"
        tabTrace.Padding = New Padding(3)
        tabTrace.Size = New Size(595, 280)
        tabTrace.TabIndex = 5
        tabTrace.Text = "Trace"
        tabTrace.UseVisualStyleBackColor = True
        ' 
        ' dgvTrace
        ' 
        dgvTrace.AllowUserToAddRows = False
        dgvTrace.AllowUserToDeleteRows = False
        dgvTrace.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        dgvTrace.Dock = DockStyle.Fill
        dgvTrace.Location = New Point(3, 3)
        dgvTrace.Name = "dgvTrace"
        dgvTrace.ReadOnly = True
        dgvTrace.RowHeadersVisible = False
        dgvTrace.RowHeadersWidth = 62
        dgvTrace.Size = New Size(589, 274)
        dgvTrace.TabIndex = 0
        ' 
        ' SplitContainer2
        ' 
        SplitContainer2.Dock = DockStyle.Fill
        SplitContainer2.Location = New Point(0, 0)
        SplitContainer2.Name = "SplitContainer2"
        ' 
        ' SplitContainer2.Panel1
        ' 
        SplitContainer2.Panel1.Controls.Add(rtbSource)
        ' 
        ' SplitContainer2.Panel2
        ' 
        SplitContainer2.Panel2.Controls.Add(SplitContainer3)
        SplitContainer2.Size = New Size(2152, 678)
        SplitContainer2.SplitterDistance = 1074
        SplitContainer2.SplitterWidth = 3
        SplitContainer2.TabIndex = 0
        ' 
        ' rtbSource
        ' 
        rtbSource.Dock = DockStyle.Fill
        rtbSource.Location = New Point(0, 0)
        rtbSource.Name = "rtbSource"
        rtbSource.Size = New Size(1074, 678)
        rtbSource.TabIndex = 0
        rtbSource.Text = ""
        ' 
        ' SplitContainer3
        ' 
        SplitContainer3.Dock = DockStyle.Fill
        SplitContainer3.Location = New Point(0, 0)
        SplitContainer3.Name = "SplitContainer3"
        ' 
        ' SplitContainer3.Panel1
        ' 
        SplitContainer3.Panel1.Controls.Add(lstCommands)
        ' 
        ' SplitContainer3.Panel2
        ' 
        SplitContainer3.Panel2.Controls.Add(rtbDebug)
        SplitContainer3.Size = New Size(1075, 678)
        SplitContainer3.SplitterDistance = 492
        SplitContainer3.SplitterWidth = 3
        SplitContainer3.TabIndex = 0
        ' 
        ' lstCommands
        ' 
        lstCommands.Dock = DockStyle.Fill
        lstCommands.Location = New Point(0, 0)
        lstCommands.Name = "lstCommands"
        lstCommands.Size = New Size(492, 678)
        lstCommands.TabIndex = 0
        ' 
        ' rtbDebug
        ' 
        rtbDebug.Dock = DockStyle.Fill
        rtbDebug.Font = New Font("Sitka Heading", 9.0F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        rtbDebug.Location = New Point(0, 0)
        rtbDebug.Name = "rtbDebug"
        rtbDebug.Size = New Size(580, 678)
        rtbDebug.TabIndex = 2
        rtbDebug.Text = ""
        ' 
        ' resizeTimer
        ' 
        ' 
        ' Form1
        ' 
        AutoScaleDimensions = New SizeF(13F, 32F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(2152, 1057)
        Controls.Add(SplitContainer1)
        Controls.Add(ToolStrip1)
        Name = "Form1"
        Text = "Form1"
        ToolStrip1.ResumeLayout(False)
        ToolStrip1.PerformLayout()
        SplitContainer1.Panel1.ResumeLayout(False)
        SplitContainer1.Panel2.ResumeLayout(False)
        CType(SplitContainer1, ComponentModel.ISupportInitialize).EndInit()
        SplitContainer1.ResumeLayout(False)
        SplitContainer4.Panel1.ResumeLayout(False)
        SplitContainer4.Panel2.ResumeLayout(False)
        CType(SplitContainer4, ComponentModel.ISupportInitialize).EndInit()
        SplitContainer4.ResumeLayout(False)
        CType(picCanvas, ComponentModel.ISupportInitialize).EndInit()
        tabInspector.ResumeLayout(False)
        tabMoves.ResumeLayout(False)
        CType(dgvMoves, ComponentModel.ISupportInitialize).EndInit()
        tabIsEaten.ResumeLayout(False)
        CType(dgvIsEaten, ComponentModel.ISupportInitialize).EndInit()
        tabCanGo.ResumeLayout(False)
        CType(dgvCanGo, ComponentModel.ISupportInitialize).EndInit()
        tabCanEat.ResumeLayout(False)
        CType(dgvCanEat, ComponentModel.ISupportInitialize).EndInit()
        tabCanBeEaten.ResumeLayout(False)
        CType(dgvCanBeEaten, ComponentModel.ISupportInitialize).EndInit()
        tabTrace.ResumeLayout(False)
        CType(dgvTrace, ComponentModel.ISupportInitialize).EndInit()
        SplitContainer2.Panel1.ResumeLayout(False)
        SplitContainer2.Panel2.ResumeLayout(False)
        CType(SplitContainer2, ComponentModel.ISupportInitialize).EndInit()
        SplitContainer2.ResumeLayout(False)
        SplitContainer3.Panel1.ResumeLayout(False)
        SplitContainer3.Panel2.ResumeLayout(False)
        CType(SplitContainer3, ComponentModel.ISupportInitialize).EndInit()
        SplitContainer3.ResumeLayout(False)
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Friend WithEvents ToolStrip1 As ToolStrip
    Friend WithEvents btnRun As ToolStripButton
    Friend WithEvents btnStop As ToolStripButton
    Friend WithEvents btnGoAgain As ToolStripButton
    Friend WithEvents btnSaveScript As ToolStripButton
    Friend WithEvents btnClear As ToolStripButton
    Friend WithEvents tslTurn As ToolStripLabel
    Friend WithEvents SplitContainer1 As SplitContainer
    Friend WithEvents SplitContainer4 As SplitContainer
    Friend WithEvents picCanvas As PictureBox
    Friend WithEvents tabInspector As TabControl
    Friend WithEvents tabMoves As TabPage
    Friend WithEvents dgvMoves As DataGridView
    Friend WithEvents tabIsEaten As TabPage
    Friend WithEvents dgvIsEaten As DataGridView
    Friend WithEvents tabCanGo As TabPage
    Friend WithEvents dgvCanGo As DataGridView
    Friend WithEvents tabCanEat As TabPage
    Friend WithEvents dgvCanEat As DataGridView
    Friend WithEvents tabCanBeEaten As TabPage
    Friend WithEvents dgvCanBeEaten As DataGridView
    Friend WithEvents tabTrace As TabPage
    Friend WithEvents dgvTrace As DataGridView
    Friend WithEvents SplitContainer2 As SplitContainer
    Friend WithEvents rtbSource As RichTextBox
    Friend WithEvents SplitContainer3 As SplitContainer
    Friend WithEvents rtbDebug As RichTextBox
    Friend WithEvents lstCommands As TreeView
    Friend WithEvents resizeTimer As Timer
    Friend WithEvents ToolStripSeparator1 As ToolStripSeparator
    Friend WithEvents btnRandomPlay As ToolStripButton
    Friend WithEvents btnAutomaticPlay As ToolStripButton

End Class
