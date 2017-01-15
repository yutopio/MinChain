using System;
using System.Collections.Generic;

namespace MinChain
{
    public static class BlockchainUtil
    {
        public static IEnumerable<Block> Ancestors(this Block block,
             Dictionary<ByteString, Block> blocks)
        {
            var hash = block.Id;
            while (!hash.IsNull())
            {
                if (!blocks.TryGetValue(hash, out block)) yield break;
                yield return block;

                hash = block.PreviousHash;
            }
        }

        public static Block LowestCommonAncestor(Block b1, Block b2,
            Dictionary<ByteString, Block> blocks)
        {
            var set = new HashSet<ByteString>();

            using (var e1 = b1.Ancestors(blocks).GetEnumerator())
            using (var e2 = b2.Ancestors(blocks).GetEnumerator())
            {
                bool f1, f2 = false;
                while ((f1 = e1.MoveNext()) || (f2 = e2.MoveNext()))
                {
                    if (f1 && !set.Add(e1.Current.Id)) return e1.Current;
                    if (f2 && !set.Add(e2.Current.Id)) return e2.Current;
                }
            }

            return null;
        }

        public static byte[] RootHashTransactionIds(IList<ByteString> txIds)
        {
            const int HashLength = 32;

            var i = 0;
            var ids = new byte[txIds.Count * HashLength];
            foreach (var txId in txIds)
            {
                Buffer.BlockCopy(
                    txId.ToByteArray(), 0, ids, i++ * HashLength, HashLength);
            }

            return Hash.ComputeDoubleSHA256(ids);
        }

        public static Block DeserializeBlock(byte[] data)
        {
            var block = new Block();
            block.Original = data;
            block.Id = ByteString.CopyFrom(ComputeBlockId(data));
            return block;
        }

        public static Transaction DeserializeTransaction(byte[] data)
        {
            var tx = new Transaction();
            tx.Original = data;
            tx.Id = ByteString.CopyFrom(ComputeTransactionId(data));
            return tx;
        }

        public static byte[] ComputeBlockId(byte[] data)
        {
            var block = DeserializeBlock(data);
            block.TransactionIds = null;
            block.Transactions = null;
            return Hash.ComputeDoubleSHA256(data);
        }

        public static byte[] ComputeTransactionId(byte[] data)
        {
            return Hash.ComputeDoubleSHA256(data);
        }

        public static byte[] GetTransactionSignHash(byte[] data)
        {
            var tx = DeserializeTransaction(data);
            foreach (var inEntry in tx.InEntries)
            {
                inEntry.Signature = null;
            }
            return Hash.ComputeDoubleSHA256(data);
        }
    }
}
