using System.Security.Cryptography;

namespace MinChain
{
    public static class Hash
    {
        public static byte[] ComputeSHA256(byte[] bytes)
        {
            using (var sha256 = SHA256.Create())
                return sha256.ComputeHash(bytes);
        }

        public static byte[] ComputeDoubleSHA256(byte[] bytes)
        {
            using (var sha256 = SHA256.Create())
                return sha256.ComputeHash(sha256.ComputeHash(bytes));
        }
    }
}
