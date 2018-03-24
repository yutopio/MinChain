using System;
using System.Collections.Generic;
using static MessagePack.MessagePackSerializer;

namespace MinChain
{
    public class Wallet
    {
        readonly KeyPair keyPair;
        public ByteString Address { get; }

        public Wallet(KeyPair keyPair)
        {
            this.keyPair = keyPair;
            Address = ByteString.CopyFrom(keyPair.Address);
        }

        public ulong GetBalance(HashSet<TransactionOutput> utxos)
        {
            ulong sum = 0;
            foreach (var utxo in utxos)
            {
                if (!utxo.Recipient.Equals(Address)) continue;
                sum += utxo.Amount;
            }
            return sum;
        }

        public Transaction SendTo(
            HashSet<TransactionOutput> utxos,
            ByteString recipient, ulong amount)
        {
            // TODO: You should consider transaction fee.

            // Extract my spendable UTXOs.
            ulong sum = 0;
            var inEntries = new List<InEntry>();
            foreach (var utxo in utxos)
            {
                if (!utxo.Recipient.Equals(Address)) continue;
                inEntries.Add(new InEntry
                {
                    TransactionId = utxo.TransactionId,
                    OutEntryIndex = utxo.OutIndex,
                });

                sum += utxo.Amount;
                if (sum >= amount) goto CreateOutEntries;
            }

            throw new ArgumentException(
                "Insufficient fund.", nameof(amount));

        CreateOutEntries:
            // Create list of out entries.  It should contain fund transfer and
            // change if necessary.  Also the sum of outputs must be less than
            // that of inputs.  The difference will be collected as transaction
            // fee.
            var outEntries = new List<OutEntry>
            {
                new OutEntry
                {
                    RecipientHash = recipient,
                    Amount = amount,
                },
            };

            var change = sum - amount;
            if (change != 0)
            {
                outEntries.Add(new OutEntry
                {
                    RecipientHash = Address,
                    Amount = change,
                });
            }

            // Construct to-be-signed transaction.
            var transaction = new Transaction
            {
                Timestamp = DateTime.UtcNow,
                InEntries = inEntries,
                OutEntries = outEntries,
            };

            // Take a transaction signing hash and sign against it.  Since
            // wallet contains a single key pair, single signing is sufficient.
            var signHash = BlockchainUtil.GetTransactionSignHash(
                Serialize(transaction));
            var signature = EccService.Sign(
                signHash, keyPair.PrivateKey, keyPair.PublicKey);
            foreach (var inEntry in inEntries)
            {
                inEntry.PublicKey = keyPair.PublicKey;
                inEntry.Signature = signature;
            }

            var bytes = Serialize(transaction);
            return BlockchainUtil.DeserializeTransaction(bytes);
        }
    }
}
