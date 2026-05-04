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
    public class ByteData : ScriptableObject
    {
        /// <summary>
        /// Represents a byte array.
        /// </summary>
        public byte[] bytes;

        /// <summary>
        /// Represents a byte data object that can be serialized and deserialized.
        /// </summary>
        [SerializeField] private string[] externs;

        [SerializeField] private List<string> boughs;

        public SortedSet<string> Externs { get; private set; }
        public List<string> Boughs => boughs;
        public void OnBeforeSerialize() => externs = Externs?.ToArray();
        public void OnAfterDeserialize() => Externs = new SortedSet<string>(externs);

        /// <summary>
        /// Retrieves a sorted set of extern names from the given binary reader.
        /// </summary>
        /// <param name="reader">The binary reader from which to read the extern names.</param>
        public void SetExterns(BinaryReader reader)
        {
            reader.BaseStream.Position = 0;
            reader.ReadUInt64(); // Skip Globals section pointer
            var constantsPos = reader.ReadUInt64();
            reader.BaseStream.Position = (long)constantsPos;

            var count = reader.ReadUInt64();
            Externs = new SortedSet<string>();
            for (ulong i = 0; i < count; i++)
            {
                var type = reader.ReadByte();
                if (type == 5) // Object
                {
                    var objType = reader.ReadByte();
                    reader.ReadBytes(17); // Skip UUID
                    if (objType == 6) // ExternFunction
                    {
                        var nameLength = reader.ReadByte();
                        var externName = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
                        reader.ReadByte(); // Arity
                        Externs.Add(externName);
                    }
                    else SkipObjectValue(reader, objType);
                }
                else SkipConstantValue(reader, type);
            }
        }

        public void SetBoughs(BinaryReader reader)
        {
            reader.BaseStream.Position = 0;
            reader.ReadUInt64(); // Skip Globals section pointer
            var constantsPos = reader.ReadUInt64();
            reader.BaseStream.Position = (long)constantsPos;

            var count = reader.ReadUInt64();
            boughs = new List<string>(); 
            for (ulong i = 0; i < count; i++)
            {
                var type = reader.ReadByte();
                if (type == 5) // Object
                {
                    var objType = reader.ReadByte();
                    reader.ReadBytes(17); // Skip UUID
                    if (objType == 10) // Anchor
                    {
                        var nameLength = reader.ReadUInt16();
                        var anchorName = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
                        reader.ReadUInt32(); // Ip
                        reader.ReadUInt32(); // VisitGlobalsIndex
                        var hasParent = reader.ReadByte() == 1;
                        if (hasParent) reader.ReadUInt32();
                        boughs.Add(anchorName);
                    }
                    else SkipObjectValue(reader, objType);
                }
                else SkipConstantValue(reader, type);
            }

        }

        private static void SkipConstantValue(BinaryReader reader, byte type)
        {
            switch (type)
            {
                case 0: // void
                case 1: // nil
                    break;
                case 2: // bool
                    reader.ReadByte();
                    break;
                case 3: // number
                {
                    var length = reader.ReadByte();
                    reader.ReadBytes(length);
                    break;
                }
                case 4: // range
                    reader.ReadInt32(); // Start
                    reader.ReadInt32(); // End
                    break;
                case 6: // map_pair
                {
                    var keyType = reader.ReadByte();
                    if (keyType == 5)
                    {
                        var objType = reader.ReadByte();
                        reader.ReadBytes(17);
                        SkipObjectValue(reader, objType);
                    }
                    else
                    {
                        SkipConstantValue(reader, keyType);
                    }

                    var valueType = reader.ReadByte();
                    if (valueType == 5)
                    {
                        var objType = reader.ReadByte();
                        reader.ReadBytes(17);
                        SkipObjectValue(reader, objType);
                    }
                    else
                    {
                        SkipConstantValue(reader, valueType);
                    }

                    break;
                }
                case 7: // visit
                    reader.ReadUInt32();
                    break;
                case 8: // enum_value
                {
                    var length = reader.ReadByte();
                    reader.ReadBytes(length);
                    break;
                }
                case 9: // timestamp
                    reader.ReadInt64();
                    break;
                case 10: // const_string
                {
                    var length = reader.ReadUInt16();
                    reader.ReadBytes(length);
                    break;
                }
                case 11: // ref
                    // Not detailed in ValueType details, but let's assume it's like a pointer/index (u32/u64?)
                    // The spec doesn't say. I'll ignore for now or assume u32 if I had to guess.
                    break;
            }
        }

        private static void SkipObjectValue(BinaryReader reader, byte objType)
        {
            switch (objType)
            {
                case 0: // string
                {
                    var length = reader.ReadUInt16();
                    reader.ReadBytes(length);
                    break;
                }
                case 1: // enum
                {
                    var nameLength = reader.ReadByte();
                    reader.ReadBytes(nameLength);
                    reader.ReadByte(); // Is Sequence
                    var count = reader.ReadByte();
                    for (var i = 0; i < count; i++)
                    {
                        var len = reader.ReadByte();
                        reader.ReadBytes(len);
                    }

                    break;
                }
                case 2: // list
                case 3: // map
                case 4: // set
                    // Non-constants
                    break;
                case 5: // function
                {
                    reader.ReadByte(); // Arity
                    reader.ReadByte(); // Is Method
                    reader.ReadUInt16(); // Locals Count
                    var instCount = reader.ReadUInt16();
                    reader.ReadBytes(instCount);
                    var debugCount = reader.ReadUInt32();
                    for (uint i = 0; i < debugCount; i++)
                    {
                        var len = reader.ReadUInt16();
                        reader.ReadBytes(len);
                        var rangeCount = reader.ReadUInt16();
                        reader.BaseStream.Position += rangeCount * 12; // Range is 3 * u32 = 12 bytes
                    }

                    break;
                }
                case 6: // extern
                {
                    var length = reader.ReadByte();
                    reader.ReadBytes(length);
                    reader.ReadByte(); // Arity
                    break;
                }
                case 7: // builtin
                {
                    var length = reader.ReadByte();
                    reader.ReadBytes(length);
                    break;
                }
                case 8: // class
                {
                    var nameLength = reader.ReadByte();
                    reader.ReadBytes(nameLength);
                    var fieldCount = reader.ReadByte();
                    for (var i = 0; i < fieldCount; i++) SkipMember(reader);
                    var methodCount = reader.ReadByte();
                    for (var i = 0; i < methodCount; i++) SkipMember(reader);
                    break;
                }
                case 9: // instance
                    break;
                case 10: // anchor
                {
                    var length = reader.ReadUInt16();
                    reader.ReadBytes(length);
                    reader.ReadUInt32(); // Ip
                    reader.ReadUInt32(); // VisitGlobalsIndex
                    var hasParent = reader.ReadByte() == 1;
                    if (hasParent) reader.ReadUInt32();
                    break;
                }
            }
        }

        private static void SkipMember(BinaryReader reader)
        {
            var length = reader.ReadByte();
            reader.ReadBytes(length);
            var type = reader.ReadByte();
            if (type == 5)
            {
                var objType = reader.ReadByte();
                reader.ReadBytes(17);
                SkipObjectValue(reader, objType);
            }
            else
            {
                SkipConstantValue(reader, type);
            }
        }
    }
}