using Newtonsoft.Json;
using System;

namespace MinChain
{
    public class KeyGenerator
    {
        public static void Exec(string[] args)
        {
            byte[] publicKey;
            byte[] privateKey;
            EccService.GenerateKey(out privateKey, out publicKey);

            var json = JsonConvert.SerializeObject(
                new KeyPair
                {
                    PrivateKey = privateKey,
                    PublicKey = publicKey,
                    Address = BlockchainUtil.ToAddress(publicKey),
                },
                Formatting.Indented);
            Console.WriteLine(json);
        }
    }
}
