using System.Security.Cryptography;

namespace MinChain
{
    public static class EccService
    {
        public static readonly ECCurve Curve = ECCurve.NamedCurves.nistP256;

        public static void GenerateKey(
            out byte[] privateKey, out ECPoint publicKey)
        {
            ECParameters param;
            using (var dsa = ECDsa.Create(Curve))
                param = dsa.ExportParameters(true);

            privateKey = param.D;
            publicKey = param.Q;
        }

        public static byte[] Sign(
            byte[] hash, byte[] privateKey, ECPoint publicKey)
        {
            var param = new ECParameters
            {
                D = privateKey,
                Q = publicKey,
                Curve = Curve,
            };

            using (var dsa = ECDsa.Create(param))
                return dsa.SignHash(hash);
        }

        public static bool Verify(
            byte[] hash, byte[] signature, ECPoint publicKey)
        {
            var param = new ECParameters
            {
                Q = publicKey,
                Curve = Curve,
            };

            using (var dsa = ECDsa.Create(param))
                return dsa.VerifyHash(hash, signature);
        }
    }
}
