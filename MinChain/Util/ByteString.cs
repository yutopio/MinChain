using System;
using System.Linq;
using MessagePack;
using MessagePack.Formatters;
using Newtonsoft.Json;

namespace MinChain
{
    /// <summary>
    /// Represents an immutable string of bytes.
    /// </summary>
    [JsonConverter(typeof(ByteStringConverter))]
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

        public class ByteStringFormatter : IMessagePackFormatter<ByteString>
        {
            public static ByteStringFormatter Instance = new ByteStringFormatter();

            ByteStringFormatter() { }

            public int Serialize(ref byte[] bytes, int offset,
                ByteString value, IFormatterResolver formatterResolver)
            {
                if (value == null)
                {
                    return MessagePackBinary.WriteNil(ref bytes, offset);
                }

                return MessagePackBinary.WriteBytes(
                    ref bytes, offset, value.bytes);
            }

            public ByteString Deserialize(byte[] bytes, int offset,
                IFormatterResolver formatterResolver, out int readSize)
            {
                if (MessagePackBinary.IsNil(bytes, offset))
                {
                    readSize = 1;
                    return null;
                }

                var buffer = MessagePackBinary.ReadBytes(
                    bytes, offset, out readSize);
                return new ByteString(buffer);
            }
        }

        public class ByteStringResolver : IFormatterResolver
        {
            public static IFormatterResolver Instance = new ByteStringResolver();

            ByteStringResolver() { }

            public IMessagePackFormatter<T> GetFormatter<T>()
            {
                return typeof(T) == typeof(ByteString) ?
                    (IMessagePackFormatter<T>)ByteStringFormatter.Instance : null;
            }
        }

        public class ByteStringConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) =>
                objectType == typeof(byte[]);

            public override object ReadJson(
                JsonReader reader, Type objectType, object existingValue,
                JsonSerializer serializer)
            {
                return new ByteString(HexConvert.ToBytes(
                    serializer.Deserialize<string>(reader) ?? ""));
            }

            public override void WriteJson(
                JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteValue(
                    HexConvert.FromBytes(((ByteString)value).bytes));
            }
        }
    }
}
