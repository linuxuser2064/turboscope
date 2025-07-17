Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices

Public Class FastPix

	'(c) Vic Joseph (Boops Boops) 2009. Correction for UseByteData applied in October 2013.

    'The FastPix class provides fast pixel processing of bitmaps. It encapsulates the LockBits/UnlockBits methods.
    'FastPix is designed to deal only with the default GDI+ pixel format 32bppArgb and the format 32bppPArgb,
    'but it includes a method for converting bitmaps.

    'To use FastPix, unzip it to your hard drive. 
    'To add it to your project in VisualStudio, select the Project Menu then Add Existing Item...
    'Then navigate to fastpix.vb on your drive.

    'The main uses of FastPix are as follows.
    '1. To convert a bitmap to a 32bppPArgb (32 bit premultiplied) format, use:
    '   FastPix.Convert(myBitmap, PixelFormat.Format32bppPArgb)
    '   Note: for the canonical format 32bppArgb, it may be more convenient to do something like this:
    '   Dim myBitmap as New Bitmap("D:\Pictures\ExistingImage.jpg")

    '1. A fast substitute for Bitmap.GetPixel and Bitmap.SetPixel. Example usage:

    '   Using fp as New FastPix(myBitmap)
    '       Dim myColor As Color =  fp.GetPixel(x, y)
    '       fp.SetPixel(x, y, Color.Orange)
    '   End Using

    '2. The class provides a PixelArray property, which is a representation of the bitmap as an array of integers.
    '   (Actual performance will naturally also depend on other aspects of your processing loop.) Example usage:

    '   Using fp as New FastPix(myBitmap)
    '       'Make a local reference to the array; it is roughly 4x as fast as direct references to fp.PixelArray:
    '       Dim pixels as Integer() = fp.PixelArray

    '           For i as integer = 0 to pixels.Length - 1

    '               'example: substitute a color
    '               if pixels(i) = Color.Red.ToArgb then pixels(i) = Color.Blue.ToArgb

    '               'example: invert the color
    '               pixels(i) = pixels(i) AND $HFFFFFF

    '           Next
    '
    '   End Using

    '3. The class also provides an optional Byte array property, which represents the bitmap as a 1D array of bytes.
	'It may be useful when individual color bytes have to be modified. 
	'NOTE: the bytes are in BRGA order, so the first Alpha byte is at index = 3. Example usage:

    '    Using fp As New FastPix(myBitmap, True)
    '       Dim bytes As byte() = fp.ColorByteArray

    '       'Modify the Alpha bytes to make the bitmap 50% transparent:
	'       For i As Integer = 3 to bytes.Length - 1 Step 4    
    '           bytes(i) = 127
    '       Next

    '   End Using

    Implements IDisposable

    Private _bmp As Bitmap
    Private _w, _h As Integer
    Private _disposed As Boolean = False
    Private _bmpData As BitmapData
    Private _PixelData As Integer()
	Private _ByteData As Byte()
	Private _UseByteArray As Boolean = False

    'An array of integers representing the pixel data of a bitmap.
    Public Property PixelArray() As Integer()
        Get
            Return _PixelData
        End Get
        Set(ByVal value As Integer())
            _PixelData = value
        End Set
    End Property

    'An array of bytes representing the pixel data of a bitmap.
    Public Property ColorByteArray() As Byte()
        Get
            Return _ByteData
        End Get
        Set(ByVal value As Byte())
            _ByteData = value
        End Set
    End Property

#Region "Constructors/Destructors"

    'The New Sub contains the LockBits+Marshall.Copy code.
	Public Sub New(ByRef bmp As Bitmap, Optional ByVal UseByteArray As Boolean = False)
		_UseByteArray = UseByteArray

		Dim pFSize As Integer = Bitmap.GetPixelFormatSize(bmp.PixelFormat)

		If pFSize <> 32 OrElse bmp.PixelFormat = PixelFormat.Indexed Then
			Throw New FormatException _
				("FastPix is designed to deal only with 32-bit pixel non-indexed formats. Your bitmap has " _
				& pFSize & "-bit pixels. You can convert it using FastPix.ConvertFormat.")
		Else
			'Convert the bitap to a 1 dimensional array of pixel data:
			_w = bmp.Width
			_h = bmp.Height
			_bmp = bmp
			Dim bmpRect As New Rectangle(0, 0, _w, _h)

			_bmpData = _bmp.LockBits(bmpRect, ImageLockMode.ReadWrite, _bmp.PixelFormat)

			If UseByteArray Then
				ReDim _ByteData(_w * _h * 4 - 1)
				Marshal.Copy(_bmpData.Scan0, _ByteData, 0, _ByteData.Length)
			Else
				ReDim _PixelData(_w * _h - 1)
				Marshal.Copy(_bmpData.Scan0, _PixelData, 0, _PixelData.Length)
			End If
		End If

	End Sub

    Public Overloads Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    'The Dispose Sub contains the Marshal.Copy+UnlockBits code.
    Protected Overloads Sub Dispose(ByVal disposing As Boolean)
		If Not _disposed Then
			If _UseByteArray Then
				If _ByteData IsNot Nothing Then Marshal.Copy(_ByteData, 0, _bmpData.Scan0, _ByteData.Length)
			Else
				If _PixelData IsNot Nothing Then Marshal.Copy(_PixelData, 0, _bmpData.Scan0, _PixelData.Length)
			End If
			_bmp.UnlockBits(_bmpData)
			_ByteData = Nothing
			_PixelData = Nothing
			_bmpData = Nothing
			_disposed = True
		End If
    End Sub

#End Region

#Region "Public Methods"

    'Return the color of any pixel in the bitmap.
    Public Function GetPixel(ByVal x As Integer, ByVal y As Integer) As Color
        Return Color.FromArgb(_PixelData(y * _w + x))
    End Function

    'Set the color of any pixel in the bitmap.
    Public Sub SetPixel(ByVal x As Integer, ByVal y As Integer, ByVal clr As Color)
        _PixelData(y * _w + x) = clr.ToArgb
    End Sub

    'Standardize the bitmap format for use with FastPix.
    Public Shared Sub ConvertFormat(ByRef bmp As Bitmap, _
                            Optional ByVal TargetFormat As PixelFormat = PixelFormat.Format32bppArgb)
        Try
            Dim bmp2 As New Bitmap(bmp.Width, bmp.Height, TargetFormat)
            Using g As Graphics = Graphics.FromImage(bmp2)
                bmp.SetResolution(96, 96)
                g.DrawImageUnscaled(bmp, Point.Empty)
            End Using
            bmp = bmp2
        Catch
            Throw New FormatException("FastPix could not convert the bitmap to the standard format.")
        End Try
    End Sub


#End Region

End Class