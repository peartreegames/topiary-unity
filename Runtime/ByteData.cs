using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

        /// of strings, while the `OnAfterDeserialize` method recreates the `ExternsSet` from the deserialized array.
        public SortedSet<string> ExternsSet;

        public void OnBeforeSerialize() => externs = ExternsSet?.ToArray();

        public void OnAfterDeserialize() => ExternsSet = new SortedSet<string>(externs);

        /// <summary>
        /// Retrieves a sorted set of external names from the given binary reader.
        /// </summary>
        /// <param name="reader">The binary reader from which to read the external names.</param>
        /// <returns>A sorted set of external names.</returns>
        public static SortedSet<string> GetExterns(BinaryReader reader)
        {
            var globalSymbolsCount = reader.ReadUInt64();
            var result = new SortedSet<string>();
            var indexSize = Marshal.SizeOf<uint>();
            for (ulong i = 0; i < globalSymbolsCount; i++)
            {
                var nameLength = reader.ReadByte();
                var name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
                reader.ReadBytes(indexSize); // skip globals index
                var isExtern = reader.ReadByte() == 1;
                _ = reader.ReadByte() == 1; // mutable
                if (isExtern) result.Add(name);
            }

            return result;
        }

        public static string[] GetBoughs(BinaryReader reader)
        {
            var globalSymbolsCount = reader.ReadUInt64();
            var indexSize = Marshal.SizeOf<uint>();

            for (ulong i = 0; i < globalSymbolsCount; i++)
            {
                var nameLength = reader.ReadByte();
                reader.ReadBytes(nameLength); // skip name
                reader.ReadBytes(indexSize); // skip globals index
                reader.ReadByte();
                reader.ReadByte(); // mutable
            }

            var boughCount = reader.ReadUInt64();
            var result = new string[boughCount];
            for (ulong i = 0; i < boughCount; i++)
            {
                var nameLength = reader.ReadByte();
                var name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
                reader.ReadBytes(indexSize); // skip index
                result[i] = name;
            }

            return result;
        }
    }
}