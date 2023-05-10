using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace JustAsPlanned
{
    static class MuseDash
    {
        private static class Patterns
        {
            public static readonly string SteamDlcCheck = @"40 53 48 83 EC ? 8B D9 33 C9 E8 ? ? ? ? 80 3D ? ? ? ? ? 75 ? 8B 0D ? ? ? ? E8 ? ? ? ? C6 05 ? ? ? ? ? 48 8B 05 ? ? ? ? 45 33 C0 8B D3 48 8B 88 ? ? ? ? 48 8B 49 ? 48 83 C4 ? 5B E9 ? ? ? ? CC CC CC CC CC 48 83 EC ? 33 C9 E8 ? ? ? ? 80 3D ? ? ? ? ? 75 ? 8B 0D ? ? ? ? E8 ? ? ? ? C6 05 ? ? ? ? ? 48 8B 05 ? ? ? ? 33 D2 48 8B 88 ? ? ? ? 48 8B 49 ? 48 83 C4 ? E9 ? ? ? ? CC CC CC CC CC CC CC CC CC CC CC CC CC 40 53 48 83 EC ? 8B D9 33 C9 E8 ? ? ? ? 80 3D ? ? ? ? ? 75 ? 8B 0D ? ? ? ? E8 ? ? ? ? C6 05 ? ? ? ? ? 48 8B 05 ? ? ? ? 45 33 C0 8B D3 48 8B 88 ? ? ? ? 48 8B 49 ? 48 83 C4 ? 5B E9 ? ? ? ? CC CC CC CC CC 48 83 EC ? 33 C9 E8 ? ? ? ? 80 3D ? ? ? ? ? 75 ? 8B 0D ? ? ? ? E8 ? ? ? ? C6 05 ? ? ? ? ? 48 8B 05 ? ? ? ? 33 D2 48 8B 88 ? ? ? ? 48 8B 49 ? 48 83 C4 ? E9 ? ? ? ? CC CC CC CC CC CC CC CC CC CC CC CC CC 48 83 EC";
            public static readonly string SteamDlcPurchaseDate = @"40 53 48 83 EC ? 8B D9 33 C9 E8 ? ? ? ? 80 3D ? ? ? ? ? 75 ? 8B 0D ? ? ? ? E8 ? ? ? ? C6 05 ? ? ? ? ? 48 8B 05 ? ? ? ? 45 33 C0 8B D3 48 8B 88 ? ? ? ? 48 8B 49 ? 48 83 C4 ? 5B E9 ? ? ? ? CC CC CC CC CC 40 55 53";
            public static readonly string IsSelectedAlbumUnlockedMaster = @"48 83 EC ? 80 3D ? ? ? ? ? 75 ? 8B 0D ? ? ? ? E8 ? ? ? ? C6 05 ? ? ? ? ? 48 8B 0D ? ? ? ? F6 81 ? ? ? ? ? 74 ? 83 B9 ? ? ? ? ? 75 ? E8 ? ? ? ? 33 C9 E8 ? ? ? ? 84 C0 0F 85";
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] buffer, long size, out long lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [In] byte[] buffer, long size, out long lpNumberOfBytesWritten);

        public static byte[] Read(IntPtr handle, IntPtr address, long size)
        {
            byte[] buffer = new byte[size];
            long bytesRead;

            ReadProcessMemory(handle, address, buffer, size, out bytesRead);

            if (bytesRead == 0)
                return null;

            return buffer;
        }

        private static IntPtr PatternScan(IntPtr handle, string pattern, IntPtr baseAddress, long scanSize)
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

            byte[] moduleBytes = Read(handle, baseAddress, scanSize);

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

        private static IntPtr PatternScan(Process process, string pattern, string moduleName)
        {
            foreach (ProcessModule module in process.Modules)
                if (module.ModuleName == moduleName)
                    return PatternScan(process.Handle, pattern, module.BaseAddress, module.ModuleMemorySize);
            return IntPtr.Zero;
        }

        public static long Write(IntPtr handle, IntPtr address, byte[] value)
        {
            WriteProcessMemory(handle, address, value, value.Length, out long bytesWritten);
            return bytesWritten;
        }

        private static bool[] patches = new bool[] { false, false, false };

        public static bool Exploit(Process museDash)
        {
            try
            {
                IntPtr dlcCheckAddr = PatternScan(museDash, Patterns.SteamDlcCheck, "GameAssembly.dll");
                IntPtr purchaseTimeAddr = PatternScan(museDash, Patterns.SteamDlcPurchaseDate, "GameAssembly.dll");
                IntPtr isMasterUnlockedAddress = PatternScan(museDash, Patterns.IsSelectedAlbumUnlockedMaster, "GameAssembly.dll");

                if (dlcCheckAddr != IntPtr.Zero && purchaseTimeAddr != IntPtr.Zero && isMasterUnlockedAddress != IntPtr.Zero)
                {
                    if (!patches[0] && Write(museDash.Handle, dlcCheckAddr, new byte[] {
                        0x48, 0xB8, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0xC3
                    }) <= 0)
                        return false;
                    else
                        patches[0] = true;

                    if (!patches[1] && Write(museDash.Handle, purchaseTimeAddr, new byte[] {
                        0xB8, 0x85, 0x47, 0xDE, 0x63,
                        0xC3
                    }) <= 0) 
                        return false;
                    else
                        patches[1] = true;

                    if (patches[2] && Write(museDash.Handle, isMasterUnlockedAddress, new byte[] {
                        0x48, 0xB8, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0xC3
                    }) <= 0) 
                        return false;
                    else
                        patches[2] = true;
                }
            }
            catch { return false; }
            return true;
        }
    }
}