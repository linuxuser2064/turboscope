Public Class PreviewWindow
    Private Sub PreviewWindow_FormClosed(sender As Object, e As FormClosedEventArgs) Handles MyBase.FormClosed
        PictureBox1.Image.Dispose()
    End Sub
End Class