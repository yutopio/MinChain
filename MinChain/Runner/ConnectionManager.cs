using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MinChain
{
    public class ConnectionManager : IDisposable
    {
        static readonly ILogger logger = Logging.Logger<ConnectionManager>();

        public const int ListenBacklog = 20;

        public event Action<int> NewConnectionEstablished;
        public event Action<Message, int> MessageReceived;

        readonly List<TcpClient> peers = new List<TcpClient>();

        Task listenTask;
        CancellationTokenSource tokenSource;
        CancellationToken token;

        public void Start(IPEndPoint localEndpoint = null)
        {
            tokenSource = new CancellationTokenSource();
            token = tokenSource.Token;

            if (localEndpoint != null) listenTask = Listen(localEndpoint);
        }

        public void Dispose()
        {
            if (!tokenSource.IsNull())
            {
                logger.LogInformation("Stop listening.");

                tokenSource.Cancel();
                tokenSource.Dispose();
                tokenSource = null;
            }

            peers.ForEach(x => x?.Dispose());
            peers.Clear();
        }

        async Task Listen(IPEndPoint localEndpoint)
        {
            var listener = new TcpListener(
                localEndpoint.Address, localEndpoint.Port);

            logger.LogInformation($"Start listening on {localEndpoint}");

            try { listener.Start(ListenBacklog); }
            catch (SocketException exp)
            {
                logger.LogError("Error listening server port", exp);
                return;
            }

            using (token.Register(listener.Stop))
            {
                while (!token.IsCancellationRequested)
                {
                    TcpClient peer;
                    try { peer = await listener.AcceptTcpClientAsync(); }
                    catch (SocketException exp)
                    {
                        logger.LogInformation(
                            "Failed to accept new client.", exp);
                        continue;
                    }

                    AddPeer(peer);
                }
            }
        }

        public async Task ConnectToAsync(IPEndPoint endpoint)
        {
            var cl = new TcpClient(AddressFamily.InterNetwork);
            try { await cl.ConnectAsync(endpoint.Address, endpoint.Port); }
            catch (SocketException exp)
            {
                logger.LogInformation($"Failed to connect to {endpoint}.", exp);
                return;
            }

            AddPeer(cl);
        }

        void AddPeer(TcpClient peer)
        {
            int id;
            lock (peers)
            {
                id = peers.Count;
                peers.Add(peer);
            }

            Task.Run(async () =>
            {
                NewConnectionEstablished(id);
                await ReadLoop(peer, id);
            });
        }

        async Task ReadLoop(TcpClient peer, int peerId)
        {
            logger.LogInformation($@"Peer #{peerId} connected to {
                peer.Client.RemoteEndPoint}.");

            try
            {
                var stream = peer.GetStream();
                while (!token.IsCancellationRequested)
                {
                    var d = await stream.ReadChunkAsync(token);
                    // TODO(yuto): Deserialize
                    MessageReceived(null, peerId);
                }
            }
            finally
            {
                logger.LogInformation($"Peer #{peerId} disconnected.");

                peers[peerId] = null;
                peer.Dispose();
            }
        }

        public Task SendAsync(Message message, int peerId)
        {
            var peer = peers[peerId];
            return peer.IsNull() ?
                Task.CompletedTask :
                SendAsync(message, peer.GetStream());
        }

        public Task BroadcastAsync(Message message, int? exceptPeerId = null)
        {
            return Task.WhenAll(
                from peer in peers.Where((_, i) => i != exceptPeerId)
                where !peer.IsNull()
                select SendAsync(message, peer.GetStream()));
        }

        Task SendAsync(Message message, NetworkStream stream)
        {
            // TODO(yuto): Serialize
            var bytes = new byte[] { 1, 2, 3 };
            return stream.WriteChunkAsync(bytes, token);
        }

        public IEnumerable<EndPoint> GetPeers()
        {
            return peers
                .Select(x => x?.Client.RemoteEndPoint as IPEndPoint)
                .Where(x => !x.IsNull());
        }

        public void Close(int peerId)
        {
            var peer = peers[peerId];
            if (peer.IsNull())
            {
                peers[peerId] = null;
                peer.Dispose();
            }
        }
    }
}
