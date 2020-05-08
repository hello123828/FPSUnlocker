using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Binarysharp.MemoryManagement;
using Binarysharp.MemoryManagement.Memory;

namespace FPSUnlocker
{
    public partial class Form1 : Form
    {
        #region Defines
        Process Roblox = null;
        int WrittenBytes, CurrentPID = 0;
        double CurrentFps = 0.0166666666666667;
        IntPtr Taskscheduler;
        int DelayOffset;
        public  MemorySharp Sharp;
        Thread WatchThread = null;
        bool Init = false;
        #endregion

        public Form1()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            numericUpDown1.Value = 60;

            WatchThread = new Thread(new ThreadStart(WatchProcess));
            WatchThread.Start();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0 && Process.GetProcessesByName("RobloxPlayerBeta").First().Id == CurrentPID)
                WriteMemory<double>(Roblox.Handle, Taskscheduler + DelayOffset, (double)(1.0 / 60)); // Set back to normal!

            if (WatchThread != null)
                WatchThread.Abort();
        }

        private void WatchProcess()
        {
            while (true)
            {
                var procs = Process.GetProcessesByName("RobloxPlayerBeta");
                if (procs.Length > 0 && procs.First().Id != CurrentPID)
                {
                    Roblox = procs.First();
                    CurrentPID = Roblox.Id;

                    Thread.Sleep(2000); // Delay

                    Sharp = new MemorySharp(Roblox);

                    SigScanSharp Sigscan = new SigScanSharp(Roblox.Handle);
                    Sigscan.SelectModule(Roblox.MainModule);

                    IntPtr GetTaskscheduler = traceRelativeCall(Roblox.Handle, Sigscan.FindPattern("E8 ? ? ? ? 8A 4D 08 83 C0 04"));

                    Taskscheduler = new RemotePointer(Sharp, GetTaskscheduler).Execute<IntPtr>();
                    DelayOffset = FindTaskSchedulerFrameDelayOffset(Roblox.Handle, Taskscheduler);
                }
                Thread.Sleep(500);
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            CurrentFps = 1.0 / (double)(numericUpDown1.Value);
            if (Roblox != null)
                WriteMemory<double>(Roblox.Handle, Taskscheduler + DelayOffset, CurrentFps);
        }

        #region FPSUnlocker
        int FindTaskSchedulerFrameDelayOffset(IntPtr Handle, IntPtr scheduler) // Thanks to austin
        {
            for (int i = 0x100; i < 0x200; i += 4)
            {
                const double frame_delay = 1.0 / 60.0;
                double difference = ReadMemory<double>(Handle, scheduler + i) - frame_delay;
                difference = difference < 0 ? -difference : difference;
                if (difference < 0.004) return i;
            }
            return 0;
        }
        #endregion


        #region Memory
        private IntPtr traceRelativeCall(IntPtr Handle, IntPtr call)
        {
            return IntPtr.Add(ReadMemory<IntPtr>(Handle, call + 1), (call.ToInt32() + 5));
        }

        public IntPtr Base(int Address)
        {
            return (IntPtr)((Address - 0x400000) + Roblox.MainModule.BaseAddress.ToInt32());
        }


        public void WriteMemory<T>(IntPtr Handle, IntPtr Address, object Value)
        {
            var buffer = StructureToByteArray(Value);

            WriteProcessMemory(Handle, Address, buffer, (uint)buffer.Length, ref WrittenBytes);
        }

        public T ReadMemory<T>(IntPtr Handle, IntPtr address) where T : struct
        {
            var ByteSize = Marshal.SizeOf(typeof(T));

            var buffer = new byte[ByteSize];

            ReadProcessMemory(Handle, address, buffer, buffer.Length, ref WrittenBytes);

            return ByteArrayToStructure<T>(buffer);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, ref int lpNumberOfBytesWritten);


        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);


        private static byte[] StructureToByteArray(object obj)
        {
            var length = Marshal.SizeOf(obj);

            var array = new byte[length];

            var pointer = Marshal.AllocHGlobal(length);

            Marshal.StructureToPtr(obj, pointer, true);
            Marshal.Copy(pointer, array, 0, length);
            Marshal.FreeHGlobal(pointer);

            return array;
        }

      

        private static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
        }

        #endregion
    }
}
