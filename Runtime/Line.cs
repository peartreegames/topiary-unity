using System;
using System.Runtime.InteropServices;
using IntPtr = System.IntPtr;

namespace PeartreeGames.Topiary.Unity
{
    
    /// <summary>
    /// Dialogue Line
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Line
    {
        private readonly IntPtr _contentPtr;
        [MarshalAs(UnmanagedType.U4)] private readonly int _contentLen;
        private readonly IntPtr _speakerPtr;
        [MarshalAs(UnmanagedType.U4)] private readonly int _speakerLen;
        private readonly IntPtr _tagsPtr;
        private readonly byte _tagsLen;
        
        /// <summary>
        /// The Speaker of the dialogue line
        /// </summary>
        public string Speaker => Library.PtrToUtf8String(_speakerPtr, _speakerLen);
        /// <summary>
        /// The words spoken
        /// </summary>
        public string Content => Library.PtrToUtf8String(_contentPtr, _contentLen);
        
        /// <summary>
        /// Array of tags
        /// </summary>
        public string[] Tags
        {
            get
            {
                if (_tagsLen == 0) return Array.Empty<string>();
                var offset = 0;
                var tags = new string[_tagsLen];
                for (var i = 0; i < _tagsLen; i++)
                {
                    var ptr = Marshal.ReadIntPtr(_tagsPtr, offset);
                    tags[i] = Library.PtrToUtf8String(ptr);
                    offset += IntPtr.Size;
                }
                return tags;
            }
        }
    }
}
