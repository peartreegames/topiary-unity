using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PeartreeGames.Topiary.Unity
{
    /// <summary>
    /// Represents a class that holds byte data and a set of external references.
    /// </summary>
    public class ByteData : ScriptableObject, ISerializationCallbackReceiver
    {
        /// <summary>
        /// Represents a byte array.
        /// </summary>
        public byte[] bytes;

        /// <summary>
        /// Represents a byte data object that can be serialized and deserialized.
        /// </summary>
        [SerializeField] private string[] externs;

        public SortedSet<string> Externs { get; private set; }
        public void OnBeforeSerialize() => externs = Externs?.ToArray();

        public void OnAfterDeserialize() => Externs = new SortedSet<string>(externs);

        /// <summary>
        /// Retrieves a sorted set of extern names from the given binary reader.
        /// </summary>
        /// <param name="reader">The binary reader from which to read the extern names.</param>
        public void SetExterns(BinaryReader reader)
        {
            var globalsStart= reader.ReadUInt64();
            reader.BaseStream.Position = (long)globalsStart;
            var globalsCount = reader.ReadUInt64();
            Externs = new SortedSet<string>();
            for (ulong i = 0; i < globalsCount; i++)
            {
                var nameLength = reader.ReadByte();
                var nameValue = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
                reader.ReadUInt32(); // skip globals index
                var isExtern = reader.ReadByte() == 1;
                reader.ReadByte(); // skip mutable
                if (isExtern) Externs.Add(nameValue);
            }
        }

        public static string[] GetBoughs(BinaryReader reader)
        {
            reader.ReadUInt64(); // skip globals
            var boughsPos = reader.ReadUInt64();
            reader.BaseStream.Position = (long)boughsPos;
            
            var boughCount = reader.ReadUInt64();
            var result = new string[boughCount];
            for (ulong i = 0; i < boughCount; i++)
            {
                var nameLength = reader.ReadUInt16();
                var name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
                reader.ReadUInt32(); // skip index
                result[i] = name;
            }
            
            return result;
        }
    }
}