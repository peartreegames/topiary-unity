using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

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

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnLineDelegate(IntPtr vmPtr, Line line);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnChoicesDelegate(IntPtr vmPtr, IntPtr choicePtr, byte length);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OutputLogDelegate(IntPtr msgPtr, Severity severity);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate TopiValue ExternFunctionDelegate(IntPtr argPtr, byte length);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SubscriberDelegate(string name, ref TopiValue value);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr createVm(IntPtr bytesPtr, int sourceLength,
            IntPtr onLinePtr,
            IntPtr onChoicesPtr);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void destroyVm(IntPtr vmPtr);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void start(IntPtr vmPtr, string bough, int boughLength);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        public static extern int compile(string path, int pathLength, byte[] output,
            int capacity);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        public static extern int calculateCompileSize(string path, int pathLength);

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
        public static extern bool tryGetValue(IntPtr vmPtr, string name, int nameLength,
            out TopiValue value);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool destroyValue(ref TopiValue value);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool subscribe(IntPtr vmPtr, string name, int nameLength);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool unsubscribe(IntPtr vmPtr, string name, int nameLength);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void setSubscriberCallback(IntPtr vmPtr, IntPtr callback);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void setExternNumber(IntPtr vmPtr, string name, int nameLength,
            float value);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void setExternString(IntPtr vmPtr, string name, int nameLength,
            string value,
            int valueLength);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void setExternBool(IntPtr vmPtr, string name, int nameLength,
            bool value);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void setExternEnum(IntPtr vmPtr, string name, int nameLength,
            string enumName, int enumNameLength, string enumValue, int enumValueLength);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void setExternNil(IntPtr vmPtr, string name, int nameLength);
        
        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void setExternFunc(IntPtr vmPtr, string name, int nameLength,
            IntPtr funcPtr,
            byte arity);
        
        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U4)]
        public static extern int calculateStateSize(IntPtr vmPtr);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern int saveState(IntPtr vmPtr, byte[] output, int capacity);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void loadState(IntPtr vmPtr, string json, int jsonLength);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void setDebugLog(IntPtr logPtr);

        [DllImport("topi", CallingConvention = CallingConvention.Cdecl)]
        public static extern void setDebugSeverity(Severity severity);


        public static string PtrToUtf8String(IntPtr pointer, int? count = null)
        {
            if (count == 0) return string.Empty;
            if (count > 0)
            {
                var len = count.Value;
                var bytes = new byte[len];
                Marshal.Copy(pointer, bytes, 0, len);
                return Encoding.UTF8.GetString(bytes).TrimEnd('\u0000');
            }

            var byteList = new List<byte>(64);
            byte readByte;
            var offset = 0;
            do
            {
                readByte = Marshal.ReadByte(pointer, offset);
                if (readByte != 0) byteList.Add(readByte);
                offset++;
            } while (readByte != 0);

            return Encoding.UTF8.GetString(byteList.ToArray());
        }
    }
}