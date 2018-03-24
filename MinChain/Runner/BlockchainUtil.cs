using System;
using System.Collections.Generic;
using System.Linq;
using static MessagePack.MessagePackSerializer;

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

        public static byte[] RootHashTransactionIds(IList<ByteString> txIds) =>
            ComputeMerkleRoot(txIds.Select(x => x.ToByteArray()).ToArray());

        public static byte[] ComputeMerkleRoot(byte[][] hashes)
        {
            // If no transaction is present, Merkle root should be 0000...0
            var count = hashes.Length;
            if (count == 0) return new byte[32];

            while (count > 1)
            {
                for (var i = 0; i < count / 2; i++)
                {
                    hashes[i] = Hash.ComputeDoubleSHA256(
                        hashes[i * 2].Concat(hashes[i * 2 + 1]).ToArray());
                }

                if (count % 2 == 1)
                {
                    hashes[count / 2] = hashes[count - 1];
                    count = count / 2 + 1;
                }
                else
                {
                    count = count / 2;
                }
            }

            return hashes[0];
        }

        public static Block DeserializeBlock(byte[] data)
        {
            var block = Deserialize<Block>(data);
            block.Original = data;
            block.Id = ByteString.CopyFrom(ComputeBlockId(data));
            return block;
        }

        public static Transaction DeserializeTransaction(byte[] data)
        {
            var tx = Deserialize<Transaction>(data);
            tx.Original = data;
            tx.Id = ByteString.CopyFrom(ComputeTransactionId(data));
            return tx;
        }

        public static byte[] ComputeBlockId(byte[] data)
        {
            var block = Deserialize<Block>(data).Clone();
            block.TransactionIds = null;
            block.Transactions = null;
            var bytes = Serialize(block);
            return Hash.ComputeDoubleSHA256(bytes);
        }

        public static byte[] ComputeTransactionId(byte[] data)
        {
            return Hash.ComputeDoubleSHA256(data);
        }

        public static byte[] GetTransactionSignHash(byte[] data)
        {
            var tx = Deserialize<Transaction>(data).Clone();
            foreach (var inEntry in tx.InEntries)
            {
                inEntry.PublicKey = null;
                inEntry.Signature = null;
            }
            var bytes = Serialize(tx);
            return Hash.ComputeDoubleSHA256(bytes);
        }

        public static byte[] ToAddress(byte[] publicKey)
        {
            // NOTE: In Bitcoin, recipient address is computed by SHA256 +
            // RIPEMD160.
            return Hash.ComputeDoubleSHA256(publicKey);
        }
    }
}
