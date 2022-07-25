using System.Runtime.InteropServices;

namespace SignalPlus.UsbEmulation.UsbIp;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct IoctlUsbIpVhciUnplug
{
    public sbyte Port;
    private fixed byte _padding[3];
}