using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static ZeroFormatter.ZeroFormatterSerializer;

namespace MinChain
{
    public partial class Runner
    {
        static readonly ILogger logger = Logging.Logger<Runner>();

        public static void Run(string[] args) =>
            new Runner().RunInternal(args);

        Configuration config;
        KeyPair myKeys;
        ByteString genesis;

        ConnectionManager connectionManager;
        InventoryManager inventoryManager;
        Executor executor;

        void RunInternal(string[] args)
        {
            if (!LoadConfiguration(args)) return;

            connectionManager = new ConnectionManager();
            inventoryManager = new InventoryManager();
            executor = new Executor();

            connectionManager.NewConnectionEstablished += NewPeer;
            connectionManager.MessageReceived += HandleMessage;

            inventoryManager.ConnectionManager = connectionManager;
            inventoryManager.Executor = executor;
            executor.InventoryManager = inventoryManager;

            connectionManager.Start(config.ListenOn);
            var t = Task.Run(async () =>
            {
                foreach (var ep in config.InitialEndpoints)
                    await connectionManager.ConnectToAsync(ep);
            });

            Console.ReadLine();

            connectionManager.Dispose();
        }

        bool LoadConfiguration(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Should provide configuration file path.");
                return false;
            }

            try
            {
                config = JsonConvert.DeserializeObject<Configuration>(
                    File.ReadAllText(Path.GetFullPath(args[0])));
            }
            catch (Exception exp)
            {
                logger.LogError(
                    "Failed to load configuration file. Run 'config' command.",
                    exp);
                return false;
            }

            try
            {
                myKeys = KeyPair.LoadFrom(config.KeyPairPath);
            }
            catch (Exception exp)
            {
                logger.LogError(
                    $"Failed to load key from {config.KeyPairPath}.",
                    exp);
                return false;
            }

            return true;
        }

        void NewPeer(int peerId)
        {
            var peers = connectionManager.GetPeers()
                .Select(x => x.ToString());
            connectionManager.SendAsync(new Hello
            {
                Genesis = genesis,
                KnownBlocks = executor.Blocks.Keys.ToList(),
                MyPeers = peers.ToList(),
            }, peerId);
        }

        void HandleMessage(Message message, int peerId)
        {
            switch (message.Type)
            {
                case MessageType.Hello:
                    HandleHello(
                        Deserialize<Hello>(message.Payload),
                        peerId);
                    break;

                case MessageType.Inventory:
                    inventoryManager.HandleMessage(
                        Deserialize<InventoryMessage>(message.Payload),
                        peerId);
                    break;
            }
        }

        void HandleHello(Hello hello, int peerId)
        {
            // Check if the peer is on the same network.
            if (!genesis.Equals(hello.Genesis))
                connectionManager.Close(peerId);

            var myBlocks = new HashSet<ByteString>();
            var peerBlocks = new HashSet<ByteString>();
            foreach (var blockId in executor.Blocks.Keys) myBlocks.Add(blockId);
            foreach (var blockId in hello.KnownBlocks) peerBlocks.Add(blockId);

            var messages = peerBlocks.Except(myBlocks)
                .Select(x => new InventoryMessage
                {
                    Type = InventoryMessageType.Request,
                    ObjectId = x,
                    IsBlock = true,
                })
                .ToArray();

            // Send request for unknown blocks.
            Task.Run(async () =>
            {
                foreach (var message in messages)
                    await connectionManager.SendAsync(message, peerId);
            });
        }
    }
}
