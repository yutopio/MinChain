using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MinChain
{
    // https://gist.github.com/yutopio/63532fe20efcf7f5173c6c3c71070af3
    public static class StreamExtensions
    {
        public static async Task<byte[]> ReadAsync(this Stream stream,
            int length, CancellationToken cancellationToken)
        {
            var ret = new byte[length];
            for (var count = 0; count < length;)
            {
                count += await stream.ReadAsync(
                    ret, count, length - count, cancellationToken);
            }

            return ret;
        }

        public static Task WriteAsync(this Stream stream,
            byte[] buffer, CancellationToken cancellationToken)
        {
            return stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        }

        public static async Task<byte[]> ReadChunkAsync(this Stream stream,
            CancellationToken cancellationToken)
        {
            var length = BitConverter.ToInt32(
                await stream.ReadAsync(4, cancellationToken), 0);
            return await stream.ReadAsync(length, cancellationToken);
        }

        public static async Task WriteChunkAsync(this Stream stream,
            byte[] buffer, CancellationToken cancellationToken)
        {
            var length = BitConverter.GetBytes(buffer.Length);
            await stream.WriteAsync(length, cancellationToken);
            await stream.WriteAsync(buffer, cancellationToken);
        }
    }
}
