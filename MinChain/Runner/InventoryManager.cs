using System.Collections.Generic;
using System.Threading.Tasks;
using static MinChain.InventoryMessageType;

namespace MinChain
{
    public class InventoryManager
    {
        public const int MaximumBlockSize = 1024 * 1024; // 1MB

        public Dictionary<ByteString, byte[]> Blocks { get; }
            = new Dictionary<ByteString, byte[]>();
        public Dictionary<ByteString, byte[]> MemoryPool { get; }
            = new Dictionary<ByteString, byte[]>();

        public ConnectionManager ConnectionManager { get; set; }

        public Task HandleMessage(InventoryMessage message, int peerId)
        {
            switch (message.Type)
            {
                case Advertise: return HandleAdvertise(message, peerId);
                case Request: return HandleRequest(message, peerId);
                case Body: return HandleBody(message, peerId);
                default: return Task.CompletedTask;
            }
        }

        async Task HandleAdvertise(InventoryMessage message, int peerId)
        {
            var dic = message.IsBlock ? Blocks : MemoryPool;
            if (dic.ContainsKey(message.ObjectId))
                return;

            message.Type = Request;
            await ConnectionManager.SendAsync(message, peerId);
        }

        async Task HandleRequest(InventoryMessage message, int peerId)
        {
            byte[] data;
            var dic = message.IsBlock ? Blocks : MemoryPool;
            if (!dic.TryGetValue(message.ObjectId, out data))
                return;

            message.Type = Body;
            message.Data = data;
            await ConnectionManager.SendAsync(message, peerId);
        }

        async Task HandleBody(InventoryMessage message, int peerId)
        {
            var data = message.Data;
            if (data.Length > MaximumBlockSize) return;

            var id = Hash.ComputeDoubleSHA256(data);
            if (!id.Equals(message.ObjectId)) return;

            var dic = message.IsBlock ? Blocks : MemoryPool;
            if (dic.ContainsKey(message.ObjectId)) return;

            dic.Add(message.ObjectId, data);

            message.Type = Advertise;
            message.Data = null;
            await ConnectionManager.BroadcastAsync(message, peerId);
        }
    }
}
