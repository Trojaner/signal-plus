using System.Net;
using System.Runtime.InteropServices;

namespace SignalPlus.UsbEmulation.UsbIp.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct OP_REQ
{
    public short version;
    public ushort command;
    public int status;

    public UsbIpCommandType GetCommandType()
    {
        return (UsbIpCommandType)(ushort)IPAddress.NetworkToHostOrder((short)command);
    }
}