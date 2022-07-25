using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SignalPlus.Helpers;
using SignalPlus.UsbEmulation.UsbIp;
using SignalPlus.UsbEmulation.UsbIp.Structs;
using Vanara.PInvoke;

namespace SignalPlus.UsbEmulation;

public abstract class VirtualUsb : IAsyncDisposable
{
    private readonly byte[] _report;
    private const short UsbIpProtocolVersion = 273;
    private const int UsbIpTcpPort = 3240;

    private readonly USB_DEVICE_DESCRIPTOR _descriptor;
    private readonly CONFIG_HID _configuration;
    private readonly USB_DEVICE_QUALIFIER_DESCRIPTOR _qualifier;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public bool IsAttached => _port > 0;

    private Kernel32.SafeHFILE _deviceHandle;
    private sbyte _port;
    protected bool IsDisposing { get; private set; }

    private readonly string[] _strings =
    {
        "Enes Sadik Ozbek", // iManufacturer
        "Arduino ARGB Controller", // iProduct
        "00000000001A", // iSerialNumber
    };

    private readonly USB_DEVICE_OTHER_SPEED_CONFIGURATION_DESCRIPTOR _speedConfig;


    protected VirtualUsb(ushort vendorId, ushort productId, byte[] report)
    {
        _report = report;
        _descriptor = new USB_DEVICE_DESCRIPTOR
        {
            bDescriptorType = 0x01,
            bcdUSB = 0x0200,
            bDeviceClass = 0x00,
            bDeviceSubClass = 0x00,
            bDeviceProtocol = 0x00,
            bMaxPacketSize0 = 0x40,
            idVendor = (short)vendorId,
            idProduct = (short)productId,
            bcdDevice = 0x0100,
            iManufacturer = 0x01,
            iProduct = 0x02,
            iSerialNumber = 0x03,
            bNumConfigurations = 0x01
        };

        _descriptor.bLength = (byte)Marshal.SizeOf(_descriptor);

        _configuration = new CONFIG_HID
        {
            dev_conf = new USB_CONFIGURATION_DESCRIPTOR
            {
                bLength = 0x09,
                bDescriptorType = (byte)DescriptorType.USB_DESCRIPTOR_CONFIGURATION,
                bNumInterfaces = 0x01,
                bConfigurationValue = 0x1,
                iConfiguration = 0x00,
                bmAttributes = 0xC0,
                bMaxPower = 0x00,
                wTotalLength = 0x0029
            },
            dev_int = new USB_INTERFACE_DESCRIPTOR
            {
                bLength = 0x09,
                bDescriptorType = (byte)DescriptorType.USB_DESCRIPTOR_INTERFACE,
                bInterfaceNumber = 0,
                bAlternateSetting = 0,
                bNumEndpoints = 2,
                bInterfaceClass = 0x03,
                bInterfaceSubClass = 0x00,
                bInterfaceProtocol = 0x00,
                iInterface = 0
            },
            dev_hid = new USB_HID_DESCRIPTOR
            {
                bLength = 0x09,
                bDescriptorType = 0x21,
                bcdHID = 0x0111,
                bCountryCode = 0x00,
                bNumDescriptors = 0x01,
                bRPDescriptorType = 0x22,
                wRPDescriptorLength = (short)_report.Length
            },
            dev_ep_out = new USB_ENDPOINT_DESCRIPTOR
            {
                bLength = 0x07,
                bDescriptorType = (byte)DescriptorType.USB_DESCRIPTOR_ENDPOINT,
                bEndpointAddress = 0x81,
                bmAttributes = 0x03,
                wMaxPacketSize = 0x0040,
                bInterval = 0x01
            },
            dev_ep_in = new USB_ENDPOINT_DESCRIPTOR
            {
                bLength = 0x07,
                bDescriptorType = (byte)DescriptorType.USB_DESCRIPTOR_ENDPOINT,
                bEndpointAddress = 0x02,
                bmAttributes = 0x03,
                wMaxPacketSize = 0x0040,
                bInterval = 0x01
            }
        };

        _qualifier = new USB_DEVICE_QUALIFIER_DESCRIPTOR
        {
            bDescriptorType = 0x6,
            bDeviceClass = _descriptor.bDeviceClass,
            bDeviceProtocol = _descriptor.bDeviceProtocol,
            bDeviceSubClass = _descriptor.bDeviceSubClass,
            bMaxPacketSize0 = _descriptor.bMaxPacketSize0,
            bcdUSB = _descriptor.bcdUSB,
            bNumConfigurations = _descriptor.bNumConfigurations,
            bReserved = 0x00,
        };

        _speedConfig = new USB_DEVICE_OTHER_SPEED_CONFIGURATION_DESCRIPTOR
        {
            bLength = 0x9,
            bDescriptorType = (byte)DescriptorType.USB_DESCRIPTOR_OTHER_SPEED_CONFIGURATION,
            wTotalLength = 0x9,
            bNumInterfaces = 0x1,
            bConfigurationValue = 0x1,
            iConfiguration = 0x00,
            bmAttributes = 0xC0,
            bMaxPower = 0x00,
        };

        _qualifier.bLength = (byte)Marshal.SizeOf(_qualifier);
        _cancellationTokenSource = new CancellationTokenSource();
    }

    ~VirtualUsb()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }

    public async Task AttachAndForwardAsync()
    {
        if (_port != 0)
        {
            throw new InvalidOperationException("Already attached to virtual USB device");
        }

        _deviceHandle = UsbIpInterop.OpenVhciHandle();

        var port = await UsbIpInterop.GetVhciFreePortAsync(_deviceHandle);
        if (port is null or <= 0)
        {
            throw new Exception("No free VHCI port available");
        }

        // Detach all previous ports
        for (sbyte oldPort = 1; oldPort < port; oldPort++)
        {
            await UsbIpInterop.DetachAsync(_deviceHandle, oldPort);
        }

        await PrepareUsbIpAsync();

        _ = Task.Run(StartServerAsync);

        var tracker = new ProcessJobTracker();
        await ProcessHelper.RunAsync(
            workingDirectory: "usbip",
            exe: Path.Combine("usbip", "usbip.exe"),
            args: $"attach -r 127.0.0.1 -b 1-1",
            tracker,
            waitForExit: true);

        while (!IsDisposing)
        {
            await Task.Delay(16);
        }

        await DisposeAsync();
    }

    private async Task StartServerAsync()
    {
        try
        {
            var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            {
                var endPoint = new IPEndPoint(IPAddress.Loopback, UsbIpTcpPort);

                serverSocket.Bind(endPoint);
                serverSocket.Listen(10);

                while (!IsDisposing)
                {
                    var clientSocket = await serverSocket.AcceptAsync(_cancellationTokenSource.Token);
                    _ = Task.Run(() => RunClientLoopAsync(clientSocket));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Environment.Exit(-1);
        }
    }

    private async Task RunClientLoopAsync(Socket clientSocket)
    {
        var loop = true;
        var attached = false;
        
        while (loop && !IsDisposing)
        {
            if (!attached)
            {
                var req = await clientSocket.ReadAsAsync<OP_REQ>(_cancellationTokenSource.Token);
                
                var cmdType = req.GetCommandType();
                if (cmdType == UsbIpCommandType.REQ_IMPORT)
                {
                    var busId = await clientSocket.ReadBusIdAsync(_cancellationTokenSource.Token);
                    if (!await ImportDeviceAsync(clientSocket, busId))
                    {
                        Console.WriteLine("Failed to import USB device");
                        break;
                    }

                    attached = true;
                }
                else
                {
                    throw new NotImplementedException($"Request cmd 0x{cmdType:X} is not implemented");
                }
            }

            int commandSize;
            unsafe
            {
                commandSize = sizeof(USBIP_CMD_SUBMIT);
            }

            var buf = new byte[commandSize];

            var read = await clientSocket.ReceiveAsync(buf, SocketFlags.None, _cancellationTokenSource.Token);
            if (read != commandSize)
            {
                break;
            }

            unsafe
            {
                fixed (byte* pByte = buf)
                {
                    var intBytes = (int*)pByte;
                    SocketHelperExtensions.Unpack(intBytes, read);
                }
            }

            var ptrElem = Marshal.UnsafeAddrOfPinnedArrayElement(buf, 0);
            var cmd = Marshal.PtrToStructure<USBIP_CMD_SUBMIT>(ptrElem);

            switch (cmd.command)
            {
                case 0x1:
                    var usbResponse = new USBIP_RET_SUBMIT
                    {
                        command = 0,
                        seqnum = cmd.seqnum,
                        devid = cmd.devid,
                        direction = cmd.direction,
                        ep = cmd.ep,
                        status = 0,
                        actual_length = 0,
                        start_frame = 0,
                        number_of_packets = 0,
                        error_count = 0,
                        setup = cmd.setup,
                    };

                    if (usbResponse.ep == 0)
                    {
                        await HandleUsbControlAsync(clientSocket, usbResponse);
                    }
                    else
                    {
                        await HandleUsbDataAsync(clientSocket, usbResponse, cmd.transfer_buffer_length);
                    }

                    break;

                case 0x2:
                    // unlink urb, not implemented yet
                    break;

                default:
                    Console.WriteLine($"ERR: Unknown USB/IP cmd: {cmd.command:X}, aborting!");
                    Debugger.Break();
                    loop = false;
                    clientSocket.Close();
                    break;
            }
        }

        await clientSocket.DisconnectAsync(false);
        clientSocket.Dispose();
    }

    private byte[] StructToBytes<T>(T obj) where T : struct
    {
        var size = Marshal.SizeOf(obj);
        var ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(obj, ptr, false);
        var bytes = new byte[size];
        Marshal.Copy(ptr, bytes, 0, size);
        Marshal.FreeHGlobal(ptr);
        return bytes;
    }
    
    private async Task HandleUsbDataAsync(Socket clientSocket, USBIP_RET_SUBMIT usbResponse, int inBufferSize)
    {
        if (usbResponse.direction == 0)
        {
            var inBuffer = new byte[inBufferSize];
            if (await clientSocket.ReceiveAsync(inBuffer, SocketFlags.None) != inBufferSize)
            {
                return;
            }

            await HandleUsbRequestAsync(inBuffer, inBufferSize);
        }


        var outBuffer = Array.Empty<byte>();
        await SendUsbResponseAsync(clientSocket, usbResponse, outBuffer, (uint)outBuffer.Length, 0);

    }

    private async Task HandleUsbControlAsync(Socket clientSocket, USBIP_RET_SUBMIT usbResponse)
    {
        var handled = false;
        var controlRequest = new USB_STANDARD_DEVICE_REQUEST
        {
            bmRequestType = (byte)(((ulong)usbResponse.setup & 0xFF00000000000000) >> 56),
            bRequest = (byte)(((ulong)usbResponse.setup & 0x00FF000000000000) >> 48),
            wValue0 = (byte)(((ulong)usbResponse.setup & 0x0000FF0000000000) >> 40),
            wValue1 = (byte)(((ulong)usbResponse.setup & 0x000000FF00000000) >> 32),
            wIndex0 = (byte)(((ulong)usbResponse.setup & 0x00000000FF000000) >> 24),
            wIndex1 = (byte)(((ulong)usbResponse.setup & 0x0000000000FF0000) >> 16),
            wLength = IPAddress.NetworkToHostOrder((short)(usbResponse.setup & 0x000000000000FFFF))
        };

        // Console.WriteLine($"USB control request: bmRequestType: {controlRequest.bmRequestType:X2}, bRequest: {controlRequest.bRequest:X2}");

        if (controlRequest.bmRequestType == 0x80) // Host Request
        {
            if (controlRequest.bRequest == 0x06) // Get Descriptor
            {
                handled = await HandleGetDescriptorAsync(clientSocket, controlRequest, usbResponse);
            }

            if (controlRequest.bRequest == 0x00) // Get STATUS
            {
                var data = new byte[2];
                data[0] = 0x01;
                data[1] = 0x00;
                await SendUsbResponseAsync(clientSocket, usbResponse, data, 2, 0);
                handled = true;
            }
        }

        if (controlRequest.bmRequestType == 0x00)
        {
            if (controlRequest.bRequest == 0x09) // Set Configuration
            {
                handled = await HandleSetConfigurationAsync(clientSocket, usbResponse);
            }
        }

        if (controlRequest.bmRequestType == 0x01)
        {
            if (controlRequest.bRequest == 0x0B) //SET_INTERFACE  
            {
                await SendUsbResponseAsync(clientSocket, usbResponse, null, 0, 1);
                handled = true;
            }
        }

        if (!handled)
        {
            await HandleUnknownControlAsync(clientSocket, controlRequest, usbResponse);
        }
    }

    private async Task<bool> HandleSetConfigurationAsync(Socket clientSocket, USBIP_RET_SUBMIT usbRequest)
    {
        await SendUsbResponseAsync(clientSocket, usbRequest, null, 0, 0);
        return true;
    }

    private async Task<bool> HandleGetDescriptorAsync(Socket clientSocket, USB_STANDARD_DEVICE_REQUEST controlRequest, USBIP_RET_SUBMIT usbRequest)
    {
        // Console.WriteLine($"Get descriptor: {controlRequest.wValue1:X2}");

        var handled = false;
        switch ((DescriptorType)controlRequest.wValue1)
        {
            case DescriptorType.USB_DESCRIPTOR_DEVICE:
                handled = true;

                var deviceDescBufffer = StructureToBytes(_descriptor);

                uint size;
                unsafe
                {
                    size = (uint)sizeof(USB_DEVICE_DESCRIPTOR);
                }

                await SendUsbResponseAsync(clientSocket, usbRequest, deviceDescBufffer, size, 0);
                break;

            case DescriptorType.USB_DESCRIPTOR_CONFIGURATION:
                var configurationBuffer = StructureToBytes(_configuration);
                await SendUsbResponseAsync(clientSocket, usbRequest, configurationBuffer, (uint)controlRequest.wLength, 0);
                handled = true;
                break;

            case DescriptorType.USB_DESCRIPTOR_STRING:
                var stringBuffer = Encoding.Unicode.GetBytes(" " + (controlRequest.wValue0 > 0 ? _strings[controlRequest.wValue0 - 1] : string.Empty) + " ");
                await SendUsbResponseAsync(clientSocket, usbRequest, stringBuffer, (uint)stringBuffer.Length, 0);
                handled = true;
                break;

            case DescriptorType.USB_DESCRIPTOR_DEVICE_QUALIFIER:
                var qualifierBuffer = StructureToBytes(_qualifier);
                await SendUsbResponseAsync(clientSocket, usbRequest, qualifierBuffer, (uint)controlRequest.wLength, 0);
                handled = true;
                break;

            case DescriptorType.USB_DESCRIPTOR_OTHER_SPEED_CONFIGURATION:
                var speedBuffer = StructureToBytes(_speedConfig);
                await SendUsbResponseAsync(clientSocket, usbRequest, speedBuffer, (uint)controlRequest.wLength, 0);
                handled = true;
                
                break;

/*
            case 0xA:
                handled = true;
                await SendUsbResponseAsync(clientSocket, usbRequest, null, 0, 1);
                break;
*/
        }

        return handled;
    }

    protected virtual async Task HandleUnknownControlAsync(Socket socket, USB_STANDARD_DEVICE_REQUEST controlRequest, USBIP_RET_SUBMIT usbRequest)
    {
        switch (controlRequest.bmRequestType)
        {
            case 0x81:
                if (controlRequest.bRequest == 0x6)  // Get Descriptor
                {
                    if (controlRequest.wValue1 == 0x22)  // send initial report
                    {
                        await SendUsbResponseAsync(socket, usbRequest, _report, (uint)_report.Length, 0);
                    }
                }

                break;

            // Host Request
            case 0x21:
                if (controlRequest.bRequest == 0x0a)  // set idle
                {
                    await SendUsbResponseAsync(socket, usbRequest, null, 0, 0);
                }

                if (controlRequest.bRequest == 0x09)  // set report
                {
                    var data = new Memory<byte>(new byte[20]);
                    if (await socket.ReceiveAsync(data, SocketFlags.None) != controlRequest.wLength)
                    {
                        throw new Exception("Receive failed");
                    }

                    await SendUsbResponseAsync(socket, usbRequest, null, 0, 0);
                }

                break;


            default:
                Console.WriteLine($"WARN: Unhandled request; bmRequestType: 0x{controlRequest.bmRequestType:X}, bRequest: 0x{controlRequest.bRequest:X}");
                break;
        }
    }

    protected virtual async Task SendUsbResponseAsync(Socket socket, USBIP_RET_SUBMIT usbRequest, byte[] data, uint size, uint status)
    {
        usbRequest.command = 0x3;
        usbRequest.status = (int)status;
        usbRequest.actual_length = (int)size;
        usbRequest.start_frame = 0x0;
        usbRequest.number_of_packets = 0x0;

        usbRequest.setup = 0x0;
        usbRequest.devid = 0x0;
        usbRequest.direction = 0x0;
        usbRequest.ep = 0x0;

        var buf = StructureToBytes(usbRequest);
        unsafe
        {
            fixed (byte* pByte = buf)
            {
                var intBytes = (int*)pByte;
                SocketHelperExtensions.Pack(intBytes, sizeof(USBIP_RET_SUBMIT));
            }
        }

        int expectedSize;
        unsafe
        {
            expectedSize = sizeof(USBIP_RET_SUBMIT);
        }

        if (await socket.SendAsync(new ArraySegment<byte>(buf, 0, buf.Length), SocketFlags.None, _cancellationTokenSource.Token) != expectedSize)
        {
            throw new Exception("Failed to send data");
        }

        if (size > 0)
        {
            if (await socket.SendAsync(new ArraySegment<byte>(data, 0, (int)size), SocketFlags.None, _cancellationTokenSource.Token) != size)
            {
                throw new Exception("Failed to send data");
            }
        }
    }

    private async Task<bool> ImportDeviceAsync(Socket clientSocket, string busId)
    {
        var rep = new OP_REP_IMPORT
        {
            version = IPAddress.HostToNetworkOrder(UsbIpProtocolVersion),
            command = IPAddress.HostToNetworkOrder((short)UsbIpCommandType.RESP_IMPORT),
            status = 0,

            busnum = IPAddress.HostToNetworkOrder(1),
            devnum = IPAddress.HostToNetworkOrder(2),
            speed = IPAddress.HostToNetworkOrder(2),

            idVendor = _descriptor.idVendor,
            idProduct = _descriptor.idProduct,

            bcdDevice = _descriptor.bcdDevice,
            bDeviceClass = _descriptor.bDeviceClass,
            bDeviceSubClass = _descriptor.bDeviceSubClass,
            bDeviceProtocol = _descriptor.bDeviceProtocol,
            bNumConfigurations = _descriptor.bNumConfigurations,
            bConfigurationValue = _configuration.dev_conf.bConfigurationValue,
            bNumInterfaces = _configuration.dev_conf.bNumInterfaces,
        };

        unsafe
        {
            var buf = Encoding.ASCII.GetBytes($"/sys/devices/pci0000:00/0000:00:01.2/usb1/{busId}");
            Marshal.Copy(buf, 0, new IntPtr(rep.usbPath), buf.Length);

            buf = Encoding.ASCII.GetBytes(busId);
            Marshal.Copy(buf, 0, new IntPtr(rep.busID), buf.Length);
        }

        return await clientSocket.SendAsAsync(rep, _cancellationTokenSource.Token);

    }

    protected unsafe byte[] StructureToBytes<T>(T data) where T : unmanaged
    {
        var size = sizeof(T);
        var buf = new byte[size];

        var ptrElem = Marshal.UnsafeAddrOfPinnedArrayElement(buf, 0);
        Marshal.StructureToPtr(data, ptrElem, true);

        return buf;
    }

    protected abstract Task HandleUsbRequestAsync(byte[] inBuffer, int inBufferSize);

    private async Task PrepareUsbIpAsync()
    {
        //var stubInstalled = ServiceHelper.IsServiceInstalled("usbip_stub");
        //var stubRunning = ServiceHelper.IsServiceRunning("usbip_stub");

        var vhciInstalled = ServiceHelper.IsServiceInstalled("usbip_vhci_ude");
        var vhciRunning = ServiceHelper.IsServiceRunning("usbip_vhci_ude");

        var certPath = Path.Combine("usbip", "usbip_test.pfx");
        using var caStore = new X509Store(StoreName.CertificateAuthority, StoreLocation.LocalMachine);
        caStore.Open(OpenFlags.ReadWrite);
        var caCertInstalled = caStore.Certificates.Any(c => c.Subject == "CN=USBIP Test");

        using var publisherStore = new X509Store(StoreName.TrustedPublisher, StoreLocation.LocalMachine);
        publisherStore.Open(OpenFlags.ReadWrite);
        var publisherCertInstalled = publisherStore.Certificates.Any(c => c.Subject == "CN=USBIP Test");

        var scPath = Path.Combine(Environment.SystemDirectory, "sc.exe");

        // var needsKdu = !stubInstalled || !stubRunning; // stub is not a signed driver
        //if (needsKdu)
        //{
        //    await ProcessHelper.RunAsync("kdu", Path.Combine("kdu", "kdu.exe"), "-dse 0", waitForExit: true);
        //}

        //try
        //{
        //if (!stubInstalled)
        //{
        //    var binPath = Path.Combine(Environment.CurrentDirectory, "usbip", "usbip_stub.sys");
        //    await ProcessHelper.RunAsync(".",
        //        "sc.exe",
        //        $"create usbip_stub displayname=\"USB/IP STUB Service\" binpath=\"{binPath}\" type=kernel start=demand error=normal group=\"Extended Base\"",
        //        isShellExecute: true,
        //        waitForExit: true);
        //}

        //if (!stubRunning)
        //{
        //    await ProcessHelper.RunAsync(".", scPath, "start usbip_stub", waitForExit: true);
        //}

        if (!vhciInstalled)
        {
            await ProcessHelper.RunAsync("usbip", Path.Combine("usbip", "usbip.exe"), "install -u", waitForExit: true);
        }

        if (!vhciRunning)
        {
            await ProcessHelper.RunAsync(".", scPath, "start usbip_vhci_ude", waitForExit: true);
        }

        //}
        //finally
        //{
        //    if (needsKdu)
        //    {
        //        await ProcessHelper.RunAsync("kdu", Path.Combine("kdu", "kdu.exe"), "-dse 22", waitForExit: true);
        //    }
        //}

        if (!caCertInstalled)
        {
            Console.WriteLine("Installing USB-IP CA certificate");
            caStore.Add(new X509Certificate2(certPath, password: "usbip"));
        }

        caStore.Close();

        if (!publisherCertInstalled)
        {
            Console.WriteLine("Installing USB-IP publisher certificate");
            publisherStore.Add(new X509Certificate2(certPath, password: "usbip"));
        }

        publisherStore.Close();
    }

    public async Task DetachAsync()
    {
        if (!IsAttached)
        {
            return;
        }

        await UsbIpInterop.DetachAsync(_deviceHandle, _port);
        _port = 0;
    }

    public virtual async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (IsDisposing)
        {
            return;
        }

        _cancellationTokenSource.Cancel();

        IsDisposing = true;
        if (_deviceHandle != null && !_deviceHandle.IsInvalid)
        {
            if (IsAttached)
            {
                await DetachAsync();
            }

            _deviceHandle.Dispose();
        }
    }
}