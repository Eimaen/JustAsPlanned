using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

// https://gist.github.com/Eimaen/0cc658316aa657181fcd061261fe527b

namespace MemoryHacking
{
    /// <summary>
    /// Some memory-related extensions.
    /// </summary>
    public static class Extensions
    {
        public static string Hex(this IntPtr intptr) => "0x" + intptr.ToString("X");
        public static Memory InitializeMemory(this Process process) => new Memory(process);
    }

    public class Memory
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] buffer, long size, out long lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [In] byte[] buffer, long size, out long lpNumberOfBytesWritten);


        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, int processId);

        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        public enum MemoryState
        {
            MemCommit = 0x1000,
            MemReserved = 0x2000,
            MemFree = 0x10000
        }

        public enum MemoryType
        {
            MemPrivate = 0x20000,
            MemMapped = 0x40000,
            MemImage = 0x1000000
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public MemoryState State;
            public MemoryProtection Protect;
            public MemoryType Type;
        }

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

        [Flags]
        public enum AllocationType
        {
            Commit = 0x1000,
            Reserve = 0x2000,
            Decommit = 0x4000,
            Release = 0x8000,
            Reset = 0x80000,
            Physical = 0x400000,
            TopDown = 0x100000,
            WriteWatch = 0x200000,
            LargePages = 0x20000000
        }

        [Flags]
        public enum MemoryProtection
        {
            Execute = 0x10,
            ExecuteRead = 0x20,
            ExecuteReadWrite = 0x40,
            ExecuteWriteCopy = 0x80,
            NoAccess = 0x01,
            ReadOnly = 0x02,
            ReadWrite = 0x04,
            WriteCopy = 0x08,
            GuardModifierflag = 0x100,
            NoCacheModifierflag = 0x200,
            WriteCombineModifierflag = 0x400
        }

        [DllImport("kernel32.dll")]
        static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        /// <summary>
        /// Class that represents a chunk of process's structure.
        /// </summary>
        public class MemoryRegion
        {
            public MemoryRegion() { }

            public MemoryRegion(MEMORY_BASIC_INFORMATION basicInformation)
            {
                BaseAddress = basicInformation.BaseAddress;
                Size = basicInformation.RegionSize.ToInt64();
                State = basicInformation.State;
                Protect = basicInformation.Protect;
                Type = basicInformation.Type;
            }

            public MemoryRegion(ProcessModule module)
            {
                BaseAddress = module.BaseAddress;
                Size = module.ModuleMemorySize;
            }

            public IntPtr BaseAddress;
            public long Size;
            public MemoryState State;
            public MemoryProtection Protect;
            public MemoryType Type;
        }

        /// <summary>
        /// The process to read memory from.
        /// </summary>
        public Process Process { get; private set; }

        public Memory(Process process) => Process = process;

        /// <summary>
        /// Get all process memory regions.
        /// </summary>
        /// <returns>Memory region list.</returns>
        public List<MemoryRegion> GetMemoryRegions()
        {
            var regions = new List<MemoryRegion>();
            IntPtr address = IntPtr.Zero;

            while (VirtualQueryEx(Process.Handle, address, out MEMORY_BASIC_INFORMATION basicInformation, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) != 0)
            {
                if (basicInformation.State != MemoryState.MemFree && !basicInformation.Protect.HasFlag(MemoryProtection.GuardModifierflag))
                    regions.Add(new MemoryRegion(basicInformation));

                address = new IntPtr(basicInformation.BaseAddress.ToInt64() + basicInformation.RegionSize.ToInt64());
            }

            return regions;
        }

        /// <summary>
        /// Get process module memory region by name.
        /// </summary>
        /// <param name="name">Process module name.</param>
        /// <returns><see cref="MemoryRegion"/> instance.</returns>
        public MemoryRegion GetModuleByName(string name)
        {
            foreach (ProcessModule module in Process.Modules)
                if (module.ModuleName == name)
                    return new MemoryRegion(module);

            return null;
        }

        /// <summary>
        /// Write a <see cref="byte"/> array to memory.
        /// </summary>
        /// <param name="address">Address of memory region to begin write.</param>
        /// <param name="value">Array of <see cref="byte"/> to write.</param>
        public void Write(IntPtr address, byte[] value) => WriteProcessMemory(Process.Handle, address, value, value.Length, out _);

        /// <summary>
        /// Write a structure to memory.
        /// </summary>
        /// <typeparam name="T">Target structure type.</typeparam>
        /// <param name="address">Address of memory region to begin write.</param>
        /// <param name="value">Structure to write.</param>
        public void Write<T>(IntPtr address, T value) => Write(address, ToByteArray(value));

        /// <summary>
        /// Read a <see cref="byte"/> array from memory.
        /// </summary>
        /// <param name="address">The beginning of memory region to read from.</param>
        /// <param name="size">Count of bytes to read.</param>
        /// <returns>An array with specified <paramref name="size"/></returns>
        public byte[] Read(IntPtr address, long size)
        {
            byte[] buffer = new byte[size];
            long bytesRead;

            ReadProcessMemory(Process.Handle, address, buffer, size, out bytesRead);

            if (bytesRead == 0)
                return null;

            return buffer;
        }

        /// <summary>
        /// Read a structure from memory.
        /// </summary>
        /// <typeparam name="T">Target structure type.</typeparam>
        /// <param name="address">The beginning of memory region to read from.</param>
        /// <returns>Structure <typeparamref name="T"/>, decoded from bytes.</returns>
        public T Read<T>(IntPtr address) => ToStructure<T>(Read(address, Marshal.SizeOf(typeof(T))));

        /// <summary>
        /// Read and decode string from memory.
        /// </summary>
        /// <param name="address">The beginning of memory region to read from.</param>
        /// <param name="bufferSize">Count of bytes to read.</param>
        /// <param name="encoding">Target string encoding, leave <see cref="null"/> to use default.</param>
        /// <returns>A string read from memory.</returns>
        public string ReadString(IntPtr address, int bufferSize, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;
            string text = encoding.GetString(Read(address, bufferSize));
            if (text.Contains('\0'))
                text = text.Substring(0, text.IndexOf('\0'));
            return text;
        }

        /// <summary>
        /// Read and decode string from memory by its pointer. Useful for dotnet apps.
        /// </summary>
        /// <param name="address">The beginning of memory region to read from.</param>
        /// <param name="encoding">Target string encoding, leave <see cref="null"/> to use default.</param>
        /// <returns>A string read from memory.</returns>
        public string ReadStringByPointer(IntPtr address, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;
            address = Read<IntPtr>(address);
            return encoding.GetString(Read(address + 0x8, Read<int>(address + 0x4) * 2)).Replace("\0", "");
        }

        /// <summary>
        /// Scan for an IDA-type pattern.
        /// </summary>
        /// <param name="pattern">IDA-type pattern.</param>
        /// <param name="offset">Pattern offset.</param>
        /// <returns>Address of the first occurence of the given pattern.</returns>
        public IntPtr PatternScan(string pattern)
        {
            foreach (MemoryRegion region in GetMemoryRegions())
            {
                IntPtr address = PatternScan(pattern, region);
                if (address != IntPtr.Zero)
                    return address;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Scan for an IDA-type pattern in target region memory.
        /// </summary>
        /// <param name="pattern">IDA-type pattern.</param>
        /// <param name="offset">Pattern offset.</param>
        /// <param name="region">A region to scan.</param>
        /// <returns>Address of the first occurence of the given pattern.</returns>
        public IntPtr PatternScan(string pattern, MemoryRegion region) => PatternScan(pattern, region.BaseAddress, region.Size);

        /// <summary>
        /// Scan for an IDA-type pattern in memory chunk.
        /// </summary>
        /// <param name="pattern">IDA-type pattern.</param>
        /// <param name="offset">Pattern offset.</param>
        /// <param name="baseAddress">The beginning of memory chunk.</param>
        /// <param name="scanSize">The size of bytes to check.</param>
        /// <returns>Address of the first occurence of the given pattern.</returns>
        public IntPtr PatternScan(string pattern, IntPtr baseAddress, long scanSize)
        {
            List<byte> patternBytes = new List<byte>();
            List<bool> patternMask = new List<bool>();

            foreach (string byteString in pattern.Split(' '))
            {
                if (byteString == "?" || byteString == "00" || byteString == "??")
                {
                    patternBytes.Add(0x00);
                    patternMask.Add(false);
                }
                else
                {
                    patternBytes.Add(byte.Parse(byteString, NumberStyles.HexNumber));
                    patternMask.Add(true);
                }
            }

            byte[] moduleBytes = Read(baseAddress, scanSize);

            if (moduleBytes == null)
                return IntPtr.Zero;

            for (int i = 0; i < moduleBytes.Length; i++)
            {
                bool found = true;

                for (int l = 0; l < patternMask.Count; l++)
                {
                    if (l + i >= moduleBytes.Length) found = false;
                    else found = !patternMask[l] || moduleBytes[l + i] == patternBytes[l];

                    if (!found) break;
                }

                if (found)
                    return new IntPtr(i + baseAddress.ToInt64());
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Resolve a pointer chain in the region and get the last address.
        /// </summary>
        /// <param name="offsets">Pointer chain to resolve.</param>
        /// <param name="region">Target region.</param>
        /// <returns>The last pointer address from chain.</returns>
        public IntPtr ResolveAddress(int[] offsets, MemoryRegion region) => ResolveAddress(offsets, region.BaseAddress);

        /// <summary>
        /// Resolve a pointer chain and get the last address.
        /// </summary>
        /// <param name="offsets">Pointer chain to resolve.</param>
        /// <param name="baseAddress">The base address to start resolving.</param>
        /// <returns>The last pointer address from chain.</returns>
        public IntPtr ResolveAddress(int[] offsets, IntPtr baseAddress)
        {
            baseAddress += offsets[0];
            for (int offsetId = 1; offsetId < offsets.Length; offsetId++)
                baseAddress = Read<IntPtr>(baseAddress) + offsets[offsetId];
            return baseAddress;
        }

        /// <summary>
        /// Inject dll from file.
        /// </summary>
        /// <param name="dllPath">Dll file path.</param>
        /// <returns></returns>
        public bool Inject(string dllPath)
        {
            IntPtr pProcHandle = OpenProcess(
                ProcessAccessFlags.CreateThread
                | ProcessAccessFlags.VirtualMemoryOperation
                | ProcessAccessFlags.VirtualMemoryRead
                | ProcessAccessFlags.VirtualMemoryWrite
                | ProcessAccessFlags.QueryInformation, true, Process.Id);

            if (pProcHandle == IntPtr.Zero)
                return false;

            IntPtr pLoadLibrary = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");

            if (pLoadLibrary == IntPtr.Zero)
                return false;

            byte[] bytes = Encoding.Default.GetBytes(dllPath);

            IntPtr pMemory = VirtualAllocEx(pProcHandle, (IntPtr)null, (uint)bytes.Length, AllocationType.Commit | AllocationType.Reserve, MemoryProtection.ExecuteReadWrite);

            if (pMemory == IntPtr.Zero)
                return false;

            if (!WriteProcessMemory(pProcHandle, pMemory, bytes, bytes.Length, out _))
                return false;

            if (CreateRemoteThread(pProcHandle, (IntPtr)null, 0, pLoadLibrary, pMemory, 0, (IntPtr)null) == IntPtr.Zero)
                return false;

            CloseHandle(pProcHandle);

            return true;
        }

        /// <summary>
        /// Inject dll loaded from bytes.
        /// </summary>
        /// <param name="bytes">Array of dll bytes.</param>
        /// <returns></returns>
        public bool Inject(byte[] bytes)
        {
            string tempFilePath = Path.GetTempFileName();
            File.WriteAllBytes(tempFilePath, bytes);
            return Inject(tempFilePath);
        }

        /// <summary>
        /// Decode a structure from bytes.
        /// </summary>
        /// <typeparam name="T">Structure type.</typeparam>
        /// <param name="bytes">Bytes to decode.</param>
        /// <returns>The resulting structure.</returns>
        static T ToStructure<T>(byte[] bytes)
        {
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            var structure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();

            return structure;
        }

        /// <summary>
        /// Encode a structure to a byte array.
        /// </summary>
        /// <param name="obj">Target structure.</param>
        /// <returns>A resulting array of bytes.</returns>
        static byte[] ToByteArray(object obj)
        {
            int length = Marshal.SizeOf(obj);
            byte[] array = new byte[length];
            IntPtr pointer = Marshal.AllocHGlobal(length);

            Marshal.StructureToPtr(obj, pointer, true);
            Marshal.Copy(pointer, array, 0, length);
            Marshal.FreeHGlobal(pointer);

            return array;
        }
    }
}
