using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace PeartreeGames.Topiary.Unity
{
    /// <summary>
    /// Topiary Value container
    /// Data is overlapped in memory so ensure correct value is used
    /// or check tag if unknown
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TopiValue : IEquatable<TopiValue>, IDisposable
    {
        public Tag tag;
        private TopiValueData _data;

        // Cached marshaling stride. Marshal.SizeOf<T>() is reflective; cache once.
        internal static readonly int Stride = Marshal.SizeOf<TopiValue>();

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
                stringValue = new StringBuffer(s)
            };
        }

        public TopiValue(string enumName, string enumValue)
        {
            tag = Tag.Enum;
            _data = new TopiValueData
            {
                enumValue = new TopiEnum(enumName, enumValue)
            };
        }

        /// <summary>
        /// Represents the different types of tags for the TopiValue struct.
        /// </summary>
        public enum Tag : byte
        {
            Nil,
            Bool,
            Number,
            String,
            List,
            Set,
            Map,
            Enum
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

        /// <remarks>
        /// Allocates a fresh managed string each access by decoding native UTF-8 bytes.
        /// Capture into a local for hot loops.
        /// </remarks>
        public string String => tag == Tag.String
            ? _data.stringValue.Value
            : throw new InvalidOperationException($"Value {tag} cannot be used as string");

        /// <remarks>
        /// Walks native memory and allocates a fresh array on every access. Capture into a
        /// local for hot loops (<c>var list = val.List;</c>) — repeated reads do not memoize.
        /// </remarks>
        public TopiValue[] List => tag == Tag.List
            ? _data.listValue.List
            : throw new InvalidOperationException($"Value {tag} cannot be used as list");

        /// <remarks>
        /// Walks native memory and allocates a fresh HashSet on every access. Capture into a
        /// local for hot loops — repeated reads do not memoize.
        /// </remarks>
        public HashSet<TopiValue> Set => tag == Tag.Set
            ? _data.listValue.Set
            : throw new InvalidOperationException($"Value {tag} cannot be used as set");

        /// <remarks>
        /// Walks native memory and allocates a fresh Dictionary on every access. Capture into a
        /// local for hot loops — repeated reads do not memoize.
        /// </remarks>
        public Dictionary<TopiValue, TopiValue> Map => tag == Tag.Map
            ? _data.listValue.Map
            : throw new InvalidOperationException($"Value {tag} cannot be used as set");

        public TopiEnum Enum => tag == Tag.Enum
            ? _data.enumValue
            : throw new InvalidOperationException($"Value {tag} cannot be used as enum");

        /// <remarks>
        /// Boxes value types and re-walks native memory on every access. Prefer the typed
        /// accessors (<see cref="Bool"/>, <see cref="Int"/>, etc.) when the tag is known.
        /// </remarks>
        public object Value => tag switch
        {
            Tag.Bool => _data.boolValue == 1,
            Tag.Number => _data.numberValue,
            Tag.String => _data.stringValue.Value,
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
                Tag.String => _data.stringValue.Value,
                Tag.List => $"List{{{string.Join(", ", _data.listValue.List)}}}",
                Tag.Set => $"Set{{{string.Join(", ", _data.listValue.Set)}}}",
                Tag.Map => $"Map{{{string.Join(", ", _data.listValue.Map)}}}",
                Tag.Enum => $"{_data.enumValue.Name}.{_data.enumValue.Value}",
                _ => $"{tag}: null"
            };

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
                    return _data.numberValue.Equals(other._data.numberValue);
                case Tag.String:
                    return _data.stringValue.Value == other._data.stringValue.Value;
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
                    Tag.List or Tag.Set or Tag.Map => _data.listValue.GetHashCode(),
                    Tag.Enum => _data.enumValue.GetHashCode(),
                    _ => -1
                };
            }
        }

        /// <summary>
        /// Frees the unmanaged memory backing this value's string / list / set / map / enum
        /// payload. Safe no-op for primitives (Bool, Number, Nil).
        /// </summary>
        /// <remarks>
        /// Constructing a List/Set/Map TopiValue transfers ownership of the inner element
        /// allocations to the outer container — the inner originals must NOT be disposed
        /// separately. Dispose only the outermost value you constructed.
        ///
        /// When a TopiValue is returned from a [Topi] extern, the native side handles the
        /// free via the registered free callback; do not call Dispose in that path.
        ///
        /// Calling Dispose more than once on the same value is a double-free.
        /// </remarks>
        public void Dispose()
        {
            switch (tag)
            {
                case Tag.String:
                    _data.stringValue.Free();
                    break;
                case Tag.Enum:
                    _data.enumValue.Free();
                    break;
                case Tag.List:
                case Tag.Set:
                case Tag.Map:
                    _data.listValue.Free();
                    break;
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
                ptr = IntPtr.Add(ptr, TopiValue.Stride);
            }

            return args;
        }

        public static bool operator ==(TopiValue left, TopiValue right) => left.Equals(right);
        public static bool operator !=(TopiValue left, TopiValue right) => !(left == right);
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct TopiValueData
    {
        [FieldOffset(0)]
        public byte boolValue;

        [FieldOffset(0)]
        public float numberValue;

        [FieldOffset(0)] public StringBuffer stringValue;

        [FieldOffset(0)] public TopiList listValue;

        [FieldOffset(0)] public TopiEnum enumValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct StringBuffer : IEquatable<StringBuffer>
    {
        private readonly IntPtr strPtr;
        private readonly UIntPtr strLen;

        internal static readonly int Stride = Marshal.SizeOf<StringBuffer>();


        public StringBuffer(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            strLen = (UIntPtr)bytes.Length;
            strPtr = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, strPtr, bytes.Length);
            Marshal.WriteByte(strPtr, bytes.Length, 0);
        }

        public int Length => (int)strLen;
        public string Value => Marshal.PtrToStringUTF8(strPtr, (int)strLen);

        public bool Equals(StringBuffer other) => Value == other.Value;

        public override bool Equals(object obj) => obj is StringBuffer other && Equals(other);

        public override int GetHashCode() => Value?.GetHashCode() ?? 0;

        // Releases the unmanaged byte buffer. Pairs with the AllocHGlobal in the ctor and the
        // free callback registered with the VM. Caller must not invoke twice.
        internal void Free()
        {
            if (strPtr != IntPtr.Zero) Marshal.FreeHGlobal(strPtr);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct TopiEnum : IEquatable<TopiEnum>
    {
        private readonly StringBuffer name;
        private readonly StringBuffer value;
        public string Name => name.Value;
        public string Value => value.Value;

        public TopiEnum(string enumName, string enumValue)
        {
            name = new StringBuffer(enumName);
            value = new StringBuffer(enumValue);
        }

        public override int GetHashCode() => Name.GetHashCode() + Value.GetHashCode();

        public bool Equals(TopiEnum other)
        {
            return name.Equals(other.name) && value.Equals(other.value);
        }

        public override bool Equals(object obj)
        {
            return obj is TopiEnum other && Equals(other);
        }

        internal void Free()
        {
            name.Free();
            value.Free();
        }
    }


    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct TopiList : IEquatable<TopiList>
    {
        private readonly IntPtr listPtr;
        private readonly ushort count;

        public TopiList(TopiValue[] list)
        {
            count = (ushort)list.Length;
            var stride = TopiValue.Stride;
            listPtr = Marshal.AllocHGlobal(stride * count);
            for (var i = 0; i < count; i++)
            {
                var itemPtr = new IntPtr(listPtr.ToInt64() + i * stride);
                Marshal.StructureToPtr(list[i], itemPtr, false);
            }
        }

        internal TopiValue[] List
        {
            get
            {
                var value = new TopiValue[count];
                var ptr = listPtr;
                for (var i = 0; i < count; i++)
                {
                    value[i] = TopiValue.FromPtr(ptr);
                    ptr = IntPtr.Add(ptr, TopiValue.Stride);
                }

                return value;
            }
        }

        public TopiList(HashSet<TopiValue> set)
        {
            count = (ushort)set.Count;
            var stride = TopiValue.Stride;
            listPtr = Marshal.AllocHGlobal(stride * count);
            var i = 0;
            foreach (var item in set)
            {
                var itemPtr = new IntPtr(listPtr.ToInt64() + i * stride);
                i++;
                Marshal.StructureToPtr(item, itemPtr, false);
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
                    ptr = IntPtr.Add(ptr, TopiValue.Stride);
                }

                return set;
            }
        }

        public TopiList(Dictionary<TopiValue, TopiValue> map)
        {
            // count is total element count, so a map of N pairs stores 2*N flattened key/value entries
            count = (ushort)(map.Count * 2);
            var stride = TopiValue.Stride;
            listPtr = Marshal.AllocHGlobal(stride * count);
            var i = 0;
            foreach (var kvp in map)
            {
                var itemPtr = new IntPtr(listPtr.ToInt64() + i * stride);
                i++;
                var valuePtr = new IntPtr(listPtr.ToInt64() + i * stride);
                i++;
                Marshal.StructureToPtr(kvp.Key, itemPtr, false);
                Marshal.StructureToPtr(kvp.Value, valuePtr, false);
            }
        }
        internal Dictionary<TopiValue, TopiValue> Map
        {
            get
            {
                var pairs = count / 2;
                var map = new Dictionary<TopiValue, TopiValue>(pairs);
                var ptr = listPtr;
                for (var i = 0; i < pairs; i++)
                {
                    var key = TopiValue.FromPtr(ptr);
                    ptr = IntPtr.Add(ptr, TopiValue.Stride);
                    var value = TopiValue.FromPtr(ptr);
                    ptr = IntPtr.Add(ptr, TopiValue.Stride);
                    map.Add(key, value);
                }

                return map;
            }
        }

        // Walks the unmanaged buffer, recursively disposes each element so inner allocations
        // (strings inside a list, enum buffers, nested lists) are released, then frees the
        // outer buffer. Mirrors how the native side releases a TopiList via the free callback.
        internal void Free()
        {
            if (listPtr == IntPtr.Zero) return;
            var ptr = listPtr;
            for (var i = 0; i < count; i++)
            {
                TopiValue.FromPtr(ptr).Dispose();
                ptr = IntPtr.Add(ptr, TopiValue.Stride);
            }
            Marshal.FreeHGlobal(listPtr);
        }

        public bool Equals(TopiList other)
        {
            return listPtr.Equals(other.listPtr) && count == other.count;
        }

        public override bool Equals(object obj)
        {
            return obj is TopiList other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(listPtr, count);
        }
    }
}