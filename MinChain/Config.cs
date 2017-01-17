using Newtonsoft.Json;

namespace MinChain
{
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
    }
}
