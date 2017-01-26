using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using ZeroFormatter;

namespace MinChain
{
    [ZeroFormattable]
    public class Block
    {
        [IgnoreFormat, JsonIgnore]
        public byte[] Original { get; set; }

        [IgnoreFormat, JsonProperty(PropertyName = "id")]
        public ByteString Id { get; set; }

        [Index(0), JsonProperty(PropertyName = "prev")]
        public virtual ByteString PreviousHash { get; set; }

        [Index(1), JsonProperty(PropertyName = "difficulty")]
        public virtual double Difficulty { get; set; }

        [Index(2), JsonProperty(PropertyName = "nonce")]
        public virtual ulong Nonce { get; set; }

        [Index(3), JsonProperty(PropertyName = "timestamp")]
        public virtual DateTime Timestamp { get; set; }

        [Index(4), JsonProperty(PropertyName = "root")]
        public virtual byte[] TransactionRootHash { get; set; }

        [Index(5), JsonIgnore]
        public virtual IList<ByteString> TransactionIds { get; set; }

        [Index(6), JsonIgnore]
        public virtual IList<byte[]> Transactions { get; set; }

        [IgnoreFormat, JsonProperty(PropertyName = "height")]
        public int Height { get; set; }

        [IgnoreFormat, JsonProperty(PropertyName = "txs")]
        public Transaction[] ParsedTransactions { get; set; }

        [IgnoreFormat, JsonIgnore]
        public double TotalDifficulty { get; set; }

        public Block Clone() =>
            new Block
            {
                Original = Original,
                Id = Id,
                PreviousHash = PreviousHash,
                Difficulty = Difficulty,
                Nonce = Nonce,
                Timestamp = Timestamp,
                TransactionRootHash = TransactionRootHash,
                TransactionIds = TransactionIds?.ToList(),
                Transactions = Transactions?.ToList(),
                Height = Height,
                ParsedTransactions = ParsedTransactions
                    ?.Select(x => x.Clone()).ToArray(),
                TotalDifficulty = TotalDifficulty,
            };
    }

    [ZeroFormattable]
    public class Transaction
    {
        [IgnoreFormat, JsonIgnore]
        public byte[] Original { get; set; }

        [IgnoreFormat, JsonProperty(PropertyName = "id")]
        public ByteString Id { get; set; }

        [Index(0), JsonProperty(PropertyName = "timestamp")]
        public virtual DateTime Timestamp { get; set; }

        [Index(1), JsonProperty(PropertyName = "in")]
        public virtual IList<InEntry> InEntries { get; set; }

        [Index(2), JsonProperty(PropertyName = "out")]
        public virtual IList<OutEntry> OutEntries { get; set; }

        [IgnoreFormat, JsonIgnore]
        public TransactionExecInformation ExecInfo { get; set; }

        public Transaction Clone() =>
            new Transaction
            {
                Original = Original,
                Id = Id,
                Timestamp = Timestamp,
                InEntries = InEntries?.Select(x => x.Clone()).ToList(),
                OutEntries = OutEntries?.Select(x => x.Clone()).ToList(),
                ExecInfo = ExecInfo,
            };
    }

    [ZeroFormattable]
    public class InEntry
    {
        [Index(0), JsonProperty(PropertyName = "tx")]
        public virtual ByteString TransactionId { get; set; }

        [Index(1), JsonProperty(PropertyName = "i")]
        public virtual ushort OutEntryIndex { get; set; }

        [Index(2), JsonProperty(PropertyName = "pub")]
        public virtual byte[] PublicKey { get; set; }

        [Index(3), JsonProperty(PropertyName = "sig")]
        public virtual byte[] Signature { get; set; }

        public InEntry Clone() =>
            new InEntry
            {
                TransactionId = TransactionId,
                OutEntryIndex = OutEntryIndex,
                PublicKey = PublicKey,
                Signature = Signature,
            };
    }

    [ZeroFormattable]
    public class OutEntry
    {
        [Index(0), JsonProperty(PropertyName = "to")]
        public virtual ByteString RecipientHash { get; set; }

        [Index(1), JsonProperty(PropertyName = "val")]
        public virtual ulong Amount { get; set; }

        public OutEntry Clone() =>
            new OutEntry
            {
                RecipientHash = RecipientHash,
                Amount = Amount,
            };
    }

    public class TransactionExecInformation
    {
        public bool Coinbase { get; set; }
        public List<TransactionOutput> RedeemedOutputs { get; set; }
        public List<TransactionOutput> GeneratedOutputs { get; set; }
        public ulong TransactionFee { get; set; }
    }

    public class TransactionOutput :
        IComparable<TransactionOutput>, IEquatable<TransactionOutput>
    {
        public ByteString TransactionId { get; set; }
        public ushort OutIndex { get; set; }
        public ByteString Recipient { get; set; }
        public ulong Amount { get; set; }

        public override int GetHashCode() =>
            TransactionId.GetHashCode() + OutIndex;

        public override bool Equals(object obj) =>
            Equals(obj as TransactionOutput);

        public bool Equals(TransactionOutput other) =>
            !other.IsNull() &&
            OutIndex == other.OutIndex &&
            TransactionId.Equals(other.TransactionId);

        public int CompareTo(TransactionOutput other)
        {
            if (other.IsNull()) return 1;

            var comp = TransactionId.CompareTo(other.TransactionId);
            return comp != 0 ? comp : OutIndex - other.OutIndex;
        }
    }
}