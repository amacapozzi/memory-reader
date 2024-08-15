using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public class MemoryReader
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll")]
    public static extern bool VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    private const int PROCESS_VM_READ = 0x0010;
    private const int PROCESS_QUERY_INFORMATION = 0x0400;
    private IntPtr _processHandle;
    private List<string> dump = new List<string>();

    public MemoryReader(int processId)
    {
        _processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, processId);
        if (_processHandle == IntPtr.Zero)
        {
            throw new Exception("Could not open process for reading.");
        }
    }

    public byte[] ReadMemory(IntPtr address, int size)
    {
        byte[] buffer = new byte[size];
        if (ReadProcessMemory(_processHandle, address, buffer, size, out int bytesRead))
        {
            Array.Resize(ref buffer, bytesRead);
            return buffer;
        }
        else
        {
            throw new Exception("Could not read process memory.");
        }
    }

    private static bool IsChar(byte b)
    {
        return (b >= 32 && b <= 126) || b == 10 || b == 13 || b == 9;
    }

    public void ParseMemory(IntPtr address, int regionSize)
    {
        byte[] buffer = ReadMemory(address, regionSize);
        StringBuilder builder = new StringBuilder();
        bool uFlag = true, isUnicode = false;
        byte first = 0, second = 0;

        for (int i = 0; i < buffer.Length; i++)
        {
            bool cFlag = IsChar(buffer[i]);

            if (cFlag && uFlag && isUnicode && first > 0)
            {
                isUnicode = false;
                if (builder.Length > 0) builder.Remove(builder.Length - 1, 1);
                builder.Append((char)buffer[i]);
            }
            else if (cFlag) builder.Append((char)buffer[i]);
            else if (uFlag && buffer[i] == 0 && IsChar(first) && IsChar(second))
                isUnicode = true;
            else if (uFlag && buffer[i] == 0 && IsChar(first) && IsChar(second) && builder.Length < 5)
            {
                isUnicode = true;
                builder = new StringBuilder();
                builder.Append((char)first);
            }
            else
            {
                if (builder.Length >= 5 && builder.Length <= 1500)
                {
                    dump.Add(builder.ToString());
                }
                isUnicode = false;
                builder = new StringBuilder();
            }

            first = second;
            second = buffer[i];
        }
    }

    public void ReadAllMemory()
    {
        IntPtr address = IntPtr.Zero;
        MEMORY_BASIC_INFORMATION mbi;

        while (VirtualQueryEx(_processHandle, address, out mbi, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))))
        {
            if (mbi.State == 0x1000 && (mbi.Protect & 0x04) != 0 && mbi.Protect != 0x100)
            {
                try
                {
                    ParseMemory(mbi.BaseAddress, (int)mbi.RegionSize);

                    foreach (var parsedString in dump)
                    {
                        Console.WriteLine(parsedString);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading memory: {ex.Message}");
                }
            }
            address = (IntPtr)((long)mbi.BaseAddress + (long)mbi.RegionSize);
        }
    }

    public void Close()
    {
        if (_processHandle != IntPtr.Zero)
        {
            CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
        }
    }
}
