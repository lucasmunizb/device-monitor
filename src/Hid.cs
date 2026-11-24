using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

internal sealed class HidDevice
{
    internal readonly string Path;
    internal readonly Hid.Caps Caps;

    internal HidDevice(string path, Hid.Caps caps)
    {
        Path = path;
        Caps = caps;
    }
}

internal static class Hid
{
    private const int Pending = 997;

    internal static List<HidDevice> Find(string usbId)
    {
        var devices = new List<HidDevice>();
        Guid guid;
        HidD_GetHidGuid(out guid);
        IntPtr set = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, 0x12);
        if (set == new IntPtr(-1))
            throw new Win32Exception();

        try
        {
            for (uint index = 0; ; index++)
            {
                var entry = new InterfaceData { Size = Marshal.SizeOf(typeof(InterfaceData)) };
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index, ref entry))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 259)
                        break;
                    throw new Win32Exception(error);
                }

                int size;
                SetupDiGetDeviceInterfaceDetail(set, ref entry, IntPtr.Zero, 0, out size, IntPtr.Zero);
                if (size <= 0)
                    continue;

                IntPtr detail = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref entry, detail, size, out size, IntPtr.Zero))
                        continue;

                    string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                    if (!path.StartsWith(@"\\?\hid#" + usbId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    using (var handle = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
                    {
                        IntPtr data;
                        if (handle.IsInvalid || !HidD_GetPreparsedData(handle, out data))
                            continue;

                        try
                        {
                            Caps caps;
                            if (HidP_GetCaps(data, out caps) == 0x110000)
                                devices.Add(new HidDevice(path, caps));
                        }
                        finally
                        {
                            HidD_FreePreparsedData(data);
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(detail);
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }
        return devices;
    }

    internal static SafeFileHandle Open(HidDevice device, bool write)
    {
        uint access = write ? 0xC0000000u : 0x80000000u;
        var handle = CreateFile(device.Path, access, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
        if (!handle.IsInvalid)
            return handle;

        int error = Marshal.GetLastWin32Error();
        handle.Dispose();
        throw new Win32Exception(error);
    }

    internal static int Transfer(SafeFileHandle handle, byte[] report, bool write, int timeout)
    {
        using (var ready = new EventWaitHandle(false, EventResetMode.ManualReset))
        {
            var overlapped = new Overlapped { Event = ready.SafeWaitHandle.DangerousGetHandle() };
            var pinned = GCHandle.Alloc(report, GCHandleType.Pinned);
            IntPtr operation = IntPtr.Zero;
            try
            {
                operation = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Overlapped)));
                Marshal.StructureToPtr(overlapped, operation, false);

                int count;
                bool completed = write
                    ? WriteFile(handle, pinned.AddrOfPinnedObject(), report.Length, out count, operation)
                    : ReadFile(handle, pinned.AddrOfPinnedObject(), report.Length, out count, operation);
                if (completed)
                    return count;

                int error = Marshal.GetLastWin32Error();
                if (error != Pending)
                    throw new Win32Exception(error);

                if (!ready.WaitOne(timeout))
                {
                    // Drain the canceled request before freeing the buffer and OVERLAPPED.
                    CancelIoEx(handle, operation);
                    GetOverlappedResult(handle, operation, out count, true);
                    return 0;
                }

                if (!GetOverlappedResult(handle, operation, out count, false))
                    throw new Win32Exception();
                return count;
            }
            finally
            {
                Marshal.FreeHGlobal(operation);
                pinned.Free();
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InterfaceData
    {
        internal int Size;
        internal Guid Guid;
        internal int Flags;
        internal IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Caps
    {
        internal ushort Usage;
        internal ushort UsagePage;
        internal ushort Input;
        internal ushort Output;
        internal ushort Feature;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        internal ushort[] Reserved;
        internal ushort LinkCollectionNodes;
        internal ushort InputButtonCaps;
        internal ushort InputValueCaps;
        internal ushort InputDataIndices;
        internal ushort OutputButtonCaps;
        internal ushort OutputValueCaps;
        internal ushort OutputDataIndices;
        internal ushort FeatureButtonCaps;
        internal ushort FeatureValueCaps;
        internal ushort FeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Overlapped
    {
        internal IntPtr Internal;
        internal IntPtr InternalHigh;
        internal uint Offset;
        internal uint OffsetHigh;
        internal IntPtr Event;
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(IntPtr data, out Caps caps);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid guid, uint index, ref InterfaceData entry);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData entry,
        IntPtr detail, int size, out int required, IntPtr info);
    [DllImport("setupapi.dll")]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(SafeFileHandle handle, IntPtr buffer, int length, out int count, IntPtr operation);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteFile(SafeFileHandle handle, IntPtr buffer, int length, out int count, IntPtr operation);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetOverlappedResult(SafeFileHandle handle, IntPtr operation, out int count, bool wait);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CancelIoEx(SafeFileHandle handle, IntPtr operation);
}
