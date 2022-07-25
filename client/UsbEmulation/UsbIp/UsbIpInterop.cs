using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SignalPlus.UsbEmulation.UsbIp.Structs;
using Vanara.PInvoke;

namespace SignalPlus.UsbEmulation.UsbIp;

public static class UsbIpInterop
{
    public const int MaxPortCount = 127;
    public static readonly Guid UsbIpVhciGuid = new(0xD35F7840, 0x6A0C, 0x11d2, 0xB8, 0x41, 0x00, 0xC0, 0x4F, 0xAD, 0x51, 0x71);

    public static Kernel32.SafeHFILE OpenVhciHandle()
    {
        var devPath = (string)null;
        var devInfo = SetupAPI.SetupDiGetClassDevs(UsbIpVhciGuid, null, default, SetupAPI.DIGCF.DIGCF_PRESENT | SetupAPI.DIGCF.DIGCF_DEVICEINTERFACE);
        if (devInfo.IsInvalid)
        {
            throw new Win32Exception("SetupDiGetClassDevs failed");
        }

        try
        {
            var deviceNoMap = new char[255];

            foreach (var devInfoData in SetupAPI.SetupDiEnumDeviceInfo(devInfo))
            {
                var instanceId = GetInstanceId(devInfo, devInfoData);
                var deviceNo = GetDeviceNoFromInstanceId(deviceNoMap, instanceId);
                if (deviceNo == 0)
                {
                    continue;
                }

                if (WalkDevicePath(devInfo, devInfoData, out devPath))
                {
                    break;
                }
            }
        }
        finally
        {
            SetupAPI.SetupDiDestroyDeviceInfoList(devInfo);
        }

        return OpenDeviceHandle(devPath);
    }

    private static Kernel32.SafeHFILE OpenDeviceHandle(string devPath)
    {
        return Kernel32.CreateFile(
            devPath,
            Kernel32.FileAccess.GENERIC_READ | Kernel32.FileAccess.GENERIC_WRITE,
            0,
            null, FileMode.Open, FileFlagsAndAttributes.FILE_FLAG_OVERLAPPED, null);

    }

    private static bool WalkDevicePath(SetupAPI.SafeHDEVINFO deviceInfo, SetupAPI.SP_DEVINFO_DATA deviceInfoData, out string devicePath)
    {
        devicePath = null;

        var hwId = GetHardwareId(deviceInfo, deviceInfoData);
        if (hwId == null || !hwId.Contains("root\\vhci_ude"))
        {
            throw new Exception($"Invalid hardware ID: {hwId}");
        }

        if (!GetInterfaceDetail(deviceInfo, deviceInfoData, UsbIpVhciGuid, out devicePath))
        {
            return false;
        }

        return true;
    }

    private static string GetHardwareId(SetupAPI.SafeHDEVINFO deviceInfo, SetupAPI.SP_DEVINFO_DATA deviceInfoData)
    {
        return GetDeviceProperty(deviceInfo, deviceInfoData, SetupAPI.SPDRP.SPDRP_HARDWAREID);
    }

    private static string GetDeviceProperty(SetupAPI.SafeHDEVINFO deviceInfo, SetupAPI.SP_DEVINFO_DATA deviceInfoData, SetupAPI.SPDRP property)
    {
        if (!SetupAPI.SetupDiGetDeviceRegistryProperty(deviceInfo, deviceInfoData, property, out _, default, 0, out var len))
        {
            var err = Win32Error.GetLastError();
            if (err == Win32Error.ERROR_INVALID_DATA)
            {
                return "";
            }

            err.ThrowUnless(Win32Error.ERROR_INSUFFICIENT_BUFFER);
        }

        var propertyBuffer = Marshal.AllocHGlobal((int)len);

        if (!SetupAPI.SetupDiGetDeviceRegistryProperty(deviceInfo, deviceInfoData, property, out _, propertyBuffer, len, out len))
        {
            Marshal.FreeHGlobal(propertyBuffer);
            Win32Error.ThrowLastError();
            return null;
        }

        var propertyValue = Marshal.PtrToStringUni(propertyBuffer);
        Marshal.FreeHGlobal(propertyBuffer);

        return propertyValue;
    }

    private static bool GetInterfaceDetail(SetupAPI.SafeHDEVINFO deviceInfo, SetupAPI.SP_DEVINFO_DATA deviceInfoData, Guid deviceGuid, out string devicePath)
    {
        devicePath = default;
        SetupAPI.SP_DEVICE_INTERFACE_DATA deviceInterfaceData = default;

        unsafe
        {
            deviceInterfaceData.cbSize = (uint)sizeof(SetupAPI.SP_DEVICE_INTERFACE_DATA);
        }

        if (!SetupAPI.SetupDiEnumDeviceInterfaces(deviceInfo, deviceInfoData, deviceGuid, 0, ref deviceInterfaceData))
        {
            Win32Error.ThrowLastErrorUnless(Win32Error.ERROR_NO_MORE_ITEMS);
            return false;
        }

        SetupAPI.SetupDiGetDeviceInterfaceDetail(deviceInfo, deviceInterfaceData, default, 0, out var len);

        Win32Error.ThrowLastErrorUnless(Win32Error.ERROR_INSUFFICIENT_BUFFER);

        var pDeviceInterfaceDetailData = Marshal.AllocHGlobal((int)len);

        try
        {
            Marshal.WriteInt32(pDeviceInterfaceDetailData, 8);

            // Try to get device details.
            if (!SetupAPI.SetupDiGetDeviceInterfaceDetail(deviceInfo, deviceInterfaceData,
                    pDeviceInterfaceDetailData, len, out len, ref deviceInfoData))
            {
                Win32Error.ThrowLastError();
                return default;
            }


            if (pDeviceInterfaceDetailData == IntPtr.Zero)
            {
                return false;
            }

            devicePath = Marshal.PtrToStringUni(pDeviceInterfaceDetailData + 4);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(pDeviceInterfaceDetailData);
        }
    }

    private static int GetDeviceNoFromInstanceId(char[] deviceNoMap, string instanceId)
    {
        var instanceIdArray = instanceId.ToCharArray();
        byte devno = 0;

        foreach (var i in instanceIdArray)
        {
            devno += (byte)(i * 19 + 13);
        }

        if (devno == 0)
        {
            devno++;
        }

        var ndevs = 0;
        while (deviceNoMap[devno - 1] > 0)
        {
            if (devno == 255)
            {
                devno = 1;
            }
            else
            {
                devno++;
            }
            if (ndevs == 255)
            {
                /* devno map is full */
                return 0;
            }
            ndevs++;
        }

        deviceNoMap[devno - 1] = (char)1;
        return devno;
    }

    private static string GetInstanceId(SetupAPI.SafeHDEVINFO deviceInfo, SetupAPI.SP_DEVINFO_DATA deviceInfoData)
    {
        if (!SetupAPI.SetupDiGetDeviceInstanceId(deviceInfo, deviceInfoData, null, 0, out var length))
        {
            Win32Error.ThrowLastErrorUnless(Win32Error.ERROR_INSUFFICIENT_BUFFER);
        }

        var instanceId = new StringBuilder((int)length);

        if (!SetupAPI.SetupDiGetDeviceInstanceId(deviceInfo, deviceInfoData, instanceId, length, out _))
        {
            Win32Error.ThrowLastError();
        }

        return instanceId.ToString();
    }

    public static async Task<sbyte?> GetVhciFreePortAsync(Kernel32.SafeHFILE device)
    {
        var result = await Kernel32.DeviceIoControlAsync<IoctlUsbIpVhciGetPortsStatus>(
            device,
            UsbIpVhciIoctlCode(0x3));
        if (result == null)
        {
            Win32Error.ThrowLastError();
        }

        unsafe
        {
            var val = result!.Value;
            var ports = val.Ports;

            for (sbyte i = 1; i < MaxPortCount; i++)
            {
                if (ports[i] == 0)
                {
                    return i;
                }
            }
        }

        return null;
    }

    public static async Task<sbyte> AttachDeviceAsync(
        Kernel32.SafeHFILE device,
        sbyte port,
        string instanceId,
        uint deviceId,
        USB_DEVICE_DESCRIPTOR descriptor,
        USB_CONFIGURATION_DESCRIPTOR configuration)
    {
        if (instanceId != null)
        {
            // todo
            throw new NotImplementedException();
        }

        var plugin = new IoctlUsbIpVhciPlugin
        {
            DeviceId = deviceId,
            Port = port,
            DeviceDescriptor = descriptor,
            DeviceConfiguration = configuration
        };

        plugin.Size = Marshal.SizeOf(plugin);

        var response = await Kernel32.DeviceIoControlAsync<IoctlUsbIpVhciPlugin, IoctlUsbIpVhciPlugin>(
            device,
            UsbIpVhciIoctlCode(0x0),
            plugin
        );

        if (response == null)
        {
            Win32Error.ThrowLastError();
        }

        return response!.Value.Port;
    }

    public static async Task DetachAsync(Kernel32.SafeHFILE device, sbyte port)
    {
        var unplug = new IoctlUsbIpVhciUnplug
        {
            Port = port
        };

        await Kernel32.DeviceIoControlAsync(device,
            UsbIpVhciIoctlCode(0x1),
            unplug);
    }

    private static uint UsbIpVhciIoctlCode(ushort function)
    {
        return Kernel32.CTL_CODE(
            Kernel32.DEVICE_TYPE.FILE_DEVICE_BUS_EXTENDER,
            function,
            Kernel32.IOMethod.METHOD_BUFFERED,
            Kernel32.IOAccess.FILE_READ_ACCESS);
    }
}