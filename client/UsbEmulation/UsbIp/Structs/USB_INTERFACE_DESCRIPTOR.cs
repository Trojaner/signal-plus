using System.Runtime.InteropServices;

namespace SignalPlus.UsbEmulation.UsbIp.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct USB_INTERFACE_DESCRIPTOR
{
    public byte bLength;               // Length of this descriptor.
    public byte bDescriptorType;       // INTERFACE descriptor type (USB_DESCRIPTOR_INTERFACE).
    public byte bInterfaceNumber;      // Number of this interface (0 based).
    public byte bAlternateSetting;     // Value of this alternate interface setting.
    public byte bNumEndpoints;         // Number of endpoints in this interface.
    public byte bInterfaceClass;       // Class code (assigned by the USB-IF).  0xFF-Vendor specific.
    public byte bInterfaceSubClass;    // Subclass code (assigned by the USB-IF).
    public byte bInterfaceProtocol;    // Protocol code (assigned by the USB-IF).  0xFF-Vendor specific.
    public byte iInterface;            // Index of String Descriptor describing the interface.
}