using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static MinChain.InventoryMessageType;
using static MessagePack.MessagePackSerializer;

namespace MinChain
{
    public class InventoryManager
    {
        public const int MaximumBlockSize = 1024 * 1024; // 1MB
        public const int MaximumTransactionSize = 2 * 1024; // 2KB

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
            if (message.IsBlock)
            {
                var block = TryLoadBlock(message.ObjectId, message.Data);
                if (block.IsNull()) return;

                var prevId = block.PreviousHash;
                if (!Blocks.ContainsKey(prevId))
                {
                    // Otherwise, ask the sending peer to provide previous block.
                    await ConnectionManager.SendAsync(new InventoryMessage
                    {
                        Type = Request,
                        IsBlock = true,
                        ObjectId = prevId,
                    }, peerId);
                }
            }
            else
            {
                var success = TryAddTransactionToMemoryPool(
                    message.ObjectId, message.Data);
                if (!success) return;
            }

            message.Type = Advertise;
            message.Data = null;
            await ConnectionManager.BroadcastAsync(message, peerId);
        }

        public Block TryLoadBlock(ByteString id, byte[] data)
        {
            // Data should not exceed the maximum size.
            if (data.Length > MaximumBlockSize)
                throw new ArgumentException(nameof(data));

            // Integrity check.
            var computedId = BlockchainUtil.ComputeBlockId(data);
            if (!ByteString.CopyFrom(computedId).Equals(id))
                throw new ArgumentException(nameof(id));

            // Try to deserialize the data for format validity check.
            var block = BlockchainUtil.DeserializeBlock(data);

            lock (Blocks)
            {
                if (Blocks.ContainsKey(id)) return null;
                Blocks.Add(id, data);
            }

            // Schedule the block for execution.
            Executor.ProcessBlock(block);

            return block;
        }

        bool TryAddTransactionToMemoryPool(ByteString id, byte[] data)
        {
            // Data should not exceed the maximum size.
            if (data.Length > MaximumTransactionSize)
                throw new ArgumentException();

            // Integrity check.
            var computedId = Hash.ComputeDoubleSHA256(data);
            if (!ByteString.CopyFrom(computedId).Equals(id))
                throw new ArgumentException();

            if (MemoryPool.ContainsKey(id)) return false;

            var tx = BlockchainUtil.DeserializeTransaction(data);

            // Ignore the coinbase transactions.
            if (tx.InEntries.Count == 0) return false;

            lock (MemoryPool)
            {
                if (MemoryPool.ContainsKey(id)) return false;
                MemoryPool.Add(id, tx);
            }

            return true;
        }
    }
}
