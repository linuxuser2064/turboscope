Imports System.ComponentModel
Imports System.Drawing.Design
Imports System.IO
Imports System.Reflection
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
    Dim realVersion As Boolean = False
    Dim walls As Bitmap

    Dim DoAntiAliasing As Boolean = False

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
        Button1.Enabled = False
        Button8.Enabled = False
        SaveSets()
        Timer1.Start()
        VideoWorker.RunWorkerAsync()
    End Sub
    Dim timewatch As New Stopwatch
    Private Sub VideoWorker_DoWork(sender As Object, e As DoWorkEventArgs) Handles VideoWorker.DoWork
        timewatch.Start()
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
        SampleRate = Channels(0).AudioData.sampleRate
        Dim bmp As New Bitmap(VideoSize.Width, VideoSize.Height)
        Using g As Drawing.Graphics = Drawing.Graphics.FromImage(bmp)
            If DoAntiAliasing Then
                g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            Else
                g.SmoothingMode = Drawing2D.SmoothingMode.None
            End If
            Dim audioCounter As Long = 0
            Dim samplesPerFrame As Integer = SampleRate \ 50
            Dim drawSampleCount As Integer = SampleRate \ 15

            Dim vid = MediaBuilder.CreateContainer($"{VideoOutputPath}_raw.mp4", ContainerFormat.MP4).
            WithVideo(New VideoEncoderSettings(VideoSize.Width, VideoSize.Height, 50, VideoCodec.Default)).
            Create

            While audioCounter < length
                If CustomRunningTimeEnabled AndAlso audioCounter >= CustomRunningTime Then Exit While
                If audioCounter + samplesPerFrame >= length Then Exit While
                If realVersion Then
                    g.DrawImageUnscaled(walls, 0, 0)
                Else
                    g.Clear(BackClr)
                End If

                For Each ch In Channels
                    If ch.Enabled Then
                        Dim clr As New Pen(ch.LineColor)
                        Dim smp = ch.AudioData.samples
                        If audioCounter + drawSampleCount < smp.Length Then
                            Dim smpData = smp.AsSpan().Slice(CInt(audioCounter), drawSampleCount).ToArray()
                            DrawChannel(g, ch, smpData, clr, bmp)
                        End If
                    End If
                Next

                BitmapToImageData.BMPtoBitmapData.AddBitmapFrame(vid, bmp)

                audioCounter += samplesPerFrame
                progress = audioCounter
            End While

            vid.Dispose()
        End Using

        Dim ffmpegArgs = $"-i ""{VideoOutputPath}_raw.mp4"" -i ""{MasterAudio}"" -shortest -c:v copy -c:a aac -b:a 320k -map 0:v:0 -map 1:a:0 ""{Application.StartupPath}\out.mp4"""
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
        timewatch.Stop()
        timewatch.Reset()
        progress = -2
    End Sub
    Function Clamp(val, min, max) As Int32
        Return Math.Max(min, Math.Min(val, max))
    End Function
    Sub DrawChannel(ByRef g As Drawing.Graphics, ByRef channel As OscilloscopeChannel, songData As Single(), ByVal lineColor As Pen, bmp As Bitmap)
        Dim width = channel.Width
        Dim height = channel.Height
        Dim chanX = channel.X
        Dim chanY = channel.Y

        ' --- ZERO-CROSSING TRIGGER ---


        ' --- PEAK SPEED TRIGGER ---
        'If UseOldTrigger Then
        '    Dim maxTrig As Single = songData.Skip(width \ 2).Take(songData.Length - width).Max()
        '    Dim minTrig As Single = songData.Skip(width \ 2).Take(songData.Length - width).Min()
        '    Dim triggerLevel As Single = (maxTrig + minTrig) / 2
        '    Dim hysteresis As Single = 0.01F
        '    ' try 1
        '    startIndex = Enumerable.Range(width \ 2, songData.Length - width - 1).FirstOrDefault(Function(i) songData(i) < triggerLevel AndAlso songData(i + 1) >= triggerLevel + hysteresis)
        '    If startIndex = 0 Then
        '        triggerLevel = minTrig + 0.01F
        '        ' try 2
        '        startIndex = Enumerable.Range(width \ 2, songData.Length - width - 1).FirstOrDefault(Function(i) songData(i) < triggerLevel AndAlso songData(i + 1) >= triggerLevel + hysteresis)
        '    End If
        '    If startIndex = 0 Then
        '        triggerLevel = maxTrig - 0.01F
        '        ' try 3
        '        startIndex = Enumerable.Range(width \ 2, songData.Length - width - 1).FirstOrDefault(Function(i) songData(i) < triggerLevel AndAlso songData(i + 1) >= triggerLevel + hysteresis)
        '    End If
        '    startIndex -= width \ 2
        'Else
        Dim peakValue As Single = Single.MinValue
        Dim shortestDistance As Integer = Integer.MaxValue
        Dim result As Integer = -1

        Dim maxTrig As Single = songData.Skip(width \ 2).Take(songData.Length - width).Max()
        Dim minTrig As Single = songData.Skip(width \ 2).Take(songData.Length - width).Min()

        Dim triggerLo As Single = Clamp(0, minTrig, maxTrig) - 0.01
        Dim triggerHi As Single = Clamp(0, minTrig, maxTrig) + 0.01

        Dim startIndexs As Integer = width \ 2
        Dim endIndex As Integer = songData.Length - width - 1
        Dim i As Integer = startIndexs

        While i < endIndex
            While i < endIndex AndAlso songData(i) > triggerLo
                i += 1
            End While
            While i < endIndex AndAlso songData(i) <= triggerHi
                i += 1
            End While

            Dim lastCrossing As Integer = i

            While i < endIndex
                Dim sample As Single = songData(i)
                If sample <= 0 Then Exit While

                If sample > peakValue Then
                    peakValue = sample
                    result = lastCrossing
                    shortestDistance = i - lastCrossing
                ElseIf sample = peakValue AndAlso (i - lastCrossing) < shortestDistance Then
                    result = lastCrossing
                    shortestDistance = i - lastCrossing
                End If

                i += 1
            End While
        End While

        ' Fallback if nothing found
        If result = -1 Then
            result = songData.Length \ 2
        End If

        ' Center waveform around trigger
        Dim startIndex = Math.Max(0, Math.Min(result - (width \ 2), songData.Length - width - 1))


        startIndex = Math.Max(0, Math.Min(startIndex, songData.Length - width - 1))
        'End If
        'Using fp As New FastPix(bmp)
        Dim prevVal = songData(startIndex)
        For i = startIndex To startIndex + width - 1
            Dim val = songData(i)
            Dim currentY = (height / 2) - (val * (height / 2)) + chanY ' calculate y of current value
            Dim prevY = (height / 2) - (prevVal * (height / 2)) + chanY ' calculate y of previous song value
            Dim x = i - startIndex + chanX
            If CRTScope Then
                For i2 = -(LineWidth \ 2) To (LineWidth \ 2)
                    If DoAntiAliasing Then
                        '                                x1                     y1                          x2                          y2
                        g.DrawLine(lineColor, New PointF(i - startIndex + chanX, currentY + i2), New PointF(i - startIndex + chanX - 1, prevY + 1 + i2))
                    Else
                        g.DrawLine(lineColor, i - startIndex + chanX, CInt(currentY) + i2, i - startIndex + chanX - 1, CInt(prevY) + 1 + i2)
                    End If
                Next
            Else
                Dim newClr As New Pen(lineColor.Color, LineWidth)
                If DoAntiAliasing Then
                    g.DrawLine(newClr, New PointF(i - startIndex + chanX, currentY), New PointF(i - startIndex + chanX - 1, prevY + 1))
                Else
                    g.DrawLine(newClr, i - startIndex + chanX, CInt(currentY), i - startIndex + chanX - 1, CInt(prevY) + 1)
                End If
            End If
            prevVal = val
        Next
        'End Using
    End Sub

    ' this is completely fucked
    Public Sub QuickDrawLine(pen As Pen, x1 As Int32, y1 As Int32, x2 As Int32, y2 As Int32, ByRef fp As FastPix)
        ' this will only draw vertical lines (x1 and x2 will be assumed to be the same)
        Dim clr As Color = pen.Color
        If y1 < y2 Then
            For i = y1 To y2
                fp.SetPixel(x1, i, clr)
            Next
        End If
        If y2 < y1 Then
            For i = y2 To y1
                fp.SetPixel(x1, i, clr)
            Next
        End If
        If y1 = y2 Then
            fp.SetPixel(x1, y1, clr)
        End If
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
        BackClr = PictureBox2.BackColor
    End Sub
    Function ScaleImage(orig As Bitmap, w As Int32, h As Int32) As Bitmap
        Dim newbmp As New Bitmap(w, h)
        Using g As Drawing.Graphics = Drawing.Graphics.FromImage(newbmp)
            g.DrawImage(orig, 0, 0, w, h)
        End Using
        Return newbmp
    End Function
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
        LineClr = ColorTranslator.FromHtml("#7AFF3E")
        ForegroundColorDlg.Color = ColorTranslator.FromHtml("#7AFF3E")
        Channels.Clear()

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
        PropertyGrid1.SelectedObject = Channels(0)
    End Sub

    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        If CustomRunningTime = 0 Then CustomRunningTime = maxprog
        If progress = -1 Then
            Me.Text = "Running FFmpeg"
            Exit Sub
        End If
        Dim div = CustomRunningTime / 100
        If progress = -2 Then
            Timer1.Stop()
            MsgBox($"Done!")
            Me.Text = "turboscope"
            Button1.Enabled = True
            Button8.Enabled = True
            progress = 0
        End If
        Me.Text = $"Progress: {Math.Round(progress / div, 2)}% (real {progress / SampleRate}s) - {timewatch.Elapsed.ToString("hh\:mm\:ss")}"
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

    'Private Sub PictureBox1_Click(sender As Object, e As EventArgs)
    '    If ForegroundColorDlg.ShowDialog() = DialogResult.OK Then
    '        PictureBox1.BackColor = ForegroundColorDlg.Color
    '    End If
    'End Sub

    Private Sub PictureBox2_Click(sender As Object, e As EventArgs) Handles PictureBox2.Click
        If realVersion Then
            If BackgroundColorDlg.ShowDialog() = DialogResult.OK Then
                PictureBox2.BackColor = BackgroundColorDlg.Color
            End If
        Else
            If RealVersionOpenFileDialog.ShowDialog = DialogResult.OK Then
                PictureBox2.BackgroundImage = Image.FromFile(RealVersionOpenFileDialog.FileName)
            End If
        End If
    End Sub

    Private Sub Button8_Click(sender As Object, e As EventArgs) Handles Button8.Click
        SaveSets()
        Dim bufBMP As New Bitmap(1280, 720)
        Using g As Drawing.Graphics = Drawing.Graphics.FromImage(bufBMP)
            g.Clear(BackClr)
            For i = 0 To Channels.Count - 1
                If Channels(i).Enabled Then
                    MsgBox(Channels(i).AudioFile)
                    Channels(i).AudioData = LoadWavSamples(Channels(i).AudioFile)
                    If Channels(i).Multiplier <> 1 Then
                        For o = 0 To Channels(i).AudioData.samples.Length - 1
                            Channels(i).AudioData.samples(o) *= Channels(i).Multiplier
                        Next
                    End If
                    Dim clr As New Pen(Channels(i).LineColor)
                    DrawChannel(g, Channels(i), Channels(i).AudioData.samples.Skip(SampleRate).Take(SampleRate \ 10).ToArray, clr, bufBMP)
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

    Private Sub CheckBox5_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox5.CheckedChanged
        DoAntiAliasing = CheckBox5.Checked
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
    <Description("Selects the line color for the channel.")>
    Public Property LineColor As Color = ColorTranslator.FromHtml("#7AFF3E")
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
