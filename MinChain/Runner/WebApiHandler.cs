using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MinChain
{
    public class WebApiHandler
    {
        readonly Configuration config;
        readonly Wallet wallet;
        readonly ConnectionManager connectionManager;
        readonly InventoryManager inventoryManager;
        readonly Executor executor;
        readonly Mining miner;

        public WebApiHandler(
            Configuration config, Wallet wallet,
            ConnectionManager connectionManager,
            InventoryManager inventoryManager, Executor executor,
            Mining miner)
        {
            this.config = config;
            this.wallet = wallet;
            this.connectionManager = connectionManager;
            this.inventoryManager = inventoryManager;
            this.executor = executor;
            this.miner = miner;
        }

        public Task HandleWebRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json";

            Task ret;
            object response = null;
            try
            {
                var components = context.Request.Path.ToString().Split('/');
                response = HandleRoot(components.Skip(1).ToArray());
                context.Response.StatusCode = StatusCodes.Status200OK;
            }
            catch (Exception e)
            {
                response = e;
                context.Response.StatusCode =
                    StatusCodes.Status500InternalServerError;
            }
            finally
            {
                ret = context.Response.WriteAsync(
                    JsonConvert.SerializeObject(response, Formatting.Indented),
                    context.RequestAborted);
            }

            return ret;
        }

        public object HandleRoot(string[] path)
        {
            var tail = path.Skip(1).ToArray();
            switch (path.Length != 0 ? path[0] : "")
            {
                case "config": return config;
                case "peers": return connectionManager.GetPeers().ToArray();
                case "mempool": return inventoryManager.MemoryPool.Keys.ToArray();
                case "utxo": return executor.Utxos.ToArray();
                case "balance": return wallet.GetBalance(executor.Utxos);
                case "block": return HandleBlock(tail);
                case "mining": return HandleMining(tail);
                default: return null;
            }
        }

        public object HandleBlock(string[] path)
        {
            var tail = path.Skip(1).ToArray();
            switch (path.Length != 0 ? path[0] : "")
            {
                case "id":
                    try
                    {
                        var key = HexConvert.ToBytes(path[1]);
                        return executor.Blocks[ByteString.CopyFrom(key)];
                    }
                    catch { return null; }

                case "height":
                    if (!int.TryParse(path[1], out var height) || height < 0)
                        return null;

                    return BlockchainUtil
                        .Ancestors(executor.Latest, executor.Blocks)
                        .Reverse().Skip(1)
                        .ElementAtOrDefault(height);

                case "latest":
                    return executor.Latest;

                case "":
                    return BlockchainUtil
                        .Ancestors(executor.Latest, executor.Blocks)
                        .Reverse().Skip(1)
                        .Select(x => x.Id);

                default: return null;
            }
        }

        public object HandleMining(string[] path)
        {
            var tail = path.Skip(1).ToArray();
            switch (path.Length != 0 ? path[0] : "")
            {
                case "start": miner.Start(); break;
                case "stop": miner.Stop(); break;
                case "status": break;
                default: return null;
            }

            return new
            {
                miner.IsMining,
                miner.RecipientAddress,
            };
        }
    }
}
