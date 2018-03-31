using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static MessagePack.MessagePackSerializer;

namespace MinChain
{
    public partial class Runner
    {
        static readonly ILogger logger = Logging.Logger<Runner>();

        public static void Run(string[] args) =>
            new Runner().RunInternal(args);

        Configuration config;
        Wallet wallet;
        Block genesis;

        ConnectionManager connectionManager;
        InventoryManager inventoryManager;
        Executor executor;
        Storage storage;
        Mining miner;

        void RunInternal(string[] args)
        {
            if (!LoadConfiguration(args)) return;

            connectionManager = new ConnectionManager();
            inventoryManager = new InventoryManager();
            executor = new Executor();
            miner = new Mining(config.MiningDegreeOfParallelism);

            connectionManager.NewConnectionEstablished += NewPeer;
            connectionManager.MessageReceived += HandleMessage;
            executor.BlockExecuted += _ => miner.Notify();

            inventoryManager.ConnectionManager = connectionManager;
            inventoryManager.Executor = executor;
            executor.InventoryManager = inventoryManager;
            miner.ConnectionManager = connectionManager;
            miner.InventoryManager = inventoryManager;
            miner.Executor = executor;

            inventoryManager.Blocks.Add(genesis.Id, genesis.Original);
            executor.ProcessBlock(genesis);

            if (!storage.IsNull())
            {
                executor.BlockExecuted +=
                    block => storage.Save(block.Id, block.Original);

                foreach ((var id, var data) in storage.LoadAll())
                    inventoryManager.TryLoadBlock(id, data);
            }

            connectionManager.Start(config.ListenOn);
            var t = Task.Run(async () =>
            {
                foreach (var ep in config.InitialEndpoints)
                    await connectionManager.ConnectToAsync(ep);
            });

            if (config.Mining)
            {
                miner.RecipientAddress = wallet.Address;
                miner.Start();
            }

            if (config.WebApiPort.HasValue)
            {
                var handler = new WebApiHandler(config, connectionManager,
                    inventoryManager, executor, miner);

                var webHost = new WebHostBuilder()
                    .UseKestrel()
                    .UseUrls($"http://*:{config.WebApiPort}")
                    .Configure(app => app.Run(handler.HandleWebRequest))
                    .Build();
                webHost.RunAsync();
            }

            ReadConsole();

            connectionManager.Dispose();
        }

        void ReadConsole()
        {
        Start:
            var line = Console.ReadLine();
            var input = line.Split(new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);

            switch (input.Length != 0 ? input[0] : null)
            {
                case "balance":
                    Console.WriteLine(wallet.GetBalance(executor.Utxos));
                    break;

                case "send":
                    try
                    {
                        var recipient = Convert.FromBase64String(input[1]);
                        var amount = ulong.Parse(input[2]);
                        var tx = wallet.SendTo(executor.Utxos,
                            ByteString.CopyFrom(recipient), amount);

                        inventoryManager.HandleMessage(new InventoryMessage
                        {
                            Type = InventoryMessageType.Body,
                            IsBlock = false,
                            ObjectId = tx.Id,
                            Data = tx.Original,
                        }, -1);
                    }
                    catch (Exception e) { Console.WriteLine(e); }
                    break;

                case "exit":
                case "quit":
                    return;
            }

            goto Start;
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
                var myKey = KeyPair.LoadFrom(config.KeyPairPath);
                wallet = new Wallet(myKey);
            }
            catch (Exception exp)
            {
                logger.LogError(
                    $"Failed to load key from {config.KeyPairPath}.",
                    exp);
                return false;
            }

            try
            {
                var bytes = File.ReadAllBytes(config.GenesisPath);
                genesis = BlockchainUtil.DeserializeBlock(bytes);
            }
            catch (Exception exp)
            {
                logger.LogError(
                    $"Failed to load the genesis from {config.GenesisPath}.",
                    exp);
                return false;
            }

            try
            {
                if (!string.IsNullOrEmpty(config.StoragePath))
                    storage = new Storage(config.StoragePath);
            }
            catch (Exception exp)
            {
                logger.LogError(
                    $@"Failed to set up blockchain storage at {
                        config.StoragePath}.",
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
                Genesis = genesis.Id,
                KnownBlocks = executor.Blocks.Keys.ToList(),
                MyPeers = peers.ToList(),
            }, peerId);
        }

        Task HandleMessage(Message message, int peerId)
        {
            switch (message.Type)
            {
                case MessageType.Hello:
                    return HandleHello(
                        Deserialize<Hello>(message.Payload),
                        peerId);

                case MessageType.Inventory:
                    return inventoryManager.HandleMessage(
                        Deserialize<InventoryMessage>(message.Payload),
                        peerId);

                default: return Task.CompletedTask;
            }
        }

        async Task HandleHello(Hello hello, int peerId)
        {
            // Check if the peer is on the same network.
            if (!genesis.Id.Equals(hello.Genesis))
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
            foreach (var message in messages)
                await connectionManager.SendAsync(message, peerId);
        }
    }
}
