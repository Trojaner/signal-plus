using System.Runtime.InteropServices;

namespace SignalPlus.UsbEmulation.UsbIp.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct OP_REP_IMPORT
{
    public short version;
    public short command;
    public int status;
    //------------- if not ok, finish here
    public fixed byte usbPath[256];
    public fixed byte busID[32];
    public int busnum;
    public int devnum;
    public int speed;
    public short idVendor;
    public short idProduct;
    public short bcdDevice;
    public byte bDeviceClass;
    public byte bDeviceSubClass;
    public byte bDeviceProtocol;
    public byte bConfigurationValue;
    public byte bNumConfigurations;
    public byte bNumInterfaces;
}