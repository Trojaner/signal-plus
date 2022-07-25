using System.Runtime.InteropServices;
using SignalPlus.UsbEmulation.UsbIp.Structs;

namespace SignalPlus.UsbEmulation.UsbIp;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct IoctlUsbIpVhciPlugin
{
    public const int MaxVhciSerialId = 127;

    public int Size;

    public uint DeviceId;
    public sbyte Port;

    public fixed byte Serial[(MaxVhciSerialId + 1) * 2];

    public USB_DEVICE_DESCRIPTOR DeviceDescriptor;
    public USB_CONFIGURATION_DESCRIPTOR DeviceConfiguration;
    
}