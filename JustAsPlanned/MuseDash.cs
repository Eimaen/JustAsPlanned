using MemoryHacking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public static bool Exploit(Process museDash)
        {
            try
            {
                Memory memory = museDash.InitializeMemory();
                IntPtr dlcCheckAddr = memory.PatternScan(Patterns.SteamDlcCheck, memory.GetModuleByName("GameAssembly.dll"));
                memory.Write(dlcCheckAddr, new byte[] {
                    0x48, 0xB8, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0xC3
                });
                IntPtr purchaseTimeAddr = memory.PatternScan(Patterns.SteamDlcPurchaseDate, memory.GetModuleByName("GameAssembly.dll"));
                memory.Write(purchaseTimeAddr, new byte[] {
                    0xB8, 0x85, 0x47, 0xDE, 0x63,
                    0xC3
                });
                IntPtr isMasterUnlockedAddress = memory.PatternScan(Patterns.IsSelectedAlbumUnlockedMaster, memory.GetModuleByName("GameAssembly.dll"));
                memory.Write(isMasterUnlockedAddress, new byte[] {
                    0x48, 0xB8, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0xC3
                });
            }
            catch { return false; }
            return true;
        }
    }
}