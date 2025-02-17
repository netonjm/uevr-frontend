﻿using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace UEVR {
    enum WM { USER = 0x400 }


    class SharedMemory {
        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct Data {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string path;

            [MarshalAs(UnmanagedType.I4)]
            public int pid;

            [MarshalAs(UnmanagedType.I4)]
            public int mainThreadId;

            [MarshalAs(UnmanagedType.I4)]
            public int commandThreadId;

            [MarshalAs(UnmanagedType.I1)]
            public bool signalFrontendConfigSetup;
        };

        public enum Command {
            ReloadConfig,
            ConfigSetupAcknowledged
        };

        public static string SharedMemoryName = "UnrealVRMod";
        public static int MessageIdentifier = BitConverter.ToInt32(Encoding.ASCII.GetBytes("VRMOD"), 0);

        public static bool Exists() {
            // Try to open the shared memory with read/write access
            try {
                using (MemoryMappedFile memory = MemoryMappedFile.OpenExisting(SharedMemoryName, MemoryMappedFileRights.ReadWrite)) {
                    return true;
                }
            } catch (Exception) {
            }

            return false;
        }

        public static MemoryMappedFile? GetMapping() {
            // Try to open the shared memory with read/write access
            try {
                return MemoryMappedFile.OpenExisting(SharedMemoryName, MemoryMappedFileRights.ReadWrite);
            } catch (Exception) {
            }

            return null;
        }

        public static Data? GetData() {
            using (var mapping = GetMapping()) {
                if (mapping != null) {
                    using (var accessor = mapping.CreateViewAccessor()) {
                        byte[] rawData = new byte[Marshal.SizeOf(typeof(Data))];
                        accessor.ReadArray(0, rawData, 0, rawData.Length);

                        var pinned = GCHandle.Alloc(rawData, GCHandleType.Pinned);
                        var data = Marshal.PtrToStructure<Data>(pinned.AddrOfPinnedObject());

                        pinned.Free();

                        return data;
                    }
                }
            }

            return null;
        }

        public static void SendCommand(Command command) {
            var data = GetData();
            if (data == null) {
                return;
            }

            var threadId = (data?.commandThreadId ?? 0);

            if (threadId == 0) {
                return;
            }

            PostThreadMessage((uint)threadId, (uint)WM.USER, (IntPtr)MessageIdentifier, (IntPtr)command);
        }
    };
}
