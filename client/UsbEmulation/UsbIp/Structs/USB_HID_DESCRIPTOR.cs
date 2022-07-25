using System.Runtime.InteropServices;

namespace SignalPlus.UsbEmulation.UsbIp.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct USB_HID_DESCRIPTOR
{
    public byte bLength;
    public byte bDescriptorType;
    public short bcdHID;
    public byte bCountryCode;
    public byte bNumDescriptors;
    public byte bRPDescriptorType;
    public short wRPDescriptorLength;
}