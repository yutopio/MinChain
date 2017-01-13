using System;
using System.Linq;

namespace MinChain
{
    /// <summary>
    /// Represents an immutable string of bytes.
    /// </summary>
    public class ByteString : IEquatable<ByteString>, IComparable<ByteString>
    {
        readonly byte[] bytes;

        ByteString(byte[] bytes)
        {
            this.bytes = bytes;
        }

        public static ByteString CopyFrom(byte[] bytes) =>
            new ByteString((byte[])bytes.Clone());

        public byte[] ToByteArray() => (byte[])bytes.Clone();

        public int Length => bytes.Length;

        public int CompareTo(ByteString other)
        {
            if (other.IsNull()) return 1;

            var minCount = Math.Min(bytes.Length, other.bytes.Length);
            for (var i = 0; i < minCount; i++)
            {
                var result = bytes[i].CompareTo(other.bytes[i]);
                if (result != 0) return result;
            }

            return bytes.Length.CompareTo(other.bytes.Length);
        }

        public bool Equals(ByteString other) =>
            !other.IsNull() && bytes.SequenceEqual(other.bytes);

        public override bool Equals(object obj) => Equals(obj as ByteString);

        public override int GetHashCode() =>
            bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24);

        public override string ToString() => HexConvert.FromBytes(bytes);
    }
}
