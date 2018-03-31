using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;

namespace MinChain
{
    public class Configurator
    {
        public const int DefaultPort = 9333;
        public const int DefaultWebApiPort = 8080;

        public static void Exec(string[] args)
        {
            var defaultRemote = new IPEndPoint(IPAddress.Loopback, DefaultPort);
            var json = JsonConvert.SerializeObject(
                new Configuration
                {
                    ListenOn = new IPEndPoint(IPAddress.Any, DefaultPort),
                    InitialEndpoints = new[] { defaultRemote },
                    WebApiPort = DefaultWebApiPort,
                    KeyPairPath = "<YOUR OWN KEYPAIR>.json",
                    GenesisPath = "<GENESIS BLOCK>.bin",
                    StoragePath = Path.Combine(Environment.CurrentDirectory, "blocks"),
                    Mining = true,
                },
                Formatting.Indented);
            Console.WriteLine(json);
        }
    }
}
