using System;
using System.Runtime.InteropServices;

namespace PeartreeGames.Topiary.Unity
{
    public static class Delegates
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnLineDelegate(IntPtr vmPtr, IntPtr linePtr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnChoicesDelegate(IntPtr vmPtr, IntPtr choicePtr, byte length);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OutputLogDelegate(StringBuffer msg, Library.Severity severity);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate TopiValue ExternFunctionDelegate(IntPtr vmPtr, IntPtr userData, IntPtr argPtr, byte length);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SubscriberDelegate(IntPtr vmPtr, IntPtr namePtr, UIntPtr nameLen, TopiValue value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FreeDelegate(IntPtr ptr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void PanicHandlerDelegate(IntPtr msgPtr, UIntPtr msgLen);
    }
}