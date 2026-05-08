using System;
using System.Runtime.InteropServices;

namespace PeartreeGames.Topiary.Unity
{
    public readonly struct Choice
    {
        public string Content { get; }

        public string[] Tags { get; }

        public int VisitCount { get; }

        internal int Ip { get; }

        public Choice(string content, string[] tags, int visitCount, int ip)
        {
            Content = content;
            Tags = tags ?? Array.Empty<string>();
            VisitCount = visitCount;
            Ip = ip;
        }

        public static Choice[] MarshalPtr(IntPtr choicePtr, byte count)
        {
            if (count == 0) return Array.Empty<Choice>();
            var choices = new Choice[count];
            var ptr = choicePtr;
            for (var i = 0; i < count; i++)
            {
                choices[i] = Marshal.PtrToStructure<ChoiceNative>(ptr).ToManaged();
                ptr = IntPtr.Add(ptr, ChoiceNative.Stride);
            }
            return choices;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct ChoiceNative
    {
        private readonly StringBuffer _content;
        private readonly IntPtr _tagsPtr;
        private readonly byte _tagsLen;

        [MarshalAs(UnmanagedType.U4)] private readonly int _visitCount;
        [MarshalAs(UnmanagedType.U4)] private readonly int _ip;

        internal static readonly int Stride = Marshal.SizeOf<ChoiceNative>();

        public Choice ToManaged()
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
            return new Choice(_content.Value, tags, _visitCount, _ip);
        }
    }
}
