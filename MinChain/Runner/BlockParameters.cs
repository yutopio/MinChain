using System;
using System.Collections.Generic;
using System.Linq;

namespace MinChain
{
    public static class BlockParameter
    {
        public static ulong GetCoinbase(int height)
        {
            // 1000000 = 0b11110100001001000000
            return height >= 2000 ? 0 :
                1000000ul >> (height / 100);
        }

        public const int BlocksToConsiderDifficulty = 10;
        public static readonly TimeSpan BlockInterval =
            TimeSpan.FromSeconds(2);

        public static double GetNextDifficulty(
            IEnumerable<Block> pastBlocks)
        {
            var blocks = pastBlocks
                .Take(BlocksToConsiderDifficulty + 1)
                .ToArray();

            // The first block after the genesis.
            var lastDiff = blocks[0].Difficulty;
            if (blocks.Length == 1) return lastDiff;

            var t = blocks.First().Timestamp - blocks.Last().Timestamp;
            var sumDiff = blocks.Reverse().Skip(1).Sum(x => x.Difficulty);
            var newDiff = sumDiff
                / t.TotalMilliseconds
                * BlockInterval.TotalMilliseconds;

            // New difficulty should be within +/- 10% of previous.
            if (newDiff < lastDiff * 0.9) return lastDiff * 0.9;
            if (newDiff > lastDiff * 1.1) return lastDiff * 1.1;
            return newDiff;
        }
    }
}
