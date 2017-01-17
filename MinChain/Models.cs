using System;
using System.Collections.Generic;
using ZeroFormatter;

namespace MinChain
{
    [ZeroFormattable]
    public class Block
    {
        [IgnoreFormat]
        public byte[] Original { get; set; }

        [IgnoreFormat]
        public ByteString Id { get; set; }

        [Index(0)]
        public virtual ByteString PreviousHash { get; set; }

        [Index(1)]
        public virtual double Difficulty { get; set; }

        [Index(2)]
        public virtual ulong Nonce { get; set; }

        [Index(3)]
        public virtual DateTime Timestamp { get; set; }

        [Index(4)]
        public virtual byte[] TransactionRootHash { get; set; }

        [Index(5)]
        public virtual IList<ByteString> TransactionIds { get; set; }

        [Index(6)]
        public virtual IList<byte[]> Transactions { get; set; }

        [IgnoreFormat]
        public int Height { get; set; }

        [IgnoreFormat]
        public Transaction[] ParsedTransactions { get; set; }

        [IgnoreFormat]
        public double TotalDifficulty { get; set; }
    }

    [ZeroFormattable]
    public class Transaction
    {
        [IgnoreFormat]
        public byte[] Original { get; set; }

        [IgnoreFormat]
        public ByteString Id { get; set; }

        [Index(0)]
        public virtual DateTime Timestamp { get; set; }

        [Index(1)]
        public virtual IList<InEntry> InEntries { get; set; }

        [Index(2)]
        public virtual IList<OutEntry> OutEntries { get; set; }

        [IgnoreFormat]
        public TransactionExecInformation ExecInfo { get; set; }
    }

    [ZeroFormattable]
    public class InEntry
    {
        [Index(0)]
        public virtual ByteString TransactionId { get; set; }

        [Index(1)]
        public virtual ushort OutEntryIndex { get; set; }

        [Index(2)]
        public virtual byte[] PublicKey { get; set; }

        [Index(3)]
        public virtual byte[] Signature { get; set; }
    }

    [ZeroFormattable]
    public class OutEntry
    {
        [Index(0)]
        public virtual ByteString RecipientHash { get; set; }

        [Index(1)]
        public virtual ulong Amount { get; set; }
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
