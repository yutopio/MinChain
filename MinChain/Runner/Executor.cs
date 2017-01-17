using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MinChain
{
    public class Executor
    {
        static readonly ILogger logger = Logging.Logger<Executor>();

        public InventoryManager InventoryManager { get; set; }

        // A connected block is a block whose ancestors upto the genesis are
        // already seen by the system.
        //
        // This dictionary is a mapping from the block ID to the deserialized
        // instance of the Block.  Some of them are not yet executed.
        public Dictionary<ByteString, Block> Blocks { get; }
            = new Dictionary<ByteString, Block>();

        // A floating block is a block whose ancestor block is not seen by the
        // system, and thus which cannot be processed at the moment.
        //
        // This dictionary is a mapping from ancestor block ID to the list of
        // block IDs of decendants.  The decendants will be processed after the
        // ancestor is successfuly connected.
        readonly Dictionary<ByteString, List<ByteString>> floatingBlocks;

        // UTXO (Unspent Transaction Output) is a unit of recipient information
        // of fund transferred during the transaction that is not redeemed yet.
        // UTXO is spent once listed as a future transaction input and removed
        // from the set of UTXOs.
        //
        // This dictionary is used as a set of UTXOs.  The justification for
        // using Dictionary data structure is to query UTXO from the partially
        // constructed UTXO instance with TxID and Output index.  This is
        // possible because TransactionOutput implements comparator which use
        // TxID and output index for the comparison.
        readonly Dictionary<TransactionOutput, TransactionOutput> utxo;

        Block latest;

        public Executor()
        {
            floatingBlocks = new Dictionary<ByteString, List<ByteString>>();
            utxo = new Dictionary<TransactionOutput, TransactionOutput>();
        }

        public void ProcessBlock(byte[] data, ByteString prevId)
        {
            Block prev;
            if (!Blocks.TryGetValue(prevId, out prev))
            {
                // If the previous block is not under the block tree, mark the
                // block as floating.
                List<ByteString> blocks;
                if (!floatingBlocks.TryGetValue(prevId, out blocks))
                    floatingBlocks[prevId] = blocks = new List<ByteString>();
                blocks.Add(prevId);
                return;
            }

            // Mark the block as the connected block.
            var block = BlockchainUtil.DeserializeBlock(data);
            block.Height = prev.Height + 1;
            block.TotalDifficulty = prev.TotalDifficulty + block.Difficulty;
            Blocks.Add(block.Id, block);

            // If the block difficulty does not surpass the current latest,
            // skip the execution.  Once the descendant block comes later,
            // evaluate the difficulty then again.
            if (latest.TotalDifficulty >= block.TotalDifficulty)
            {
                CheckFloatingBlocks(block.Id);
                return;
            }

            // Otherwise, try to execute the block.  Considering the block folk,
            // first revert all the applied blocks prior to the fork point in
            // past blocks, if exists.  Then apply blocks after the fork.
            var fork = BlockchainUtil.LowestCommonAncestor(
                latest, block, Blocks);
            var revertingChain = latest.Ancestors(Blocks)
                .TakeWhile(x => !x.Id.Equals(fork.Id))
                .ToList();
            var applyingChain = block.Ancestors(Blocks)
                .TakeWhile(x => !x.Id.Equals(fork.Id))
                .Reverse()
                .ToList();

            revertingChain.ForEach(Revert);

            int? failureIndex = null;
            for (var i = 0; i < applyingChain.Count; i++)
            {
                var applyBlock = applyingChain[i];
                try { Run(applyBlock); }
                catch
                {
                    // The block was invalid.  Revert.
                    PurgeBlock(applyBlock.Id);

                    failureIndex = i;
                    break;
                }

                Apply(applyBlock);
            }

            if (failureIndex.HasValue)
            {
                // Failure occurred during the block execution.  Perform
                // opposite to revert to the state before the execution.
                applyingChain.Take(failureIndex.Value)
                    .Reverse().ToList().ForEach(Revert);

                revertingChain.Reverse();
                revertingChain.ForEach(Apply);
                return;
            }

            CheckFloatingBlocks(block.Id);
        }

        void CheckFloatingBlocks(ByteString waitingBlockId)
        {
            List<ByteString> pendingBlocks;
            if (floatingBlocks.TryGetValue(waitingBlockId, out pendingBlocks))
            {
                foreach (var floatBlockId in pendingBlocks)
                {
                    ProcessBlock(
                        InventoryManager.Blocks[floatBlockId],
                        waitingBlockId);
                }
            }
        }

        void Apply(Block block)
        {
            logger.LogWarning($@"Applying Block {block.Height}:{
                block.Id.ToString().Substring(0, 7)}.");

            foreach (var tx in block.ParsedTransactions)
            {
                if (!tx.ExecInfo.Coinbase)
                    InventoryManager.MemoryPool.Remove(tx.Id);

                tx.ExecInfo.RedeemedOutputs.ForEach(x => utxo.Remove(x));
                tx.ExecInfo.GeneratedOutputs.ForEach(x => utxo.Add(x, x));
            }

            latest = block;
        }

        void Revert(Block block)
        {
            logger.LogWarning($@"Reverting Block {block.Height}:{
                block.Id.ToString().Substring(0, 7)}.");

            foreach (var tx in block.ParsedTransactions)
            {
                if (!tx.ExecInfo.Coinbase)
                    InventoryManager.MemoryPool.Add(tx.Id, tx.Original);

                tx.ExecInfo.RedeemedOutputs.ForEach(x => utxo.Add(x, x));
                tx.ExecInfo.GeneratedOutputs.ForEach(x => utxo.Remove(x));
            }

            latest = Blocks[block.PreviousHash];
        }

        void PurgeBlock(ByteString id)
        {
            Block block;
            if (Blocks.TryGetValue(id, out block))
            {
                // The block to be purged must not be executed yet.
                Debug.Assert(block.ParsedTransactions.IsNull());
                Blocks.Remove(id);
            }

            List<ByteString> blocks;
            if (floatingBlocks.TryGetValue(id, out blocks))
            {
                floatingBlocks.Remove(id);

                // Also purge descendant blocks of the purging block.
                blocks.ForEach(PurgeBlock);
            }

            InventoryManager.Blocks.Remove(id);
        }

        void Run(Block block)
        {
            logger.LogDebug($@"Attempt to run Block:{
                block.Id.ToString().Substring(0, 7)}");

            Debug.Assert(latest.Id.Equals(block.PreviousHash));
            if (!block.ParsedTransactions.IsNull()) return;

            var blockTime = block.Timestamp;
            var txCount = block.Transactions.Count;
            var rootTxHash = BlockchainUtil.RootHashTransactionIds(
                block.TransactionIds);
            var difficulty = BlockParameter.GetNextDifficulty(
                block.Ancestors(Blocks).Skip(1));

            // Block header validity check.

            // The block timestamp must be the past time after previous block.
            // The block must contain at least one transaction: coinbase.
            // The number of transaction IDs and actual data must match.
            // Transaction Root must be the hash of all TX IDs concatenated.
            // The block must have a pointer to the previous block.
            // The block's difficulty must be within the computed range.
            // The Block ID has greater difficulty than the computed difficulty.
            if (blockTime > DateTime.UtcNow ||
                blockTime < latest.Timestamp ||
                txCount == 0 || txCount != block.TransactionIds.Count ||
                !rootTxHash.SequenceEqual(block.TransactionRootHash) ||
                !latest.Id.Equals(block.PreviousHash) ||
                block.Difficulty >= difficulty * (1 + 1e-15) ||
                block.Difficulty <= difficulty * (1 - 1e-15) ||
                Hash.Difficulty(block.Id.ToByteArray()) < block.Difficulty)
            {
                throw new ArgumentException();
            }

            // Deserialize transactions and check IDs.
            var transactions = new Transaction[txCount];
            for (var i = 0; i < txCount; i++)
            {
                var tx = BlockchainUtil.DeserializeTransaction(
                    block.Transactions[i]);

                if (!tx.Id.Equals(block.TransactionIds[i]))
                    throw new ArgumentException();
                transactions[i] = tx;
            }

            // Run normal transactions.
            ulong coinbase = BlockParameter.GetCoinbase(latest.Height + 1);
            var spent = new List<TransactionOutput>();
            for (var i = 1; i < txCount; i++)
            {
                Run(transactions[i], blockTime, spentTxo: spent);

                // Collect all transaction fees to pay to miner.  Accumulate
                // spent transaction outputs.
                var exec = transactions[i].ExecInfo;
                coinbase += exec.TransactionFee;
                spent.AddRange(exec.RedeemedOutputs);
            }

            // Run the coinbase transaction.
            Run(transactions[0], blockTime, coinbase);

            block.Height = latest.Height + 1;
            block.ParsedTransactions = transactions;
            block.TotalDifficulty = latest.TotalDifficulty + block.Difficulty;
        }

        void Run(Transaction tx,
            DateTime blockTime, ulong coinbase = 0,
            List<TransactionOutput> spentTxo = null)
        {
            logger.LogDebug($@"Attempt to run TX:{
                tx.Id.ToString().Substring(0, 7)}");

            // Transaction header validity check.
            if (tx.Timestamp >= blockTime ||
                !(coinbase == 0 ^ tx.InEntries.Count == 0))
            {
                throw new ArgumentException();
            }

            // In-Entry validity check.
            ulong inSum = coinbase;
            var redeemed = new List<TransactionOutput>();
            var signHash = BlockchainUtil.GetTransactionSignHash(tx.Original);
            foreach (var inEntry in tx.InEntries)
            {
                // Signature check.
                var verified = EccService.Verify(
                    signHash, inEntry.Signature, inEntry.PublicKey);

                // UTXO check. The transaction output must not be spent by
                // previous transactions.
                var txo = new TransactionOutput
                {
                    TransactionId = inEntry.TransactionId,
                    OutIndex = inEntry.OutEntryIndex,
                };
                var unspent =
                    !(spentTxo?.Contains(txo) ?? false) &&
                    utxo.TryGetValue(txo, out txo);

                // Recipient address check.
                // NOTE: In Bitcoin, recipient address is computed by RIPEMD160.
                var addr = Hash.ComputeSHA256(inEntry.PublicKey);
                var redeemable = txo.Recipient.Equals(addr);

                // Sum all the reedemable.
                inSum = checked(inSum + txo.Amount);

                if (!verified || !unspent || !redeemable)
                    throw new ArgumentException();
                redeemed.Add(txo);
            }

            // Out-entry validity check.
            ulong outSum = 0;
            ushort outIndex = 0;
            var generated = new List<TransactionOutput>();
            foreach (var outEntry in tx.OutEntries)
            {
                if (outEntry.RecipientHash.IsNull() || outEntry.Amount <= 0)
                    throw new ArgumentException();

                // Sum all the transferred.
                outSum = checked(outSum + outEntry.Amount);

                // Create new UTXO entry.
                generated.Add(new TransactionOutput
                {
                    TransactionId = tx.Id,
                    OutIndex = outIndex++,
                    Recipient = outEntry.RecipientHash,
                    Amount = outEntry.Amount,
                });
            }

            // Output exceeds input or coinbase.
            if (outSum > inSum) throw new ArgumentException();

            tx.ExecInfo = new TransactionExecInformation
            {
                Coinbase = coinbase != 0,
                RedeemedOutputs = redeemed,
                GeneratedOutputs = generated,
                TransactionFee = inSum - outSum,
            };
        }
    }
}
