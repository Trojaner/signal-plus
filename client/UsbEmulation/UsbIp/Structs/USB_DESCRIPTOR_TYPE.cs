namespace SignalPlus.UsbEmulation.UsbIp.Structs;

public enum DescriptorType : byte
{
    USB_DESCRIPTOR_DEVICE = 0x01,    // Device Descriptor.
    USB_DESCRIPTOR_CONFIGURATION = 0x02,    // Configuration Descriptor.
    USB_DESCRIPTOR_STRING = 0x03,    // String Descriptor.
    USB_DESCRIPTOR_INTERFACE = 0x04,    // Interface Descriptor.
    USB_DESCRIPTOR_ENDPOINT = 0x05,    // Endpoint Descriptor.
    USB_DESCRIPTOR_DEVICE_QUALIFIER = 0x06,    // Device Qualifier.
    USB_DESCRIPTOR_OTHER_SPEED_CONFIGURATION = 0x07,    // Device Qualifier.
}