using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace MinChain
{
    public class Block
    {
        public byte[] Original { get; set; }
        public ByteString Id { get; set; }
        public ByteString PreviousHash { get; set; }
        public double Difficulty { get; set; }
        public ulong Nonce { get; set; }
        public DateTime Timestamp { get; set; }
        public byte[] TransactionRootHash { get; set; }
        public ByteString[] TransactionIds { get; set; }
        public byte[][] Transactions { get; set; }
        public int Height { get; set; }
        public Transaction[] ParsedTransactions { get; set; }
        public double TotalDifficulty { get; set; }
    }

    public class Transaction
    {
        public byte[] Original { get; set; }
        public ByteString Id { get; set; }
        public DateTime Timestamp { get; set; }
        public InEntry[] InEntries { get; set; }
        public OutEntry[] OutEntries { get; set; }
        public TransactionExecInformation ExecInfo { get; set; }
    }

    public class InEntry
    {
        public ByteString TransactionId { get; set; }
        public ushort OutEntryIndex { get; set; }
        public ECPoint PublicKey { get; set; }
        public byte[] Signature { get; set; }
    }

    public class OutEntry
    {
        public ByteString RecipientHash { get; set; }
        public ulong Amount { get; set; }
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
