namespace MinChain
{
    public enum MessageType
    {
        Hello,
        Inventory
    }

    public class Message
    {
        public MessageType Type { get; set; }
        public byte[] Payload { get; set; }
    }

    public enum InventoryMessageType : byte
    {
        Advertise, Request, Body
    }

    public class InventoryMessage
    {
        public InventoryMessageType Type { get; set; }
        public ByteString ObjectId { get; set; }
        public bool IsBlock { get; set; }
        public byte[] Data { get; set; }

        public static implicit operator Message(InventoryMessage message) =>
            null;
    }
}
