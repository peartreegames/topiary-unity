using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace PeartreeGames.Topiary.Unity
{
    /// <summary>
    /// Topiary Value container
    /// Data is overlapped in memory so ensure correct value is used
    /// or check tag if unknown
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TopiValue : IDisposable, IEquatable<TopiValue>
    {
        [MarshalAs(UnmanagedType.U1)] public Tag tag;
        private TopiValueData _data;

        /// <summary>
        /// Converts a pointer to a TopiValue struct.
        /// </summary>
        /// <param name="ptr">The pointer to the TopiValue struct.</param>
        /// <returns>The converted TopiValue.</returns>
        public static TopiValue FromPtr(IntPtr ptr) => Marshal.PtrToStructure<TopiValue>(ptr);

        public TopiValue(bool b)
        {
            tag = Tag.Bool;
            _data = new TopiValueData
            {
                boolValue = (byte)(b ? 1 : 0)
            };
        }

        public TopiValue(int i)
        {
            tag = Tag.Number;
            _data = new TopiValueData
            {
                numberValue = i
            };
        }

        public TopiValue(float i)
        {
            tag = Tag.Number;
            _data = new TopiValueData
            {
                numberValue = i
            };
        }

        public TopiValue(string s)
        {
            tag = Tag.String;
            _data = new TopiValueData
            {
                stringValue = Marshal.StringToHGlobalAnsi(s)
            };
        }

        public TopiValue(string enumName, string enumValue)
        {
            tag = Tag.Enum;
            _data = new TopiValueData
            {
                enumValue = new TopiEnum
                {
                    namePtr = Marshal.StringToHGlobalAnsi(enumName),
                    valuePtr = Marshal.StringToHGlobalAnsi(enumValue)
                }
            };
        }

        /// <summary>
        /// Represents the different types of tags for the TopiValue struct.
        /// </summary>
        public enum Tag : byte
        {
            /// <summary>
            /// Represents the Nil tag of the <see cref="Tag"/> enum.
            /// </summary>
            Nil,

            /// <summary>
            /// Represents a boolean value in the Topiary framework.
            /// </summary>
            Bool,

            /// <summary>
            /// Represents a numeric value in the TopiValue enum.
            /// </summary>
            Number,

            /// <summary>
            /// Represents a string value in the <see cref="TopiValue"/> enum.
            /// </summary>
            String,

            /// <summary>
            /// Represents the different tags for the TopiValue enum.
            /// </summary>
            List,

            /// <summary>
            /// Represents a member of the enum Tag that signifies a Set type.
            /// </summary>
            Set,

            /// <summary>
            /// Represents a member of the enum Tag with the value Map.
            /// </summary>
            Map,

            /// <summary>
            /// Represents a member of the enum Tag with the value Enum.
            /// </summary>
            Enum,
        }


        public bool Bool => tag == Tag.Bool
            ? _data.boolValue == 1
            : throw new InvalidOperationException($"Value {tag} cannot be used as bool");

        public int Int => tag == Tag.Number
            ? Convert.ToInt32(_data.numberValue)
            : throw new InvalidOperationException($"Value {tag} cannot be used as int");

        public float Float => tag == Tag.Number
            ? _data.numberValue
            : throw new InvalidOperationException($"Value {tag} cannot be used as float");

        public string String => tag == Tag.String
            ? Library.PtrToUtf8String(_data.stringValue)
            : throw new InvalidOperationException($"Value {tag} cannot be used as string");

        public TopiValue[] List => tag == Tag.List
            ? _data.listValue.List
            : throw new InvalidOperationException($"Value {tag} cannot be used as list");

        public HashSet<TopiValue> Set => tag == Tag.Set
            ? _data.listValue.Set
            : throw new InvalidOperationException($"Value {tag} cannot be used as set");

        public Dictionary<TopiValue, TopiValue> Map => tag == Tag.Map
            ? _data.listValue.Map
            : throw new InvalidOperationException($"Value {tag} cannot be used as set");

        public TopiEnum Enum => tag == Tag.Enum
            ? _data.enumValue
            : throw new InvalidOperationException($"Value {tag} cannot be used as enum");

        // Will create boxing, better to use the above is value type is known
        public object Value => tag switch
        {
            Tag.Bool => _data.boolValue == 1,
            Tag.Number => _data.numberValue,
            Tag.String => Library.PtrToUtf8String(_data.stringValue),
            Tag.List => _data.listValue.List,
            Tag.Set => _data.listValue.Set,
            Tag.Map => _data.listValue.Map,
            Tag.Enum => _data.enumValue,
            _ => null
        };

        /// <summary>
        /// Gets the value of the TopiValue as the specified type.
        /// Will create boxing, better to use the above is value type is known
        /// </summary>
        /// <typeparam name="T">The type to convert the value to.</typeparam>
        /// <returns>The value of the TopiValue as type T.</returns>
        /// <remarks>
        /// This method converts the underlying value of the TopiValue to the specified type T.
        /// If the conversion is not possible, an InvalidCastException will be thrown.
        /// </remarks>
        public T AsType<T>() => (T)Convert.ChangeType(Value, typeof(T));

        /// <summary>
        /// Converts the TopiValue object to its string representation.
        /// </summary>
        /// <returns>The string representation of the TopiValue object.</returns>
        public override string ToString() =>
            tag switch
            {
                Tag.Bool => _data.boolValue == 1 ? "True" : "False",
                Tag.Number => _data.numberValue.ToString(CultureInfo.CurrentCulture),
                Tag.String => Library.PtrToUtf8String(_data.stringValue),
                Tag.List => $"List{{{string.Join(", ", _data.listValue.List)}}}",
                Tag.Set => $"Set{{{string.Join(", ", _data.listValue.Set)}}}",
                Tag.Map => $"Map{{{string.Join(", ", _data.listValue.Map)}}}",
                Tag.Enum => $"{_data.enumValue.Name}.{_data.enumValue.Value}",
                _ => $"{tag}: null"
            } ?? throw new InvalidOperationException();

        /// <summary>
        /// Releases the resources used by the TopiValue.
        /// </summary>
        public void Dispose()
        {
            Library.destroyValue(ref this);
        }

        /// <summary>
        /// Determines whether the current instance is equal to the specified object.
        /// </summary>
        /// <param name="other">The object to compare with the current instance.</param>
        /// <returns>True if the current instance is equal to the specified object; otherwise, false.</returns>
        public bool Equals(TopiValue other)
        {
            if (tag != other.tag) return false;
            switch (tag)
            {
                case Tag.Bool:
                    return _data.boolValue == other._data.boolValue;
                case Tag.Number:
                    return Math.Abs(_data.numberValue - other._data.numberValue) < 0.0001f;
                case Tag.String:
                    return _data.stringValue == other._data.stringValue;
                case Tag.List:
                case Tag.Set:
                case Tag.Map:
                    return _data.listValue.Equals(other._data.listValue);
                case Tag.Enum:
                    return _data.enumValue.Name.Equals(other._data.enumValue.Name) &&
                           _data.enumValue.Value.Equals(other._data.enumValue.Value);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Determines whether the current instance is equal to another TopiValue object.
        /// </summary>
        /// <param name="obj">The TopiValue object to compare with the current instance.</param>
        /// <returns>true if the current instance is equal to the other object; otherwise, false.</returns>
        public override bool Equals(object obj) => obj is TopiValue other && Equals(other);

        /// <summary>
        /// Gets the hash code of the TopiValue object.
        /// </summary>
        /// <returns>The hash code of the TopiValue object.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)tag * 397) ^ tag switch
                {
                    Tag.Nil => 0,
                    Tag.Bool => _data.boolValue.GetHashCode(),
                    Tag.Number => _data.numberValue.GetHashCode(),
                    Tag.String => _data.stringValue.GetHashCode(),
                    Tag.List => _data.listValue.GetHashCode(),
                    Tag.Set => _data.listValue.GetHashCode(),
                    Tag.Map => _data.listValue.GetHashCode(),
                    Tag.Enum => _data.enumValue.GetHashCode(),
                    _ => -1
                };
            }
        }

        /// <summary>
        /// Creates an array of TopiValue objects from the given IntPtr and count.
        /// </summary>
        /// <param name="argPtr">The IntPtr pointing to the start of the memory block containing the TopiValues.</param>
        /// <param name="count">The number of TopiValues to create.</param>
        /// <returns>An array of TopiValue objects.</returns>
        public static TopiValue[] CreateArgs(IntPtr argPtr, byte count)
        {
            var args = new TopiValue[count];
            var ptr = argPtr;
            for (var i = 0; i < count; i++)
            {
                args[i] = FromPtr(ptr);
                ptr = IntPtr.Add(ptr, Marshal.SizeOf<TopiValue>());
            }

            return args;
        }

        public static bool operator ==(TopiValue left, TopiValue right) => left.Equals(right);
        public static bool operator !=(TopiValue left, TopiValue right) => !(left == right);
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct TopiValueData
    {
        [FieldOffset(0)] [MarshalAs(UnmanagedType.I1)]
        public byte boolValue;

        [FieldOffset(0)] [MarshalAs(UnmanagedType.R4)]
        public float numberValue;

        [FieldOffset(0)] public IntPtr stringValue;

        [FieldOffset(0)] public TopiList listValue;

        [FieldOffset(0)] public TopiEnum enumValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TopiEnum
    {
        internal IntPtr namePtr;
        internal IntPtr valuePtr;
        public string Name => Library.PtrToUtf8String(namePtr);
        public string Value => Library.PtrToUtf8String(valuePtr);

        public override int GetHashCode() => Name.GetHashCode() + Value.GetHashCode();
    }


    [StructLayout(LayoutKind.Sequential)]
    internal struct TopiList
    {
        internal IntPtr listPtr;
        [MarshalAs(UnmanagedType.U2)] public short count;

        internal TopiValue[] List
        {
            get
            {
                var value = new TopiValue[count];
                var ptr = listPtr;
                for (var i = 0; i < count; i++)
                {
                    value[i] = TopiValue.FromPtr(ptr);
                    ptr = IntPtr.Add(ptr, Marshal.SizeOf<TopiValue>());
                }

                return value;
            }
        }

        internal HashSet<TopiValue> Set
        {
            get
            {
                var set = new HashSet<TopiValue>();
                var ptr = listPtr;
                for (var i = 0; i < count; i++)
                {
                    set.Add(TopiValue.FromPtr(ptr));
                    ptr = IntPtr.Add(ptr, Marshal.SizeOf<TopiValue>());
                }

                return set;
            }
        }

        internal Dictionary<TopiValue, TopiValue> Map
        {
            get
            {
                var map = new Dictionary<TopiValue, TopiValue>(count);
                var ptr = listPtr;
                for (var i = 0; i < count; i++)
                {
                    var key = TopiValue.FromPtr(ptr);
                    ptr = IntPtr.Add(ptr, Marshal.SizeOf<TopiValue>());
                    var value = TopiValue.FromPtr(ptr);
                    ptr = IntPtr.Add(ptr, Marshal.SizeOf<TopiValue>());
                    map.Add(key, value);
                }

                return map;
            }
        }
    }
}
