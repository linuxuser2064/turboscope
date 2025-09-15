Imports System.ComponentModel
Imports System.Drawing.Design
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Runtime.ConstrainedExecution
Imports System.Runtime.InteropServices
Imports System.Windows.Forms.Design
Imports System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar
Imports System.Xml.Serialization
Imports FFMediaToolkit
Imports FFMediaToolkit.Encoding
Imports NAudio.Wave
Imports NAudio.Wave.SampleProviders
Public Class Form1
    Public Channels As New List(Of OscilloscopeChannel)
    Dim ChannelCount As Int32 = 3

    Dim CRTScope As Boolean = True
    Dim LineWidth As Int32 = 5
    Dim LineClr As Color
    Dim BackClr As Color = Color.Black

    Dim DoAntiAliasing As Boolean = False

    Dim CustomRunningTimeEnabled As Boolean = False
    Dim CustomRunningTime As Long = 0
    Dim VideoOutputPath As String
    Dim MasterAudio As String
    Dim SampleRate As Int32 = 44100 ' stub

    Dim progress As Long = 0
    Dim maxprog As Long = 0
    Dim DoResample As Boolean = False
    Dim ResampleTo As Int32 = 0
    Dim VideoBitrate = 10000000
    Dim VideoFramerate = 50
    Dim VideoSize As New Size(1280, 720) ' stub
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
    Public Function LoadWavSamples(filePath As String, resampleRate As Integer) As (samples As Single(), sampleRate As Integer)
        Dim fakereader As MediaFoundationResampler = New MediaFoundationResampler(New AudioFileReader(filePath).ToMono.ToWaveProvider16, resampleRate)
        fakereader.ResamplerQuality = 1
        Dim reader As ISampleProvider = fakereader.ToSampleProvider
        Dim SampleRate = reader.WaveFormat.SampleRate
        Dim sampleList As New List(Of Single)
        Dim buffer(1023) As Single
        Dim read As Integer
        Do
            read = reader.Read(buffer, 0, buffer.Length)
            If read > 0 Then
                sampleList.AddRange(buffer.Take(read))
            End If
        Loop While read > 0
        Return (sampleList.ToArray(), SampleRate)
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
        progress = -3
        For i = 0 To Channels.Count - 1
            If Channels(i).Enabled Then
                Channels(i).AudioData = If(DoResample, LoadWavSamples(Channels(i).AudioFile, ResampleTo), LoadWavSamples(Channels(i).AudioFile))
                If Channels(i).Multiplier <> 1 Then
                    For o = 0 To Channels(i).AudioData.samples.Length - 1
                        Channels(i).AudioData.samples(o) *= Channels(i).Multiplier
                    Next
                End If
            End If
        Next

        Dim length = Channels(0).AudioData.samples.Length
        SampleRate = Channels(0).AudioData.sampleRate
        If CustomRunningTimeEnabled Then
            CustomRunningTime = NumericUpDown1.Value * SampleRate
        End If

        If DoAntiAliasing Then
            Dim bmp As New Bitmap(VideoSize.Width, VideoSize.Height)
            Dim g As Drawing.Graphics
            g = Drawing.Graphics.FromImage(bmp)
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            g.Clear(BackClr)
            Dim audioCounter As Long = 0
            Dim samplesPerFrame As Integer = SampleRate \ VideoFramerate
            Dim drawSampleCount As Integer = SampleRate \ 15
            Dim vid = MediaBuilder.CreateContainer($"{VideoOutputPath}_raw.mp4", ContainerFormat.MP4).
        WithVideo(New VideoEncoderSettings(VideoSize.Width, VideoSize.Height, VideoFramerate, VideoCodec.H264) With {.Bitrate = VideoBitrate, .EncoderPreset = EncoderPreset.Fast}).
        Create
            maxprog = length
            While audioCounter < length
                If CustomRunningTimeEnabled AndAlso audioCounter >= CustomRunningTime Then Exit While
                If audioCounter + samplesPerFrame >= length Then Exit While
                g.Clear(BackClr)
                For Each ch In Channels
                    If ch.Enabled Then
                        Dim clr As New Pen(ch.LineColor)
                        Dim smp = ch.AudioData.samples
                        If audioCounter + drawSampleCount < smp.Length Then
                            Dim smpData = smp.AsSpan().Slice(CInt(audioCounter), drawSampleCount).ToArray()
                            DrawChannelGDI(g, ch, smpData, clr, bmp)
                        End If
                    End If
                Next
                BitmapToImageData.BMPtoBitmapData.AddBitmapFrame(vid, bmp)

                audioCounter += samplesPerFrame
                progress = audioCounter
            End While
            vid.Dispose()

        Else


            Dim bmp As New Bitmap(VideoSize.Width, VideoSize.Height)
            Dim data As BitmapData = bmp.LockBits(New Rectangle(0, 0, bmp.Width, bmp.Height),
                                     Imaging.ImageLockMode.ReadWrite,
                                     Imaging.PixelFormat.Format32bppArgb)
            Dim blankFrame(data.Stride * data.Height) As Byte
            For i = 0 To blankFrame.Length - 1 Step 4
                blankFrame(Clamp(i, 0, blankFrame.Length - 1)) = BackClr.A
                blankFrame(Clamp(i + 1, 0, blankFrame.Length - 1)) = BackClr.R
                blankFrame(Clamp(i + 2, 0, blankFrame.Length - 1)) = BackClr.G
                blankFrame(Clamp(i + 3, 0, blankFrame.Length - 1)) = BackClr.B
            Next
            Marshal.Copy(blankFrame, 0, data.Scan0, blankFrame.Length - 1)


            Dim audioCounter As Long = 0
            Dim samplesPerFrame As Integer = SampleRate \ VideoFramerate
            Dim drawSampleCount As Integer = SampleRate \ 15
            Dim vid = MediaBuilder.CreateContainer($"{VideoOutputPath}_raw.mp4", ContainerFormat.MP4).
            WithVideo(New VideoEncoderSettings(VideoSize.Width, VideoSize.Height, VideoFramerate, VideoCodec.H264) With {.Bitrate = VideoBitrate, .EncoderPreset = EncoderPreset.Fast}).
            Create
            maxprog = length
            While audioCounter < length
                If CustomRunningTimeEnabled AndAlso audioCounter >= CustomRunningTime Then Exit While
                If audioCounter + samplesPerFrame >= length Then Exit While
                Marshal.Copy(blankFrame, 0, data.Scan0, blankFrame.Length - 1)
                For Each ch In Channels
                    If ch.Enabled Then
                        Dim smp = ch.AudioData.samples
                        If audioCounter + drawSampleCount < smp.Length Then
                            Dim smpData = smp.AsSpan().Slice(CInt(audioCounter), drawSampleCount).ToArray()
                            DrawChannelFast(ch, smpData, ColorToArgbFixed(ch.LineColor), data)
                        End If
                    End If
                Next
                BitmapToImageData.BMPtoBitmapData.AddBitmapFrame(vid, data)

                audioCounter += samplesPerFrame
                progress = audioCounter
            End While

            vid.Dispose()
        End If


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
    Function Clamp(val As Int32, min As Int32, max As Int32) As Int32
        If val < min Then
            Return min
        ElseIf val > max Then
            Return max
        Else
            Return val
        End If
    End Function
    Function ColorToArgbFixed(clr As Color) As Integer
        Dim bytes() = {clr.A, clr.R, clr.G, clr.B}
        Return BitConverter.ToInt32(bytes, 0)
    End Function
    Sub DrawChannelFast(ByRef channel As OscilloscopeChannel, songData As Single(), ByVal lineARGB As Integer, ByRef data As BitmapData)
        Dim width = channel.Width
        Dim height = channel.Height
        Dim chanX = channel.X
        Dim chanY = channel.Y

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

        ' Lock the bitmap for writing
        Dim pixels(data.Width * data.Height - 1) As Integer
        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length)
        Dim prevVal = songData(startIndex)
        For i = startIndex To startIndex + width - 1
            Dim val = songData(i)
            Dim currentY = (height / 2) - (val * (height / 2)) + chanY ' calculate y of current value
            Dim roundedY As Int32 = Math.Floor(currentY)
            Dim prevY = (height / 2) - (prevVal * (height / 2)) + chanY ' calculate y of previous song value
            Dim roundedPrevY As Int32 = Math.Floor(prevY)
            Dim x = i - startIndex + chanX
            Dim y1 = CInt(Math.Floor(currentY))
            Dim y2 = CInt(Math.Floor(prevY))

            If y1 > y2 Then
                Dim tmp = y1
                y1 = y2
                y2 = tmp
            End If


            Dim verticalHalf As Integer = LineWidth \ 2

            For y = (y1 - verticalHalf) To (y2 + verticalHalf)
                ' Clamp y so we don't go outside the bitmap
                Dim clampedY = Clamp(y, 0, data.Height - 1)

                ' horizontal thickness
                Dim horizontalHalf As Integer = If(CRTScope, 0, LineWidth \ 2)

                For i2 = -horizontalHalf To horizontalHalf
                    Dim px = Clamp(x + i2, 0, data.Width - 1)
                    Dim index = (clampedY * data.Stride \ 4) + px
                    pixels(index) = lineARGB
                Next
            Next
            prevVal = val
        Next
        Marshal.Copy(pixels, 0, data.Scan0, pixels.Length)

    End Sub
    Sub DrawChannelGDI(ByRef graphic As Drawing.Graphics, ByRef channel As OscilloscopeChannel, songData As Single(), ByVal lineColor As Pen, bmp As Bitmap)
        Dim width = channel.Width
        Dim height = channel.Height
        Dim chanX = channel.X
        Dim chanY = channel.Y

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
        Dim newClr As New Pen(lineColor.Color, LineWidth)
        newClr.Width = LineWidth
        newClr.StartCap = Drawing2D.LineCap.Round
        newClr.EndCap = Drawing2D.LineCap.Round
        newClr.LineJoin = Drawing2D.LineJoin.Round
        Dim prevVal = songData(startIndex)
        For i = startIndex To startIndex + width - 1
            Dim val = songData(i)
            Dim currentY = (height / 2) - (val * (height / 2)) + chanY ' calculate y of current value
            Dim prevY = (height / 2) - (prevVal * (height / 2)) + chanY ' calculate y of previous song value
            Dim x = i - startIndex + chanX
            If CRTScope Then
                For i2 = -(LineWidth \ 2) To (LineWidth \ 2)
                    '                                x1                     y1                          x2                          y2
                    graphic.DrawLine(lineColor, New PointF(i - startIndex + chanX, currentY + i2), New PointF(i - startIndex + chanX - 1, prevY + 1 + i2))
                Next
            Else
                graphic.DrawLine(newClr, New PointF(i - startIndex + chanX, currentY), New PointF(i - startIndex + chanX - 1, prevY + 1))
            End If
            prevVal = val
        Next
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
        VideoOutputPath = TextBox5.Text
        CRTScope = CheckBox4.Checked
        LineWidth = NumericUpDown3.Value
        BackClr = PictureBox2.BackColor
        DoResample = CheckBox7.Checked
        ResampleTo = NumericUpDown4.Value
        VideoSize = New Size(CInt(MaskedTextBox1.Text), CInt(MaskedTextBox2.Text))
        VideoBitrate = CInt(MaskedTextBox3.Text) * 1000
        VideoFramerate = NumericUpDown5.Value
    End Sub
    Function ScaleImage(orig As Bitmap, w As Int32, h As Int32) As Bitmap
        Dim newbmp As New Bitmap(w, h)
        Using g As Drawing.Graphics = Drawing.Graphics.FromImage(newbmp)
            g.DrawImage(orig, 0, 0, w, h)
        End Using
        Return newbmp
    End Function
    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        MasterAudioOpenFileDialog.ShowDialog()
    End Sub

    Private Sub MasterAudioOpenFileDialog1_FileOk(sender As Object, e As System.ComponentModel.CancelEventArgs) Handles MasterAudioOpenFileDialog.FileOk
        TextBox4.Text = MasterAudioOpenFileDialog.FileName
    End Sub

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click
        Mp4SaveDialog.ShowDialog()
    End Sub

    Private Sub SaveFileDialog1_FileOk(sender As Object, e As System.ComponentModel.CancelEventArgs) Handles Mp4SaveDialog.FileOk
        TextBox5.Text = Mp4SaveDialog.FileName
    End Sub

    Private Sub Button7_Click(sender As Object, e As EventArgs) Handles Button7.Click
        ChannelCount = 4
        UpdateChannels()
        Channels(0).X = 0
        Channels(0).Y = 0
        Channels(0).Width = VideoSize.Width
        Channels(0).Height = VideoSize.Height \ 4
        Channels(0).Enabled = True
        Channels(1).X = 0
        Channels(1).Y = VideoSize.Height \ 4
        Channels(1).Width = VideoSize.Width
        Channels(1).Height = VideoSize.Height \ 4
        Channels(1).Enabled = True
        Channels(2).X = 0
        Channels(2).Y = (VideoSize.Height \ 4) * 2
        Channels(2).Width = VideoSize.Width
        Channels(2).Height = VideoSize.Height \ 4
        Channels(2).Enabled = True
        Channels(3).X = 0
        Channels(3).Y = (VideoSize.Height \ 4) * 3
        Channels(3).Width = VideoSize.Width
        Channels(3).Height = VideoSize.Height \ 4
        Channels(3).Enabled = True
        PropertyGrid1.Refresh()
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        ChannelCount = 3
        UpdateChannels()
        Channels(0).X = 0
        Channels(0).Y = 0
        Channels(0).Width = VideoSize.Width
        Channels(0).Height = VideoSize.Height \ 3
        Channels(0).Enabled = True
        Channels(1).X = 0
        Channels(1).Y = VideoSize.Height \ 3
        Channels(1).Width = VideoSize.Width
        Channels(1).Height = VideoSize.Height \ 3
        Channels(1).Enabled = True
        Channels(2).X = 0
        Channels(2).Y = (VideoSize.Height \ 3) * 2
        Channels(2).Width = VideoSize.Width
        Channels(2).Height = VideoSize.Height \ 3
        Channels(2).Enabled = True
        PropertyGrid1.Refresh()
    End Sub

    Private Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click
        ChannelCount = 2
        UpdateChannels()
        Channels(0).X = 0
        Channels(0).Y = 0
        Channels(0).Width = VideoSize.Width
        Channels(0).Height = VideoSize.Height \ 2
        Channels(0).Enabled = True
        Channels(1).X = 0
        Channels(1).Y = VideoSize.Height \ 2
        Channels(1).Width = VideoSize.Width
        Channels(1).Height = VideoSize.Width \ 2
        Channels(1).Enabled = True
        PropertyGrid1.Refresh()
    End Sub

    Private Sub Button6_Click(sender As Object, e As EventArgs) Handles Button6.Click
        ChannelCount = 1
        UpdateChannels()
        Channels(0).X = 0
        Channels(0).Y = 0
        Channels(0).Width = VideoSize.Width
        Channels(0).Height = VideoSize.Height
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
        ComboBox1.SelectedIndex = 0
        Button2.PerformClick()
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
            Exit Sub
        End If
        If progress = -3 Then
            Me.Text = "Loading samples..."
            Exit Sub
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

    Private Sub PictureBox2_Click(sender As Object, e As EventArgs) Handles PictureBox2.Click
        If BackgroundColorDlg.ShowDialog() = DialogResult.OK Then
            PictureBox2.BackColor = BackgroundColorDlg.Color
        End If
    End Sub

    Private Sub Button8_Click(sender As Object, e As EventArgs) Handles Button8.Click
        SaveSets()
        Dim bufBMP As New Bitmap(VideoSize.Width, VideoSize.Height)
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
                    DrawChannelGDI(g, Channels(i), Channels(i).AudioData.samples.Skip(SampleRate).Take(SampleRate \ 10).ToArray, clr, bufBMP)
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

    Private Sub Button9_Click(sender As Object, e As EventArgs) Handles Button9.Click
        If OscilloscopeSettingsSaveDialog.ShowDialog = DialogResult.OK Then
            Dim sets As New OscilloscopeSettings
            sets.Channels = Me.Channels
            sets.ChannelColor = New List(Of (r As Integer, g As Integer, b As Integer))
            For i = 0 To Channels.Count - 1
                sets.ChannelColor.Add((Channels(i).LineColor.R, Channels(i).LineColor.G, Channels(i).LineColor.B))
            Next
            Dim turd As New XmlSerializer(sets.GetType)
            Dim stream As New FileStream(OscilloscopeSettingsSaveDialog.FileName, FileMode.Create, FileAccess.Write)
            turd.Serialize(stream, sets)
            stream.Dispose()
        End If
    End Sub

    Private Sub Button10_Click(sender As Object, e As EventArgs) Handles Button10.Click
        If OscilloscopeSettingsOpenDialog.ShowDialog = DialogResult.OK Then
            Dim sets As New OscilloscopeSettings
            Dim turd As New XmlSerializer(sets.GetType)
            Dim stream As New FileStream(OscilloscopeSettingsOpenDialog.FileName, FileMode.Open, FileAccess.ReadWrite)
            sets = turd.Deserialize(stream)
            Me.Channels = sets.Channels
            For i = 0 To Channels.Count - 1
                Channels(i).LineColor = Color.FromArgb(sets.ChannelColor(i).r, sets.ChannelColor(i).g, sets.ChannelColor(i).b)
            Next
            ChannelCount = sets.Channels.Count
            UpdateChannels()
            PropertyGrid1.Refresh()
            stream.Dispose()
        End If
    End Sub

    Private Sub MaskedTextBox1_TextChanged(sender As Object, e As EventArgs) Handles MaskedTextBox2.TextChanged, MaskedTextBox1.TextChanged
        If (MaskedTextBox1.Text = "") = False AndAlso (MaskedTextBox2.Text = "") = False Then
            VideoSize = New Size(CInt(MaskedTextBox1.Text), CInt(MaskedTextBox2.Text))
        End If
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
Public Class OscilloscopeSettings
    Public Channels As List(Of OscilloscopeChannel)
    Public ChannelColor As List(Of (r As Int32, g As Int32, b As Int32))
End Class