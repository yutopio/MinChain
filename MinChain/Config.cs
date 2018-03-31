using Newtonsoft.Json;
using System.IO;
using System.Net;

namespace MinChain
{
    public class Configuration
    {
        [JsonProperty(PropertyName = "listen")]
        public IPEndPoint ListenOn { get; set; }

        [JsonProperty(PropertyName = "peers")]
        public IPEndPoint[] InitialEndpoints { get; set; }

        [JsonProperty(PropertyName = "keypair")]
        public string KeyPairPath { get; set; }

        [JsonProperty(PropertyName = "genesis")]
        public string GenesisPath { get; set; }

        [JsonProperty(PropertyName = "mining")]
        public bool Mining { get; set; }

        [JsonProperty(PropertyName = "mining_dop")]
        public uint? MiningDegreeOfParallelism { get; set; }

        public bool ShouldSerializeListenOn() => !ListenOn.IsNull();
        public bool ShouldSerializeInitialEndpoints() =>
            !InitialEndpoints.IsNullOrEmpty();
        public bool ShouldSerializeKeyPairPath() =>
            !string.IsNullOrWhiteSpace(KeyPairPath);
        public bool ShouldSerializeGenesisPath() =>
            !string.IsNullOrWhiteSpace(GenesisPath);
        public bool ShouldSerializeMiningDegreeOfParallelism() =>
            MiningDegreeOfParallelism.HasValue;
    }

    public class KeyPair
    {
        [JsonProperty(PropertyName = "pub")]
        public byte[] PublicKey { get; set; }

        [JsonProperty(PropertyName = "prv")]
        public byte[] PrivateKey { get; set; }

        [JsonProperty(PropertyName = "addr")]
        public byte[] Address { get; set; }

        public bool ShouldSerializePublicKey() => !PublicKey.IsNullOrEmpty();
        public bool ShouldSerializePrivateKey() => !PrivateKey.IsNullOrEmpty();
        public bool ShouldSerializeAddress() => !Address.IsNullOrEmpty();

        public static KeyPair LoadFrom(string path)
        {
            var keyContent = File.ReadAllText(path);
            var keyPair = JsonConvert.DeserializeObject<KeyPair>(keyContent);
            if (!EccService.TestKey(keyPair.PrivateKey, keyPair.PublicKey))
                throw new InvalidDataException("Collapsed keypair.");

            return keyPair;
        }
    }
}
