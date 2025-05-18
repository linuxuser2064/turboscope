Imports System.ComponentModel
Imports System.Drawing.Design
Imports System.IO
Imports System.Windows.Forms.Design
Imports FFMediaToolkit
Imports FFMediaToolkit.Encoding
Imports NAudio.Wave
Public Class Form1
    Public Channels As New List(Of OscilloscopeChannel)
    Dim ChannelCount As Int32 = 3

    Dim CRTScope As Boolean = True
    Dim LineWidth As Int32 = 5
    Dim LineClr As Color
    Dim BackClr As Color = Color.Black

    Dim CustomRunningTimeEnabled As Boolean = False
    Dim CustomRunningTime As Long = 0
    Dim VideoOutputPath As String
    Dim MasterAudio As String
    Dim SampleRate As Int32 = 44100 ' stub
    Dim VideoSize As New Size(1280, 720) ' stub
    Dim progress As Long = 0
    Dim maxprog As Long = 0
    Public Function LoadWavSamples(filePath As String) As (samples As Single(), sampleRate As Integer)
        Dim reader As ISampleProvider = New AudioFileReader(filePath).ToMono
        Dim sampleRate = reader.WaveFormat.SampleRate
        Dim sampleList As New List(Of Single)
        Dim buffer(1023) As Single
        Dim read As Integer
        Do
            read = reader.Read(buffer, 0, buffer.Length)
            If read > 0 Then
                sampleList.AddRange(buffer.Take(read))
            End If
        Loop While read > 0
        Return (sampleList.ToArray(), sampleRate)
    End Function
    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        SaveSets()
        Timer1.Start()
        VideoWorker.RunWorkerAsync()
    End Sub
    Dim teringwatch As New Stopwatch
    Private Sub VideoWorker_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles VideoWorker.DoWork
        teringwatch.Start()
        FFmpegLoader.FFmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg\x86_64")

        For i = 0 To Channels.Count - 1
            If Channels(i).Enabled Then
                Channels(i).AudioData = LoadWavSamples(Channels(i).AudioFile)
                If Channels(i).Multiplier <> 1 Then
                    For o = 0 To Channels(i).AudioData.samples.Length - 1
                        Channels(i).AudioData.samples(o) *= Channels(i).Multiplier
                    Next
                End If
            End If
        Next

        Dim length = Channels(0).AudioData.samples.Length
        maxprog = length
        Dim bmp As New Bitmap(VideoSize.Width, VideoSize.Height)
        Using g As Drawing.Graphics = Drawing.Graphics.FromImage(bmp)
            Dim audioCounter As Long = 0
            Dim clr As New Pen(LineClr)
            Dim samplesPerFrame As Integer = SampleRate \ 50
            Dim drawSampleCount As Integer = SampleRate \ 16

            Dim vid = MediaBuilder.CreateContainer($"{VideoOutputPath}_raw.mp4", ContainerFormat.MP4).
            WithVideo(New VideoEncoderSettings(VideoSize.Width, VideoSize.Height, 50, VideoCodec.Default)).
            Create

            While audioCounter < length
                If CustomRunningTimeEnabled AndAlso audioCounter >= CustomRunningTime Then Exit While
                If audioCounter + samplesPerFrame >= length Then Exit While

                g.Clear(BackClr)

                For Each ch In Channels
                    If ch.Enabled Then
                        Dim smp = ch.AudioData.samples
                        If audioCounter + drawSampleCount < smp.Length Then
                            Dim smpData = smp.AsSpan().Slice(CInt(audioCounter), drawSampleCount).ToArray()
                            DrawChannel(g, ch, smpData, clr)
                        End If
                    End If
                Next

                BitmapToImageData.my_ass.AddBitmapFrame(vid, bmp)

                audioCounter += samplesPerFrame
                progress = audioCounter
            End While

            vid.Dispose()
        End Using

        Dim ffmpegArgs = $"-i ""{VideoOutputPath}_raw.mp4"" -i ""{MasterAudio}"" -shortest -c copy -map 0:v:0 -map 1:a:0 ""{Application.StartupPath}\out.mp4"""
        Dim psi As New ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg\x86_64\ffmpeg.exe"), ffmpegArgs) With {
        .UseShellExecute = False,
        .CreateNoWindow = True
    }
        progress = -1
        Using proc = Process.Start(psi)
            proc.WaitForExit()
        End Using
        File.Copy(Path.Combine(Application.StartupPath, "out.mp4"), VideoOutputPath, True)
        File.Delete(Path.Combine(Application.StartupPath, "out.mp4"))
        File.Delete($"{VideoOutputPath}_raw.mp4")
        teringwatch.Stop()
        MsgBox("Done!")
        Me.Text = "turboscope"
    End Sub
    Sub DrawChannel(ByRef g As Drawing.Graphics, ByRef channel As OscilloscopeChannel, songData As Single(), ByVal lineColor As Pen)
        Dim width = channel.Width
        Dim height = channel.Height
        Dim xOffset = channel.X
        Dim yOffset = channel.Y

        Dim maxTrig As Single = songData.Skip(width \ 2).Take(songData.Length - width).Max()
        Dim minTrig As Single = songData.Skip(width \ 2).Take(songData.Length - width).Min()
        Dim triggerLevel As Single = (maxTrig + minTrig) / 2
        Dim hysteresis As Single = 0.01F

        Dim index = Enumerable.Range(width \ 2, songData.Length - width - 1).
        FirstOrDefault(Function(i) songData(i) < triggerLevel AndAlso songData(i + 1) >= triggerLevel + hysteresis)

        index -= width \ 2
        index = Math.Max(0, Math.Min(index, songData.Length - width - 1))

        Dim prevX = songData(index)
        For i = index To index + width - 1
            Dim x = songData(i)
            Dim y1 = CInt((height / 2) - (x * (height / 2)) + yOffset) ' calculate y of current value
            Dim y0 = CInt((height / 2) - (prevX * (height / 2)) + yOffset) ' calculate y of previous song value
            If CRTScope Then
                For i2 = -(LineWidth \ 2) To (LineWidth \ 2)
                    g.DrawLine(lineColor, i - index + xOffset, y1 + i2, i - index - 1 + xOffset, y0 + 1 + i2)
                Next
            Else
                lineColor.Width = LineWidth
                lineColor.StartCap = Drawing2D.LineCap.Round
                lineColor.EndCap = Drawing2D.LineCap.Round
                lineColor.LineJoin = Drawing2D.LineJoin.Round
                g.DrawLine(lineColor, i - index + xOffset, y1, i - index - 1 + xOffset, y0 + 1)
            End If
            prevX = x
        Next
    End Sub
    Private Sub SaveSets()
        MasterAudio = TextBox4.Text
        CustomRunningTimeEnabled = CheckBox6.Checked
        If CustomRunningTimeEnabled Then
            CustomRunningTime = NumericUpDown1.Value * SampleRate
        End If
        VideoOutputPath = TextBox5.Text
        CRTScope = CheckBox4.Checked
        LineWidth = NumericUpDown3.Value
        LineClr = PictureBox1.BackColor
        BackClr = PictureBox2.BackColor
    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        MasterAudioOpenFileDialog1.ShowDialog()
    End Sub

    Private Sub MasterAudioOpenFileDialog1_FileOk(sender As Object, e As System.ComponentModel.CancelEventArgs) Handles MasterAudioOpenFileDialog1.FileOk
        TextBox4.Text = MasterAudioOpenFileDialog1.FileName
    End Sub

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click
        SaveFileDialog1.ShowDialog()
    End Sub

    Private Sub SaveFileDialog1_FileOk(sender As Object, e As System.ComponentModel.CancelEventArgs) Handles SaveFileDialog1.FileOk
        TextBox5.Text = SaveFileDialog1.FileName
    End Sub

    Private Sub Button7_Click(sender As Object, e As EventArgs) Handles Button7.Click
        ChannelCount = 4
        UpdateChannels()
        Channels(0).X = 0
        Channels(0).Y = 0
        Channels(0).Width = 1280
        Channels(0).Height = 180
        Channels(0).Enabled = True
        Channels(1).X = 0
        Channels(1).Y = 180
        Channels(1).Width = 1280
        Channels(1).Height = 180
        Channels(1).Enabled = True
        Channels(2).X = 0
        Channels(2).Y = 360
        Channels(2).Width = 1280
        Channels(2).Height = 180
        Channels(2).Enabled = True
        Channels(3).X = 0
        Channels(3).Y = 540
        Channels(3).Width = 1280
        Channels(3).Height = 180
        Channels(3).Enabled = True
        PropertyGrid1.Refresh()
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        ChannelCount = 3
        UpdateChannels()
        Channels(0).X = 0
        Channels(0).Y = 0
        Channels(0).Width = 1280
        Channels(0).Height = 240
        Channels(0).Enabled = True
        Channels(1).X = 0
        Channels(1).Y = 240
        Channels(1).Width = 1280
        Channels(1).Height = 240
        Channels(1).Enabled = True
        Channels(2).X = 0
        Channels(2).Y = 480
        Channels(2).Width = 1280
        Channels(2).Height = 240
        Channels(2).Enabled = True
        PropertyGrid1.Refresh()
    End Sub

    Private Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click
        ChannelCount = 2
        UpdateChannels()
        Channels(0).X = 0
        Channels(0).Y = 0
        Channels(0).Width = 1280
        Channels(0).Height = 360
        Channels(0).Enabled = True
        Channels(1).X = 0
        Channels(1).Y = 360
        Channels(1).Width = 1280
        Channels(1).Height = 360
        Channels(1).Enabled = True
        PropertyGrid1.Refresh()
    End Sub

    Private Sub Button6_Click(sender As Object, e As EventArgs) Handles Button6.Click
        ChannelCount = 1
        UpdateChannels()
        Channels(0).X = 0
        Channels(0).Y = 0
        Channels(0).Width = 1280
        Channels(0).Height = 720
        Channels(0).Enabled = True
        PropertyGrid1.Refresh()
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        PictureBox1.BackColor = ColorTranslator.FromHtml("#7AFF3E")
        LineClr = ColorTranslator.FromHtml("#7AFF3E")
        ForegroundColorDlg.Color = ColorTranslator.FromHtml("#7AFF3E")
        For i = 0 To 2
            Channels.Add(New OscilloscopeChannel)
        Next
        Channels(0).X = NumericUpDown11.Value
        Channels(0).Y = NumericUpDown12.Value
        Channels(0).Width = NumericUpDown13.Value
        Channels(0).Height = NumericUpDown14.Value
        Channels(0).Enabled = CheckBox1.Checked
        Channels(1).X = NumericUpDown21.Value
        Channels(1).Y = NumericUpDown22.Value
        Channels(1).Width = NumericUpDown23.Value
        Channels(1).Height = NumericUpDown24.Value
        Channels(1).Enabled = CheckBox2.Checked
        Channels(2).X = NumericUpDown31.Value
        Channels(2).Y = NumericUpDown32.Value
        Channels(2).Width = NumericUpDown33.Value
        Channels(2).Height = NumericUpDown34.Value
        Channels(2).Enabled = CheckBox3.Checked
        ComboBox1.SelectedIndex = 0
    End Sub

    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        If CustomRunningTime = 0 Then CustomRunningTime = maxprog
        If progress = -1 Then
            Me.Text = "Running FFmpeg"
            Exit Sub
        End If
        Dim div = CustomRunningTime / 100
        Me.Text = $"Progress: {Math.Round(progress / div, 2)}% (real {progress / SampleRate}s) - {teringwatch.Elapsed.ToString("hh\:mm\:ss")}"
    End Sub

    Private Sub ComboBox1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBox1.SelectedIndexChanged
        PropertyGrid1.SelectedObject = Channels(ComboBox1.SelectedIndex)
    End Sub
    Public Sub UpdateChannels()

        ' Trim if there are too many channels
        If Channels.Count > ChannelCount Then
            Channels.RemoveRange(ChannelCount, Channels.Count - ChannelCount)

            ' Add only the missing channels
        ElseIf Channels.Count < ChannelCount Then
            For i = Channels.Count To ChannelCount - 1
                Channels.Add(New OscilloscopeChannel())
            Next
        End If

        ComboBox1.Items.Clear()
        For i = 1 To Channels.Count
            ComboBox1.Items.Add($"Channel {i}")
        Next
        ComboBox1.SelectedIndex = 0
        NumericUpDown2.Value = ChannelCount
        PropertyGrid1.Refresh()
    End Sub
    Private Sub NumericUpDown2_ValueChanged(sender As Object, e As EventArgs) Handles NumericUpDown2.ValueChanged
        ChannelCount = NumericUpDown2.Value
        UpdateChannels()
    End Sub

    Private Sub PictureBox1_Click(sender As Object, e As EventArgs) Handles PictureBox1.Click
        If ForegroundColorDlg.ShowDialog() = DialogResult.OK Then
            PictureBox1.BackColor = ForegroundColorDlg.Color
        End If
    End Sub

    Private Sub PictureBox2_Click(sender As Object, e As EventArgs) Handles PictureBox2.Click
        If BackgroundColorDlg.ShowDialog() = DialogResult.OK Then
            PictureBox2.BackColor = BackgroundColorDlg.Color
        End If
    End Sub

    Private Sub Button8_Click(sender As Object, e As EventArgs) Handles Button8.Click
        SaveSets()
        Dim bufBMP As New Bitmap(1280, 720)
        Using g As Drawing.Graphics = Drawing.Graphics.FromImage(bufBMP)
            For i = 0 To Channels.Count - 1
                If Channels(i).Enabled Then
                    Channels(i).AudioData = LoadWavSamples(Channels(i).AudioFile)
                    If Channels(i).Multiplier <> 1 Then
                        For o = 0 To Channels(i).AudioData.samples.Length - 1
                            Channels(i).AudioData.samples(o) *= Channels(i).Multiplier
                        Next
                    End If
                    DrawChannel(g, Channels(i), Channels(i).AudioData.samples.Skip(SampleRate).Take(SampleRate \ 10).ToArray, New Pen(LineClr))
                    Channels(i).AudioData = Nothing
                End If
            Next
        End Using
        PreviewWindow.Show()
        PreviewWindow.PictureBox1.Image = bufBMP
        AddHandler PreviewWindow.FormClosed, Sub()
                                                 bufBMP.Dispose()
                                             End Sub
    End Sub
End Class
Public Class OscilloscopeChannel
    <Description("Selects the X coordinate of the visible area.")>
    Public Property X As Int32 = 0
    <Description("Selects the Y coordinate of the visible area.")>
    Public Property Y As Int32 = 0
    <Description("Selects the width of the visible area.")>
    Public Property Width As Int32 = 1280
    <Description("Selects the height of the visible area.")>
    Public Property Height As Int32
    '<Category("Paths"),
    <Description("Select the input audio file."),
     Editor(GetType(AudioFileEditor), GetType(UITypeEditor))>
    Public Property AudioFile As String
    Public AudioData As (samples As Single(), sampleRate As Integer)
    Public TriggerVal
    <Description("Selects the amplification factor of the audio.")>
    Public Property Multiplier As Single = 1
    <Description("Selects if the channel should be rendered.")>
    Public Property Enabled As Boolean = True

End Class
Public Class AudioFileEditor
    Inherits UITypeEditor
    Public Overrides Function GetEditStyle(context As ITypeDescriptorContext) As UITypeEditorEditStyle
        Return UITypeEditorEditStyle.Modal
    End Function
    Public Overrides Function EditValue(context As ITypeDescriptorContext, provider As IServiceProvider, value As Object) As Object
        Dim edSvc = TryCast(provider.GetService(GetType(IWindowsFormsEditorService)), IWindowsFormsEditorService)
        If edSvc IsNot Nothing Then
            Using ofd As New OpenFileDialog()
                ofd.Filter = "Audio files|*.wav;*.mp3;.aiff|All files|*"
                ofd.Title = "Select an Audio File"
                If ofd.ShowDialog() = DialogResult.OK Then
                    Return ofd.FileName
                End If
            End Using
        End If
        Return value
    End Function
End Class
