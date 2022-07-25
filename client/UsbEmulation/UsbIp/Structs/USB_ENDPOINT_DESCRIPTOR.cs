using System.Runtime.InteropServices;

namespace SignalPlus.UsbEmulation.UsbIp.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct USB_ENDPOINT_DESCRIPTOR
{
    public byte bLength;               // Length of this descriptor.
    public byte bDescriptorType;       // ENDPOINT descriptor type (USB_DESCRIPTOR_ENDPOINT).
    public byte bEndpointAddress;      // Endpoint address. Bit 7 indicates direction (0=OUT, 1=IN).
    public byte bmAttributes;          // Endpoint transfer type.
    public short wMaxPacketSize;        // Maximum packet size.
    public byte bInterval;             // Polling interval in frames.
}