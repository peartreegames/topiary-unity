using System;
using System.Runtime.InteropServices;

namespace PeartreeGames.Topiary.Unity
{
    public readonly struct Line
    {
        public string Speaker { get; }
        public string Content { get; }
        public string[] Tags { get; }

        public Line(string speaker, string content, string[] tags)
        {
            Speaker = speaker;
            Content = content;
            Tags = tags ?? Array.Empty<string>();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct LineNative
    {
        private readonly StringBuffer _content;
        private readonly StringBuffer _speaker;
        private readonly IntPtr _tagsPtr;
        private readonly byte _tagsLen;

        public Line ToManaged()
        {
            string[] tags;
            if (_tagsLen == 0)
            {
                tags = Array.Empty<string>();
            }
            else
            {
                tags = new string[_tagsLen];
                var ptr = _tagsPtr;
                for (var i = 0; i < _tagsLen; i++)
                {
                    tags[i] = Marshal.PtrToStructure<StringBuffer>(ptr).Value;
                    ptr = IntPtr.Add(ptr, StringBuffer.Stride);
                }
            }
            return new Line(_speaker.Value, _content.Value, tags);
        }
    }
}
