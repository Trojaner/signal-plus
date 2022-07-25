namespace SignalPlus.UsbEmulation.UsbIp.Structs;

public struct USB_DEVICE_OTHER_SPEED_CONFIGURATION_DESCRIPTOR
{
    public byte bLength;
    public byte bDescriptorType;
    public short wTotalLength;
    public byte bNumInterfaces;
    public byte bConfigurationValue;
    public byte iConfiguration;
    public byte bmAttributes;
    public byte bMaxPower;
}