using System.Runtime.InteropServices;

namespace SignalPlus.UsbEmulation.UsbIp.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct USBIP_RET_SUBMIT
{
    public int command;
    public int seqnum;
    public int devid;
    public int direction;
    public int ep;
    public int status;
    public int actual_length;
    public int start_frame;
    public int number_of_packets;
    public int error_count;
    public long setup;
}