using System.Runtime.InteropServices;

namespace SignalPlus.UsbEmulation.UsbIp.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct USB_CONFIGURATION_DESCRIPTOR
{
    public byte bLength;               // Length of this descriptor.
    public byte bDescriptorType;       // CONFIGURATION descriptor type (USB_DESCRIPTOR_CONFIGURATION).
    public short wTotalLength;          // Total length of all descriptors for this configuration.
    public byte bNumInterfaces;        // Number of interfaces in this configuration.
    public byte bConfigurationValue;   // Value of this configuration (1 based).
    public byte iConfiguration;        // Index of String Descriptor describing the configuration.
    public byte bmAttributes;          // Configuration characteristics.
    public byte bMaxPower;             // Maximum power consumed by this configuration.
}