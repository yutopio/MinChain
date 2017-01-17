using System.Collections.Generic;
using ZeroFormatter;
using static ZeroFormatter.ZeroFormatterSerializer;

namespace MinChain
{
    public enum MessageType
    {
        Hello,
        Inventory
    }

    [ZeroFormattable]
    public class Message
    {
        [Index(0)]
        public virtual MessageType Type { get; set; }

        [Index(1)]
        public virtual byte[] Payload { get; set; }
    }

    [ZeroFormattable]
    public class Hello
    {
        [Index(0)]
        public virtual IList<string> MyPeers { get; set; }

        [Index(1)]
        public virtual ByteString Genesis { get; set; }

        [Index(2)]
        public virtual IList<ByteString> KnownBlocks { get; set; }

        public static implicit operator Message(Hello message)
        {
            return new Message
            {
                Type = MessageType.Hello,
                Payload = Serialize(message),
            };
        }
    }

    public enum InventoryMessageType : byte
    {
        Advertise, Request, Body
    }

    [ZeroFormattable]
    public class InventoryMessage
    {
        [Index(0)]
        public virtual InventoryMessageType Type { get; set; }

        [Index(1)]
        public virtual ByteString ObjectId { get; set; }

        [Index(2)]
        public virtual bool IsBlock { get; set; }

        [Index(3)]
        public virtual byte[] Data { get; set; }

        public static implicit operator Message(InventoryMessage message)
        {
            return new Message
            {
                Type = MessageType.Inventory,
                Payload = Serialize(message),
            };
        }
    }
}
