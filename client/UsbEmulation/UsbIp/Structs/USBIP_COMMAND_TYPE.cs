namespace SignalPlus.UsbEmulation.UsbIp.Structs;

public enum UsbIpCommandType
{
    UNKNOWN = 0x0,

    REQ_IMPORT = 0x80 << 8 | 0x03,
    RESP_IMPORT = 0x00 << 8 | 0x03,

    REQ_DEVLIST = 0x80 << 8 | 0x05,
    RESP_DEVLIST = 0x00 << 8 | 0x05
}