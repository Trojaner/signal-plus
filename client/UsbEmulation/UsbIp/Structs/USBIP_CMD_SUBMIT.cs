using System.Runtime.InteropServices;

namespace SignalPlus.UsbEmulation.UsbIp.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct USBIP_CMD_SUBMIT
{
    public int command;
    public int seqnum;
    public int devid;
    public int direction;
    public int ep;
    public int transfer_flags;
    public int transfer_buffer_length;
    public int start_frame;
    public int number_of_packets;
    public int interval;
    public long setup;
}