using MessagePack;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using System;
using System.Security.Cryptography;

namespace MinChain
{
    public static class EccService
    {
        // NIST P-256 Curve (aka. secp256r1)
        public const string CurveName = "secp256r1";
        public static readonly ECCurve Curve = ECCurve.NamedCurves.nistP256;

        public static void GenerateKey(
            out byte[] privateKey, out byte[] publicKey)
        {
            var rnd = new SecureRandom();
            rnd.SetSeed(DateTime.UtcNow.Ticks);

            var curve = SecNamedCurves.GetByName(CurveName);
            var n = curve.N;

            BigInteger d;
            do
            {
                d = new BigInteger(n.BitLength, rnd).SetBit(n.BitLength - 1);
            } while (d.CompareTo(n) >= 0);
            privateKey = d.ToByteArrayUnsigned();

            var pubPoint = curve.G.Multiply(d).Normalize();
            publicKey = ToBytes(new ECPoint
            {
                X = pubPoint.XCoord.GetEncoded(),
                Y = pubPoint.YCoord.GetEncoded(),
            });
        }

        public static byte[] Sign(
            byte[] hash, byte[] privateKey, byte[] publicKey)
        {
            var param = new ECParameters
            {
                D = privateKey,
                Q = ToEcPoint(publicKey),
                Curve = Curve,
            };

            using (var dsa = ECDsa.Create(param))
                return dsa.SignHash(hash);
        }

        public static bool Verify(
            byte[] hash, byte[] signature, byte[] publicKey)
        {
            var param = new ECParameters
            {
                Q = ToEcPoint(publicKey),
                Curve = Curve,
            };

            using (var dsa = ECDsa.Create(param))
                return dsa.VerifyHash(hash, signature);
        }

        public static bool TestKey(byte[] privateKey, byte[] publicKey)
        {
            byte[] testHash;
            using (var sha = SHA256.Create())
                testHash = sha.ComputeHash(new byte[0]);

            try
            {
                var signature = Sign(testHash, privateKey, publicKey);
                return Verify(testHash, signature, publicKey);
            }
            catch { return false; }
        }

        static byte[] ToBytes(ECPoint point)
        {
            return MessagePackSerializer.Serialize(
                new FormattableEcPoint { X = point.X, Y = point.Y });
        }

        static ECPoint ToEcPoint(byte[] bytes)
        {
            var pt = MessagePackSerializer
                .Deserialize<FormattableEcPoint>(bytes);
            return new ECPoint { X = pt.X, Y = pt.Y };
        }

        [MessagePackObject]
        public class FormattableEcPoint
        {
            [Key(0)]
            public virtual byte[] X { get; set; }

            [Key(1)]
            public virtual byte[] Y { get; set; }
        }
    }
}
