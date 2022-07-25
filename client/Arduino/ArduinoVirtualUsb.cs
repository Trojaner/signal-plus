using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SignalPlus.UsbEmulation;

namespace SignalPlus.Arduino;

public class ArduinoVirtualUsb : VirtualUsb
{
    private readonly ArduinoDevice _rgbDevice;
    private const int TargetFrameRate = 32;
    private const int MaxLedsPerPacket = 20;

    public const ushort VendorId = 0x1209;
    public const ushort ProductId = 0x1337;

    private readonly Color24[] _image = new Color24[MatrixPanel.Height * MatrixPanel.Width];
    private readonly SemaphoreSlim _semaphore;

    public static readonly byte[] Report =
    {
        0x06, 0x00, 0xFF,  // Usage Page (Vendor Defined 0xFF00)
        0x09, 0x01,        // Usage (0x01)
        0xA1, 0x01,        // Collection (Application)
        0x85, 0x01,        //   Report ID (1)
        0x15, 0x00,        //   Logical Minimum (0)
        0x25, 0x01,        //   Logical Maximum (1)
        0x35, 0x00,        //   Physical Minimum (0)
        0x45, 0x01,        //   Physical Maximum (1)
        0x65, 0x00,        //   Unit (None)
        0x55, 0x00,        //   Unit Exponent (0)
        0x75, 0x01,        //   Report Size (1)
        0x96, 0x00, 0x02,  //   Report Count (512)
        0x81, 0x03,        //   Input (Const,Var,Abs,No Wrap,Linear,Preferred State,No Null Position)
        0x91, 0x03,        //   Output (Const,Var,Abs,No Wrap,Linear,Preferred State,No Null Position,Non-volatile)
        0xC0,              // End Collection
    };


    public ArduinoVirtualUsb(ArduinoDevice rgbDevice) : base(VendorId, ProductId, Report)
    {
        _rgbDevice = rgbDevice;
        _semaphore = new SemaphoreSlim(0);
        Task.Run(RgbDeviceLoopAsync);
    }

    private async Task RgbDeviceLoopAsync()
    {
        while (!IsDisposing)
        {
            for (byte i = 0; i < _image.Length; i++)
            {
                await _rgbDevice.SetLedAsync(i, _image[i]);
            }

            await _semaphore.WaitAsync();
        }
    }


    protected override async Task HandleUsbRequestAsync(byte[] inBuffer, int inBufferSize)
    {
        // Console.WriteLine($"Received: {string.Join(" ", inBuffer.Select(x => x.ToString("X2")))}");

        await using var ms = new MemoryStream(inBuffer);
        var binaryReader = new BinaryReader(ms);

        var startByte = binaryReader.ReadByte();
        if (startByte != 0x01)
        {
            throw new Exception($"Invalid protocol: start byte mismatch; received: 0x{startByte:X}, expected: 0x01");
        }

        var currentPacket = binaryReader.ReadByte();
        // Console.WriteLine("current packet: " + currentPacket);

        for (var led = (byte)(currentPacket * MaxLedsPerPacket); led < (currentPacket + 1) * MaxLedsPerPacket; led++)
        {
            var r = binaryReader.ReadByte();
            var g = binaryReader.ReadByte();
            var b = binaryReader.ReadByte();

            var color = new Color24(r, g, b);
            _image[led] = color;

            if (led == _image.Length - 1)
            {
                break;
            }
        }

        _semaphore.Release();
    }
}