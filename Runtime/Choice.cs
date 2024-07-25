using System;
using System.Runtime.InteropServices;

namespace PeartreeGames.Topiary.Unity
{
    /// <summary>
    /// Represents a choice in a dialogue.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Choice
    {
        private readonly StringBuffer _content;
        private readonly IntPtr _tagsPtr;
        private readonly byte _tagsLen;

        [MarshalAs(UnmanagedType.U4)] private readonly int _visitCount;
        [MarshalAs(UnmanagedType.U4)] private readonly int _ip;

        /// <summary>
        /// Gets the visit count associated with the choice.
        /// </summary>
        /// <value>The visit count.</value>
        public int VisitCount => _visitCount;

        /// <summary>
        /// Gets the IP (Instruction Pointer) of the choice.
        /// </summary>
        /// <remarks>Mostly used internally, but exposed here as well</remarks>
        public int Ip => _ip;

        /// <summary>
        /// Represents a choice in a dialogue.
        /// </summary>
        public string Content => _content.Value;

        /// <summary>
        /// Gets the tags associated with the choice.
        /// </summary>
        /// <value>
        /// The tags associated with the choice.
        /// </value>
        /// <remarks>
        /// The tags are represented as an array of strings.
        /// </remarks>
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

        /// <summary>
        /// Marshals an <see cref="IntPtr"/> pointer to an array of <see cref="Choice"/> structures.
        /// </summary>
        /// <param name="choicePtr">The pointer to the array of <see cref="Choice"/> structures.</param>
        /// <param name="count">The number of <see cref="Choice"/> structures in the array.</param>
        /// <returns>An array of <see cref="Choice"/> structures.</returns>
        public static Choice[] MarshalPtr(IntPtr choicePtr, byte count)
        {
            var choices = new Choice[count];
            var ptr = choicePtr;
            for (var i = 0; i < count; i++)
            {
                choices[i] = Marshal.PtrToStructure<Choice>(ptr);
                ptr = IntPtr.Add(ptr, Marshal.SizeOf<Choice>());
            }

            return choices;
        }
    }
}
