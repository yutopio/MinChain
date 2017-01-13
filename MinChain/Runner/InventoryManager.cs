using System.Collections.Generic;

namespace MinChain
{
    public class InventoryManager
    {
        public const int MaximumBlockSize = 1024 * 1024; // 1MB

        public Dictionary<ByteString, byte[]> Blocks { get; }
            = new Dictionary<ByteString, byte[]>();
        public Dictionary<ByteString, byte[]> MemoryPool { get; }
            = new Dictionary<ByteString, byte[]>();

        public void ReceivedAdvertise(InventoryMessage message, int peerId)
        {
        }

        public void ReceivedRequest(InventoryMessage message, int peerId)
        {
        }

        public void ReceivedBody(InventoryMessage message, int peerId)
        {
        }
    }
}
