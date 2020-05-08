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
        Process Roblox;
        int WrittenBytes = 0;
        double CurrentFps = 0.0166666666666667;
        IntPtr Threadscheduler;
        int DelayOffset;
        public  MemorySharp Sharp;
        Thread LoopThread;
        bool Init = false;
        #endregion

        public Form1()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            numericUpDown1.Value = 60;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            //if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0 && Process.GetProcessesByName("RobloxPlayerBeta").First() == Roblox)
            //    WriteMemory<double>(Roblox.Handle, Threadscheduler + DelayOffset, (double)(1.0 / 60)); // Set back to normal!
            
            LoopThread.Abort();
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            if (!Init)
            {
                var procs = Process.GetProcessesByName("RobloxPlayerBeta");
                if (procs.Length > 0)
                {
                    Roblox = procs.First();

                    Sharp = new MemorySharp(Roblox);

                    SigScanSharp Sigscan = new SigScanSharp(Roblox.Handle);
                    Sigscan.SelectModule(Roblox.MainModule);

                    IntPtr GetThreadScheduler = traceRelativeCall(Roblox.Handle, Sigscan.FindPattern("E8 ? ? ? ? 8A 4D 08 83 C0 04"));

                    Threadscheduler = new RemotePointer(Sharp, GetThreadScheduler).Execute<IntPtr>();
                    DelayOffset = FindTaskSchedulerFrameDelayOffset(Roblox.Handle, Threadscheduler);

                    LoopThread = new Thread(new ThreadStart(LoopWith60PS));
                    LoopThread.Start();
                    Init = true;
                }
                else
                {
                    MessageBox.Show("Roblox Doesnt Exist", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            CurrentFps = 1.0 / (double)(numericUpDown1.Value);
        }

        #region FPSUnlocker
        int FindTaskSchedulerFrameDelayOffset(IntPtr Handle, IntPtr scheduler) // Credits to austin
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

        long fpsStartTime, fpsFrameCount;
        private void LimitFPS(int fps) // Thanks google!
        {
            long freq = System.Diagnostics.Stopwatch.Frequency;
            long frame = System.Diagnostics.Stopwatch.GetTimestamp();
            while ((frame - fpsStartTime) * fps < freq * fpsFrameCount)
            {
                int sleepTime = (int)((fpsStartTime * fps + freq * fpsFrameCount - frame * fps) * 1000 / (freq * fps));
                if (sleepTime > 0) System.Threading.Thread.Sleep(sleepTime);
                frame = System.Diagnostics.Stopwatch.GetTimestamp();
            }
            if (++fpsFrameCount > fps)
            {
                fpsFrameCount = 0;
                fpsStartTime = frame;
            }
        }

        private void LoopWith60PS()
        {
            while (true)
            {
                LimitFPS(60); // Sync with roblox render time
                WriteMemory<double>(Roblox.Handle, Threadscheduler + DelayOffset, CurrentFps);
                label2.Text = "Current FPS: " + numericUpDown1.Value.ToString();
            }
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
