using System.Runtime.InteropServices;

namespace SignalPlus.UsbEmulation.UsbIp.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CONFIG_HID
{
    public USB_CONFIGURATION_DESCRIPTOR dev_conf;
    public USB_INTERFACE_DESCRIPTOR dev_int;
    public USB_HID_DESCRIPTOR dev_hid;
    public USB_ENDPOINT_DESCRIPTOR dev_ep_out;
    public USB_ENDPOINT_DESCRIPTOR dev_ep_in;
}