using System;
using System.Runtime.InteropServices;

namespace PeartreeGames.Topiary.Unity
{
    
    /// <summary>
    /// Dialogue Line
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Line
    {
        private readonly StringBuffer _content;
        private readonly StringBuffer _speaker;
        private readonly IntPtr _tagsPtr;
        private readonly byte _tagsLen;

        /// <summary>
        /// The Speaker of the dialogue line
        /// </summary>
        public string Speaker => _speaker.Value; 
        /// <summary>
        /// The words spoken
        /// </summary>
        public string Content => _content.Value;
        
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
                    var str = Marshal.PtrToStructure<StringBuffer>(_tagsPtr + offset);
                    tags[i] = str.Value;
                    offset += Marshal.SizeOf<StringBuffer>();
                }
                return tags;
            }
        }
    }
}
