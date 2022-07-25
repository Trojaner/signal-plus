using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SignalPlus.Helpers;

public static class SocketHelperExtensions
{
    public static async Task<T> ReadAsAsync<T>(this Socket socket, CancellationToken cancellationToken) where T : unmanaged
    {
        int size;
        unsafe
        {
            size = sizeof(T);
        }

        var buf = new byte[size];

        var received = await socket.ReceiveAsync(buf, SocketFlags.None, cancellationToken);
        if (received != size)
        {
            return default;
        }

        var ptrElem = Marshal.UnsafeAddrOfPinnedArrayElement(buf, 0);
        return Marshal.PtrToStructure<T>(ptrElem);
    }

    public static async Task<T> ReadAndUnpackAsAsync<T>(this Socket socket, CancellationToken cancellationToken) where T : unmanaged
    {
        int size;
        unsafe
        {
            size = sizeof(T);
        }

        var buf = new byte[size];
        
        var received = await socket.ReceiveAsync(buf, SocketFlags.None, cancellationToken);
        if (received != size)
        {
            return default;
        }

        unsafe
        {
            fixed (byte* pByte = buf)
            {
                var intBytes = (int*)pByte;
                Unpack(intBytes, size);
            }
        }

        var ptrElem = Marshal.UnsafeAddrOfPinnedArrayElement(buf, 0);
        return Marshal.PtrToStructure<T>(ptrElem);
    }

    public static unsafe void Pack(int* data, int size)
    {
        size /= 4;
        
        for (var i = 0; i < size; i++)
        {
            data[i] = IPAddress.HostToNetworkOrder(data[i]);
        }
        
        (data[size - 1], data[size - 2]) = (data[size - 2], data[size - 1]);
    }

    public static unsafe void Unpack(int* data, int size)
    {
        size /= 4;

        for (var i = 0; i < size; i++)
        {
            data[i] = IPAddress.NetworkToHostOrder(data[i]);
        }
        
        (data[size - 1], data[size - 2]) = (data[size - 2], data[size - 1]);
    }

    public static async Task<bool> SendAsAsync<T>(this Socket socket, T data, CancellationToken cancellationToken) where T : unmanaged
    {
        int size;
        unsafe
        {
            size = sizeof(T);
        }

        var buf = new byte[size];

        var ptrElem = Marshal.UnsafeAddrOfPinnedArrayElement(buf, 0);
        Marshal.StructureToPtr<T>(data, ptrElem, true);

        var sent = await socket.SendAsync(buf, SocketFlags.None, cancellationToken);
        return sent == size;
    }

    public static async Task<string> ReadBusIdAsync(this Socket socket, CancellationToken cancellationToken)
    {
        var busId = new byte[32];
        
        var received = await socket.ReceiveAsync(busId.AsMemory(), SocketFlags.None, cancellationToken);
        if (received != busId.Length)
        {
            return null;
        }

        var handle = GCHandle.Alloc(busId, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStringAnsi(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }
}
