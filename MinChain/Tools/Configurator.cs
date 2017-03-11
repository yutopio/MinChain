using Newtonsoft.Json;
using System;
using System.Net;

namespace MinChain
{
    public class Configurator
    {
        public const int DefaultPort = 9333;

        public static void Exec(string[] args)
        {
            var defaultRemote = new IPEndPoint(IPAddress.Loopback, DefaultPort);
            var json = JsonConvert.SerializeObject(
                new Configuration
                {
                    ListenOn = new IPEndPoint(IPAddress.Any, DefaultPort),
                    InitialEndpoints = new[] { defaultRemote },
                    KeyPairPath = "<YOUR OWN KEYPAIR>.json",
                    GenesisPath = "<GENESIS BLOCK>.bin",
                    Mining = true,
                },
                Formatting.Indented);
            Console.WriteLine(json);
        }
    }
}
