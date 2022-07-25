using System;
using System.Runtime.InteropServices;

namespace SignalPlus.Arduino;

[StructLayout(LayoutKind.Explicit, Size = Size)]
public readonly struct Color24Image
{
    public const int PixelCount = MatrixPanel.Width * MatrixPanel.Height;
    public const int Size = PixelCount * Color24.Size;

    [FieldOffset(0x00)]
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = PixelCount)]
    private readonly Color24[] _pixels;

    public Color24Image()
    {
        _pixels = new Color24[PixelCount];
    }

    public Color24 this[byte x, byte y]
    {
        get => GetPixel(x, y);
        set => SetPixel(x, y, value);
    }

    private Color24 GetPixel(byte x, byte y)
    {
        return _pixels[GetIndex(x, y)];
    }

    private void SetPixel(byte x, byte y, Color24 color)
    {
        _pixels[GetIndex(x, y)] = color;
    }

    private int GetIndex(byte x, byte y)
    {
        return y * MatrixPanel.Width + x;
    }

    public byte[] ToByteArray()
    {
        var bytes = new byte[Size];
        unsafe
        {
            fixed (Color24* p = _pixels)
            {
                Marshal.Copy((IntPtr)p, bytes, 0, Size);
            }
        }
        return bytes;
    }

    public (Color24, bool isDifferent)[,] Compare(Color24Image reference)
    {
        var result = new (Color24, bool isDifferent)[MatrixPanel.Width,MatrixPanel.Height];
            
        for (byte x = 0; x < MatrixPanel.Width; x++)
        {
            for (byte y = 0; y < MatrixPanel.Height; y++)
            {
                var idx = GetIndex(x, y);
                var src = _pixels[idx];
                var cmp = reference._pixels[idx];
                var check = src.R == cmp.R && src.G == cmp.G && src.B == cmp.B;

                result[x, y] = (reference._pixels[GetIndex(x, y)], check);
            }
        }

        return result;
    }
}