using System;
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
    private readonly StringParser _stringParser;

    public MemoryReader(int processId)
    {
        _processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, processId);
        if (_processHandle == IntPtr.Zero)
        {
            throw new Exception("Could not open process for reading.");
        }
        _stringParser = new StringParser();
    }

    public byte[] ReadMemory(IntPtr address, int size)
    {
        byte[] buffer = new byte[size];
        if (ReadProcessMemory(_processHandle, address, buffer, size, out int bytesRead) && bytesRead == size)
        {
            return buffer;
        }
        else
        {
            throw new Exception("Could not read process memory.");
        }
    }

    public string ReadMemoryAsString(IntPtr address, int size, Encoding encoding)
    {
        byte[] buffer = ReadMemory(address, size);
        return encoding.GetString(buffer);
    }

    public void Close()
    {
        if (_processHandle != IntPtr.Zero)
        {
            CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
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
                    byte[] memoryContent = ReadMemory(mbi.BaseAddress, (int)mbi.RegionSize);
                    bool p = _stringParser.ProcessContents(memoryContent, memoryContent.Length, "memory");
                    Console.WriteLine(p);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading memory: {ex.Message}");
                }
            }
            address = (IntPtr)((long)mbi.BaseAddress + (long)mbi.RegionSize);
        }
    }
}