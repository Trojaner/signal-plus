using System.Runtime.InteropServices;

namespace SignalPlus.UsbEmulation.UsbIp.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct USB_STANDARD_DEVICE_REQUEST
{
    public byte bmRequestType;
    public byte bRequest;
    public byte wValue0;
    public byte wValue1;
    public byte wIndex0;
    public byte wIndex1;
    public short wLength;
}