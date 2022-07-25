using System.Runtime.InteropServices;

namespace SignalPlus.UsbEmulation.UsbIp.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct USB_DEVICE_DESCRIPTOR
{
    public byte bLength;               // Length of this descriptor.
    public byte bDescriptorType;       // DEVICE descriptor type (USB_DESCRIPTOR_DEVICE).
    public short bcdUSB;                // USB Spec Release Number (BCD).
    public byte bDeviceClass;          // Class code (assigned by the USB-IF). 0xFF-Vendor specific.
    public byte bDeviceSubClass;       // Subclass code (assigned by the USB-IF).
    public byte bDeviceProtocol;       // Protocol code (assigned by the USB-IF). 0xFF-Vendor specific.
    public byte bMaxPacketSize0;       // Maximum packet size for endpoint 0.
    public short idVendor;              // Vendor ID (assigned by the USB-IF).
    public short idProduct;             // Product ID (assigned by the manufacturer).
    public short bcdDevice;             // Device release number (BCD).
    public byte iManufacturer;         // Index of String Descriptor describing the manufacturer.
    public byte iProduct;              // Index of String Descriptor describing the product.
    public byte iSerialNumber;         // Index of String Descriptor with the device's serial number.
    public byte bNumConfigurations;    // Number of possible configurations.
}