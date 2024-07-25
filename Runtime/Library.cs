using System;
using System.Runtime.InteropServices;

namespace PeartreeGames.Topiary.Unity
{
    public static class Library
    {
        public enum Severity : byte
        {
            Debug,
            Info,
            Warn,
            Error
        }

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr createVm(
            IntPtr bytesPtr,
            int bytesLength,
            IntPtr onLinePtr,
            IntPtr onChoicesPtr,
            IntPtr onValueChangedPtr,
            IntPtr logPtr,
            Severity severity
        );

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void destroyVm(IntPtr vmPtr);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void start(IntPtr vmPtr,
            [MarshalAs(UnmanagedType.LPStr)] string bough);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        public static extern int compile(
            [MarshalAs(UnmanagedType.LPStr)] string path,
            byte[] output,
            int capacity,
            IntPtr logPtr,
            Severity severity
        );

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        public static extern int calculateCompileSize(
            [MarshalAs(UnmanagedType.LPStr)] string path,
            IntPtr logPtr,
            Severity severity
        );

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void run(IntPtr vmPt);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void selectContinue(IntPtr vmPtr);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool canContinue(IntPtr vmPtr);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool isWaiting(IntPtr vmPtr);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void selectChoice(IntPtr vmPtr, int index);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool subscribe(IntPtr vmPtr,
            [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool unsubscribe(IntPtr vmPtr,
            [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void setExtern(IntPtr vmPtr,
            [MarshalAs(UnmanagedType.LPStr)] string name, TopiValue value, IntPtr freePtr);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void setExternFunc(
            IntPtr vmPtr,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            IntPtr funcPtr,
            byte arity,
            IntPtr freePtr);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        public static extern int calculateStateSize(IntPtr vmPtr);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern int saveState(IntPtr vmPtr, byte[] output, int capacity);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void loadState(IntPtr vmPtr,
            [MarshalAs(UnmanagedType.LPStr)] string json, int jsonLength);
    }
}