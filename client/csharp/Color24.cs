using System.Runtime.InteropServices;

namespace ArduinoArgb;

[StructLayout(LayoutKind.Explicit, Size = Size)]
public struct Color24
{
    public const int Size = 0x3;
    public static Color24 Black = new(0, 0, 0);
    
    public Color24(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
    }

    public static Color24 FromHsl(double h, double sl, double l)
    {
        var r = l;
        var g = l;
        var b = l;
        var v = (l <= 0.5) ? (l * (1.0 + sl)) : (l + sl - l * sl);

        if (v > 0)
        {
            var m = l + l - v;
            var sv = (v - m) / v;

            h *= 6.0;

            var sextant = (int)h;
            var fract = h - sextant;
            var vsf = v * sv * fract;
            var mid1 = m + vsf;
            var mid2 = v - vsf;

            switch (sextant)
            {
                case 0:
                    r = v;
                    g = mid1;
                    b = m;
                    break;

                case 1:
                    r = mid2;
                    g = v;
                    b = m;
                    break;

                case 2:
                    r = m;
                    g = v;
                    b = mid1;
                    break;

                case 3:
                    r = m;
                    g = mid2;
                    b = v;
                    break;

                case 4:
                    r = mid1;
                    g = m;
                    b = v;
                    break;

                case 5:
                    r = v;
                    g = m;
                    b = mid2;
                    break;

            }

        }

        return new Color24
        {
            R = Convert.ToByte(r * 255.0f),
            G = Convert.ToByte(g * 255.0f),
            B = Convert.ToByte(b * 255.0f)
        };
    }

    public byte this[int index]
    {
        get
        {
            return index switch
            {
                0 => R,
                1 => G,
                2 => B,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }
    }

    [FieldOffset(0x00)] public byte R;
    [FieldOffset(0x01)] public byte G;
    [FieldOffset(0x02)] public byte B;

    public byte[] ToByteArray()
    {
        return new[] { R, G, B };
    }

    public override string ToString()
    {
        return $"{R},{G},{B}";
    }
}