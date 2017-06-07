using System.Collections.Generic;
using MessagePack;
using static MessagePack.MessagePackSerializer;

namespace MinChain
{
    public enum MessageType
    {
        Hello,
        Inventory
    }

    [MessagePackObject]
    public class Message
    {
        [Key(0)]
        public virtual MessageType Type { get; set; }

        [Key(1)]
        public virtual byte[] Payload { get; set; }
    }

    [MessagePackObject]
    public class Hello
    {
        [Key(0)]
        public virtual IList<string> MyPeers { get; set; }

        [Key(1)]
        public virtual ByteString Genesis { get; set; }

        [Key(2)]
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

    [MessagePackObject]
    public class InventoryMessage
    {
        [Key(0)]
        public virtual InventoryMessageType Type { get; set; }

        [Key(1)]
        public virtual ByteString ObjectId { get; set; }

        [Key(2)]
        public virtual bool IsBlock { get; set; }

        [Key(3)]
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
