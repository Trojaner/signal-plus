using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SignalPlus.Arduino;

namespace SignalPlus;
public class Program
{
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SwHide = 0x00;
    private const int SwShow = 0x05;

    
    private static ArduinoDevice _arduino;
    private static ArduinoVirtualUsb _rgbUsb;

    ~Program()
    {
        _arduino.Dispose();
        _rgbUsb.DisposeAsync().GetAwaiter().GetResult();
    }

    public static async Task Main(string[] args)
    {
        var handle = GetConsoleWindow();
        ShowWindow(handle, args.Contains("--silent") ? SwHide : SwShow);
        
        var comPort = args.Length > 0 ? args[0] : "COM3";

        _arduino = new ArduinoDevice(comPort);
        await _arduino.ConnectAsync();

        Console.WriteLine("Connected to Arduino RGB device");

        _rgbUsb = new ArduinoVirtualUsb(_arduino);
        await _rgbUsb.AttachAndForwardAsync();
    }
}