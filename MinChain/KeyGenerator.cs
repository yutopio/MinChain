using System;
using System.Security.Cryptography;

namespace MinChain
{
    public class KeyGenerator
    {
        public static void Exec(string[] args)
        {
            ECPoint publicKey;
            byte[] privateKey;
            EccService.GenerateKey(out privateKey, out publicKey);

            Console.WriteLine(HexConvert.FromBytes(privateKey));
            Console.WriteLine(HexConvert.FromBytes(publicKey.X));
            Console.WriteLine(HexConvert.FromBytes(publicKey.Y));

            var hash = Hash.ComputeDoubleSHA256(new byte[] { 1, 2, 3 });
            var signature = EccService.Sign(hash, privateKey, publicKey);
            Console.WriteLine(HexConvert.FromBytes(signature));

            var verified = EccService.Verify(hash, signature, publicKey);
            Console.WriteLine(verified);
        }
    }
}
