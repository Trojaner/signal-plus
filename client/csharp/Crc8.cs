namespace ArduinoArgb;

public static class Crc8
{
    private static readonly byte[] Table = new byte[256];
    // x8 + x7 + x6 + x4 + x2 + 1
    const byte Poly = 0xD5;

    public static byte ComputeChecksum(byte[] bytes, int count)
    {
        byte crc = 0;
        if (count == 0)
        {
            return 0;
        }

        for (var i = 0; i < count; i++)
        {
            var b = bytes[i];
            crc = Table[crc ^ b];
        }

        return crc;
    }

    static Crc8()
    {
        for (var i = 0; i < 256; ++i)
        {
            var temp = i;
            for (var j = 0; j < 8; ++j)
            {
                if ((temp & 0x80) != 0)
                {
                    temp = (temp << 1) ^ Poly;
                }
                else
                {
                    temp <<= 1;
                }
            }

            Table[i] = (byte)temp;
        }
    }
}
