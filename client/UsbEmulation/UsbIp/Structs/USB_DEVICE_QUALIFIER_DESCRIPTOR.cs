using System.Runtime.InteropServices;

namespace SignalPlus.UsbEmulation.UsbIp.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct USB_DEVICE_QUALIFIER_DESCRIPTOR
{
    public byte bLength;               // Size of this descriptor
    public byte bDescriptorType;                 // Type, always USB_DESCRIPTOR_DEVICE_QUALIFIER
    public short bcdUSB;                // USB spec version, in BCD
    public byte bDeviceClass;          // Device class code
    public byte bDeviceSubClass;       // Device sub-class code
    public byte bDeviceProtocol;       // Device protocol
    public byte bMaxPacketSize0;       // EP0, max packet size
    public byte bNumConfigurations;    // Number of "other-speed" configurations
    public byte bReserved;             // Always zero (0)
};