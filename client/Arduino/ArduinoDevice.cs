using System;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using SignalPlus.Helpers;

namespace SignalPlus.Arduino;

public class ArduinoDevice : IDisposable
{
    public int LedCount => MatrixPanel.Width * MatrixPanel.Height;

    private readonly SerialPort _port;

    public ArduinoDevice(string comPort)
    {
        _port = new SerialPort(comPort, 921600, Parity.None, 8, StopBits.One);
    }

    public async Task ConnectAsync()
    {
        _port.DtrEnable = true;
        _port.RtsEnable = true;
        _port.Handshake = Handshake.None;
        _port.Encoding = Encoding.ASCII;
        _port.DataReceived += (s, e) =>
        {
            if (e.EventType == SerialData.Eof)
            {
                return;
            }

            var sp = (SerialPort)s;

            var buf = new byte[sp.BytesToRead];
            sp.Read(buf, 0, buf.Length);

            //Console.WriteLine(_port.Encoding.GetString(buf));
        };

        _port.Open();
        await Task.Delay(2500);
    }

    public async Task SetLedAsync(byte led, Color24 color)
    {
        var colorData = color.ToByteArray();

        var payload = new byte[1 + Color24.Size];
        payload[0] = led;

        Buffer.BlockCopy(colorData, 0, payload, 1, colorData.Length);

        await WriteAsync(0x01, payload);
    }

    public Task SetPixelAsync(byte x, byte y, Color24 color)
    {
        const int width = MatrixPanel.Width;
        var yIndex = y * width;

        var idx = y % 2 == 0
            ? (byte)(yIndex + x)
            : (byte)(yIndex + (byte)(width - 1 - x));

        return SetLedAsync(idx, color);
    }

    public async Task SetLedsAsync(Color24[] leds)
    {
        if (leds.Length > LedCount)
        {
            throw new Exception($"Cannot write more than {LedCount} leds");
        }

        const int chunkSize = 19;

        var chunkCount = Math.Ceiling((float)leds.Length / chunkSize);
        for (byte chunkId = 0; chunkId < chunkCount; chunkId++)
        {
            const int payloadOffset = 2;

            var start = (byte)(chunkId * chunkSize);
            var end = (byte)Math.Min((chunkId + 1) * chunkSize, leds.Length);

            var payload = new byte[3 + (end - start) * Color24.Size];
            payload[0] = start;
            payload[1] = end;

            for (var ledId = start; ledId < end; ledId++)
            {
                var color = leds[ledId];
                var colorData = color.ToByteArray();
                Buffer.BlockCopy(colorData, 0, payload, (ledId - start) * Color24.Size + payloadOffset, Color24.Size);
            }

            await WriteAsync(0x02, payload);
        }
    }

    public async Task SetBrightnessAsync(byte brightness)
    {
        var payload = new byte[1];
        payload[0] = brightness;

        await WriteAsync(0x03, payload);
    }

    protected async Task WriteAsync(byte command, byte[] payload)
    {
        var header = new byte[4];
        header[0] = 0xFC;
        header[1] = command;
        header[2] = (byte)payload.Length;
        header[3] = Crc8.ComputeChecksum(header, header.Length - 1);

        var packet = new byte[header.Length + payload.Length + 1]; // +1 payload crc
        Buffer.BlockCopy(header, 0, packet, 0, header.Length);
        Buffer.BlockCopy(payload, 0, packet, header.Length, payload.Length);
        packet[^1] = Crc8.ComputeChecksum(payload, payload.Length);

        //Console.WriteLine();
        //Console.WriteLine("Sending: " + string.Join(" ", packet.Select(b => b.ToString("X2"))));

        await _port.BaseStream.WriteAsync(packet.AsMemory());
    }

    public void Dispose()
    {
        _port?.Dispose();
    }
}