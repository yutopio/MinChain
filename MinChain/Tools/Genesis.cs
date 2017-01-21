using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using static MinChain.BlockchainUtil;
using static System.Console;

namespace MinChain
{
    public class Genesis
    {
        public static ByteString EmptyHash = ByteString.CopyFrom(new byte[32]);
        public static double Difficulty = 2e-6;

        public static void Exec(string[] args)
        {
            if (args.Length != 2)
            {
                WriteLine("Provide public key and path to genesis:");
                WriteLine("  genesis <key>.json <genesis>.bin");
                return;
            }

            var keyPair = KeyPair.LoadFrom(args[0]);

            WriteLine("Creating new genesis block.");

            var tx = Mining.CreateCoinbase(0, ToAddress(keyPair.PublicKey));
            var txIds = new List<ByteString> { tx.Id };
            var root = RootHashTransactionIds(txIds);

            var b = new Block
            {
                PreviousHash = EmptyHash,
                Difficulty = Difficulty,
                Nonce = 0,
                Timestamp = DateTime.UtcNow,
                TransactionRootHash = root,
                TransactionIds = txIds,
                Transactions = new List<byte[]> { tx.Original },
                ParsedTransactions = new[] { tx }
            };

            Mining.Mine(b);

            var json = JsonConvert.SerializeObject(b, Formatting.Indented);
            WriteLine(json);

            using (var fs = File.OpenWrite(args[1]))
            {
                fs.Write(b.Original, 0, b.Original.Length);
                fs.Flush();
            }
        }
    }
}
