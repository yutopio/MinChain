using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using static ZeroFormatter.ZeroFormatterSerializer;

namespace MinChain
{
    public partial class Runner
    {
        static readonly ILogger logger = Logging.Logger<Runner>();

        public static void Run(string[] args) =>
            new Runner().RunInternal(args);

        readonly ByteString genesis;

        ConnectionManager connectionManager;
        InventoryManager inventoryManager;
        Executor executor;

        void RunInternal(string[] args)
        {
            connectionManager = new ConnectionManager();
            inventoryManager = new InventoryManager();
            executor = new Executor();

            connectionManager.NewConnectionEstablished += NewPeer;
            connectionManager.MessageReceived += HandleMessage;

            inventoryManager.ConnectionManager = connectionManager;
            inventoryManager.Executor = executor;
            executor.InventoryManager = inventoryManager;

            connectionManager.Start(
                new IPEndPoint(IPAddress.Any, int.Parse(args[0])));

            Console.ReadLine();

            connectionManager.Dispose();
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
