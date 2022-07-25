using System.Runtime.InteropServices;

namespace SignalPlus.UsbEmulation.UsbIp;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct IoctlUsbIpVhciGetPortsStatus
{
    public byte MaxPorts;
    public fixed byte Ports[UsbIpInterop.MaxPortCount];
}