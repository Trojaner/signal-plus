using System.Runtime.InteropServices;

namespace ArduinoArgb;

[StructLayout(LayoutKind.Explicit, Size = 1 + Color24.Size)]
public struct ColorStatus
{
    [FieldOffset(0x00)]
    public Color24 Color;
    
    [FieldOffset(0x03)]
    public bool Changed;
}