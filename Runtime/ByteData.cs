using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace PeartreeGames.Topiary.Unity
{
    /// <summary>
    /// Compiled topi bytecode plus the externs/boughs index extracted from the constants section.
    /// </summary>
    public class ByteData : ScriptableObject
    {
        /// <summary>
        /// Represents a byte array.
        /// </summary>
        public byte[] bytes;

        [SerializeField] private string[] externs;
        [SerializeField] private List<string> boughs;

        private HashSet<string> _externSet;

        // Lazily materialized from the serialized `externs` array. The cache is rebuilt
        // on first access in built players (where Parse never runs).
        public HashSet<string> Externs =>
            _externSet ??= new HashSet<string>(externs ?? Array.Empty<string>());

        public List<string> Boughs => boughs;

        // Mirrors topiary/src/types/value.zig `Type`.
        private enum ConstantTag : byte
        {
            Void = 0,
            Nil = 1,
            Bool = 2,
            Number = 3,
            Range = 4,
            Obj = 5,
            MapPair = 6,
            Visit = 7,
            EnumValue = 8,
            Timestamp = 9,
            ConstString = 10,
            Ref = 11,
        }

        // Mirrors topiary/src/types/value.zig `Obj.DataType`.
        private enum ObjectTag : byte
        {
            String = 0,
            Enum = 1,
            List = 2,
            Map = 3,
            Set = 4,
            Function = 5,
            Extern = 6,
            Builtin = 7,
            Class = 8,
            Instance = 9,
            Anchor = 10,
        }

        /// <summary>
        /// Reads externs and boughs from the bytecode constants section in a single pass.
        /// Editor-only path, called by <c>TopiScriptedImporter</c>.
        /// </summary>
        public void Parse(BinaryReader reader)
        {
            reader.BaseStream.Position = ReadConstantsOffset(reader);

            var count = reader.ReadUInt64();
            var externSet = new HashSet<string>();
            boughs = new List<string>();

            for (ulong i = 0; i < count; i++)
            {
                var type = (ConstantTag)reader.ReadByte();
                if (type != ConstantTag.Obj)
                {
                    SkipConstantValue(reader, type);
                    continue;
                }

                var objType = (ObjectTag)reader.ReadByte();
                reader.ReadBytes(17); // UUID

                switch (objType)
                {
                    case ObjectTag.Extern:
                    {
                        var nameLength = reader.ReadByte();
                        var externName = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
                        reader.ReadByte(); // arity
                        externSet.Add(externName);
                        break;
                    }
                    case ObjectTag.Anchor:
                    {
                        var nameLength = reader.ReadUInt16();
                        var anchorName = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
                        reader.ReadUInt32(); // ip
                        reader.ReadUInt32(); // visitGlobalsIndex
                        var hasParent = reader.ReadByte() == 1;
                        if (hasParent) reader.ReadUInt32();
                        boughs.Add(anchorName);
                        break;
                    }
                    default:
                        SkipObjectValue(reader, objType);
                        break;
                }
            }

            externs = new string[externSet.Count];
            externSet.CopyTo(externs);
            _externSet = externSet;
        }

        private const string Magic = "TPBC";
        private const ushort SupportedVersion = 3;

        private static long ReadConstantsOffset(BinaryReader reader)
        {
            reader.BaseStream.Position = 0;
            var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (magic != Magic)
                throw new InvalidDataException($"Invalid topi bytecode magic: '{magic}' (expected '{Magic}').");
            var version = reader.ReadUInt16();
            if (version != SupportedVersion)
                throw new InvalidDataException($"Unsupported topi bytecode version {version} (expected {SupportedVersion}).");
            reader.ReadUInt16(); // reserved
            reader.ReadUInt16(); // locals_count
            reader.ReadUInt64(); // skip Globals offset
            return (long)reader.ReadUInt64(); // Constants offset
        }

        private static void SkipConstantValue(BinaryReader reader, ConstantTag type)
        {
            switch (type)
            {
                case ConstantTag.Void:
                case ConstantTag.Nil:
                    break;
                case ConstantTag.Bool:
                    reader.ReadByte();
                    break;
                case ConstantTag.Number:
                {
                    var length = reader.ReadByte();
                    reader.ReadBytes(length);
                    break;
                }
                case ConstantTag.Range:
                    reader.ReadInt32(); // Start
                    reader.ReadInt32(); // End
                    break;
                case ConstantTag.MapPair:
                {
                    SkipNested(reader);
                    SkipNested(reader);
                    break;
                }
                case ConstantTag.Visit:
                    reader.ReadUInt32();
                    break;
                case ConstantTag.EnumValue:
                {
                    var length = reader.ReadByte();
                    reader.ReadBytes(length);
                    break;
                }
                case ConstantTag.Timestamp:
                    reader.ReadInt64();
                    break;
                case ConstantTag.ConstString:
                {
                    var length = reader.ReadUInt16();
                    reader.ReadBytes(length);
                    break;
                }
                case ConstantTag.Ref:
                    // Zig serialize errors on this tag, so it cannot appear as a top-level
                    // constant. Hitting it means the bytecode format has drifted; silently
                    // skipping would corrupt the rest of the section.
                    throw new InvalidDataException(
                        "Unexpected 'ref' constant tag in constants section.");
                default:
                    throw new InvalidDataException(
                        $"Unknown constant tag {(byte)type} in constants section.");
            }
        }

        // Reads a constant tag and either recurses into SkipConstantValue or unwraps an
        // Obj header (objType + UUID) before delegating to SkipObjectValue.
        private static void SkipNested(BinaryReader reader)
        {
            var type = (ConstantTag)reader.ReadByte();
            if (type == ConstantTag.Obj)
            {
                var objType = (ObjectTag)reader.ReadByte();
                reader.ReadBytes(17);
                SkipObjectValue(reader, objType);
            }
            else
            {
                SkipConstantValue(reader, type);
            }
        }

        private static void SkipObjectValue(BinaryReader reader, ObjectTag objType)
        {
            switch (objType)
            {
                case ObjectTag.String:
                {
                    var length = reader.ReadUInt16();
                    reader.ReadBytes(length);
                    var segmentCount = reader.ReadByte();
                    for (var i = 0; i < segmentCount; i++)
                    {
                        var tag = reader.ReadByte();
                        if (tag == 0) reader.ReadBytes(4); // literal: u16 start + u16 end
                        else reader.ReadBytes(1);          // interp: u8 index
                    }
                    break;
                }
                case ObjectTag.Enum:
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
                case ObjectTag.List:
                case ObjectTag.Map:
                case ObjectTag.Set:
                    // Non-constants
                    break;
                case ObjectTag.Function:
                {
                    var nameLength = reader.ReadUInt16();
                    reader.ReadBytes(nameLength);
                    reader.ReadByte(); // Arity
                    reader.ReadByte(); // Is Method
                    reader.ReadUInt16(); // Locals Count
                    var instCount = reader.ReadUInt32();
                    reader.ReadBytes((int)instCount);
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
                case ObjectTag.Extern:
                {
                    var length = reader.ReadByte();
                    reader.ReadBytes(length);
                    reader.ReadByte(); // Arity
                    break;
                }
                case ObjectTag.Builtin:
                {
                    var length = reader.ReadByte();
                    reader.ReadBytes(length);
                    break;
                }
                case ObjectTag.Class:
                {
                    var nameLength = reader.ReadByte();
                    reader.ReadBytes(nameLength);
                    var fieldCount = reader.ReadByte();
                    for (var i = 0; i < fieldCount; i++) SkipMember(reader);
                    var methodCount = reader.ReadByte();
                    for (var i = 0; i < methodCount; i++) SkipMember(reader);
                    break;
                }
                case ObjectTag.Instance:
                    break;
                case ObjectTag.Anchor:
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
            SkipNested(reader);
        }
    }
}
