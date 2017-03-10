using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static MinChain.InventoryMessageType;
using static ZeroFormatter.ZeroFormatterSerializer;

namespace MinChain
{
    public class InventoryManager
    {
        public const int MaximumBlockSize = 1024 * 1024; // 1MB

        public Dictionary<ByteString, byte[]> Blocks { get; }
            = new Dictionary<ByteString, byte[]>();
        public Dictionary<ByteString, Transaction> MemoryPool { get; }
            = new Dictionary<ByteString, Transaction>();

        public ConnectionManager ConnectionManager { get; set; }
        public Executor Executor { get; set; }

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
            // Data should not contain anything. (To prevent DDoS)
            if (!message.Data.IsNull()) throw new ArgumentException();

            var haveObject = message.IsBlock ?
                Blocks.ContainsKey(message.ObjectId) :
                MemoryPool.ContainsKey(message.ObjectId);
            if (haveObject) return;

            message.Type = Request;
            await ConnectionManager.SendAsync(message, peerId);
        }

        async Task HandleRequest(InventoryMessage message, int peerId)
        {
            // Data should not contain anything. (To prevent DDoS)
            if (!message.Data.IsNull()) throw new ArgumentException();

            byte[] data;
            if (message.IsBlock)
            {
                if (!Blocks.TryGetValue(message.ObjectId, out data)) return;
            }
            else
            {
                Transaction tx;
                if (!MemoryPool.TryGetValue(message.ObjectId, out tx)) return;
                data = tx.Original;
            }

            message.Type = Body;
            message.Data = data;
            await ConnectionManager.SendAsync(message, peerId);
        }

        async Task HandleBody(InventoryMessage message, int peerId)
        {
            // Data should not exceed the maximum size.
            var data = message.Data;
            if (data.Length > MaximumBlockSize) throw new ArgumentException();

            var id = message.IsBlock ?
                BlockchainUtil.ComputeBlockId(data) :
                Hash.ComputeDoubleSHA256(data);
            if (!ByteString.CopyFrom(id).Equals(message.ObjectId)) return;

            if (message.IsBlock)
            {
                lock (Blocks)
                {
                    if (Blocks.ContainsKey(message.ObjectId)) return;
                    Blocks.Add(message.ObjectId, data);
                }

                var prevId = Deserialize<Block>(data).PreviousHash;
                if (!Blocks.ContainsKey(prevId))
                {
                    await ConnectionManager.SendAsync(new InventoryMessage
                    {
                        Type = Request,
                        IsBlock = true,
                        ObjectId = prevId,
                    }, peerId);
                }
                else
                {
                    Executor.ProcessBlock(data, prevId);
                }
            }
            else
            {
                if (MemoryPool.ContainsKey(message.ObjectId)) return;

                var tx = BlockchainUtil.DeserializeTransaction(data);

                // Ignore the coinbase transactions.
                if (tx.InEntries.Count == 0) return;

                lock (MemoryPool)
                {
                    if (MemoryPool.ContainsKey(message.ObjectId)) return;
                    MemoryPool.Add(message.ObjectId, tx);
                }
            }

            message.Type = Advertise;
            message.Data = null;
            await ConnectionManager.BroadcastAsync(message, peerId);
        }
    }
}
