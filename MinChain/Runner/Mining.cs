using System;
using System.Collections.Generic;
using System.Threading;
using static MinChain.BlockchainUtil;
using static ZeroFormatter.ZeroFormatterSerializer;

namespace MinChain
{
    public class Mining
    {
        public static Transaction CreateCoinbase(int height, byte[] recipient)
        {
            var tx = new Transaction
            {
                Timestamp = DateTime.UtcNow,
                InEntries = new List<InEntry>(),
                OutEntries = new List<OutEntry>
                {
                    new OutEntry
                    {
                        Amount = BlockParameter.GetCoinbase(0),
                        RecipientHash = ByteString.CopyFrom(recipient),
                    },
                },
            };

            var data = tx.Original = Serialize(tx);
            tx.Id = ByteString.CopyFrom(ComputeTransactionId(data));
            return tx;
        }

        public static bool Mine(Block seed,
            CancellationToken token = default(CancellationToken))
        {
            var rnd = new Random();
            var nonceSeed = new byte[sizeof(ulong)];
            rnd.NextBytes(nonceSeed);

            ulong nonce = BitConverter.ToUInt64(nonceSeed, 0);
            while (!token.IsCancellationRequested)
            {
                seed.Nonce = nonce++;
                seed.Timestamp = DateTime.UtcNow;

                var data = Serialize(seed);
                var blockId = ComputeBlockId(data);
                if (Hash.Difficulty(blockId) > seed.Difficulty)
                {
                    seed.Id = ByteString.CopyFrom(blockId);
                    seed.Original = data;
                    return true;
                }
            }

            return false;
        }
    }
}
