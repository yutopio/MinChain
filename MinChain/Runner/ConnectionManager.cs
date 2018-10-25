using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static MessagePack.MessagePackSerializer;

namespace MinChain
{
    public class ConnectionManager : IDisposable
    {
        static readonly ILogger logger = Logging.Logger<ConnectionManager>();

        public const int ListenBacklog = 20;

        public event Action<int> NewConnectionEstablished;
        public event Func<Message, int, Task> MessageReceived;

        readonly List<ConnectionInfo> peers = new List<ConnectionInfo>();

        Task listenTask;
        CancellationTokenSource tokenSource;
        CancellationToken token;
        SemaphoreSlim sendLock;

        class ConnectionInfo
        {
            public ConnectionInfo(TcpClient tcpClient)
            {
                Client = tcpClient;
                Stream = tcpClient.GetStream();
            }

            public TcpClient Client { get; }
            public NetworkStream Stream { get; }
            public Task LastWrite { get; set; } = Task.CompletedTask;
        }

        public void Start(IPEndPoint localEndpoint = null)
        {
            tokenSource = new CancellationTokenSource();
            token = tokenSource.Token;

            if (localEndpoint != null) listenTask = Listen(localEndpoint);

            sendLock = new SemaphoreSlim(1);
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

            peers.ForEach(x => x?.Client.Dispose());
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

            var tcs = new TaskCompletionSource<int>();
            using (token.Register(tcs.SetCanceled))
            {
                while (!token.IsCancellationRequested)
                {
                    var acceptTask = listener.AcceptTcpClientAsync();
                    if ((await Task.WhenAny(acceptTask, tcs.Task)).IsCanceled) break;

                    TcpClient peer;
                    try { peer = acceptTask.Result; }
                    catch (SocketException exp)
                    {
                        logger.LogInformation(
                            "Failed to accept new client.", exp);
                        continue;
                    }

                    var readLoop = AddPeer(peer);
                }
            }

            listener.Stop();
        }

        public async Task ConnectToAsync(
            IPEndPoint endpoint, bool autoReconnect = false)
        {
            var cl = new TcpClient(AddressFamily.InterNetwork);
            try { await cl.ConnectAsync(endpoint.Address, endpoint.Port); }
            catch (SocketException exp)
            {
                logger.LogInformation(
                    $"Failed to connect to {endpoint}." +
                    (autoReconnect ? "  Retry in 30 seconds." : ""),
                    exp);

                if (autoReconnect)
                {
                    // Create another task to retry.
                    var ignored = Task.Delay(TimeSpan.FromSeconds(30))
                        .ContinueWith(_ => ConnectToAsync(endpoint, true));
                }

                return;
            }

            var readLoop = AddPeer(cl);
            if (autoReconnect)
            {
                // Retry immediately if read loop exited (disconnected).
                var ignored = readLoop
                    .ContinueWith(_ => ConnectToAsync(endpoint, true));
            }
        }

        Task AddPeer(TcpClient peer)
        {
            var connectionInfo = new ConnectionInfo(peer);

            int id;
            lock (peers)
            {
                id = peers.Count;
                peers.Add(connectionInfo);
            }

            return Task.Run(async () =>
            {
                NewConnectionEstablished(id);
                await ReadLoop(connectionInfo, id);
            });
        }

        async Task ReadLoop(ConnectionInfo connection, int peerId)
        {
            logger.LogInformation($@"Peer #{peerId} connected to {
                connection.Client.Client.RemoteEndPoint}.");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var d = await connection.Stream.ReadChunkAsync(token);
                    var msg = Deserialize<Message>(d);
                    await MessageReceived(msg, peerId);
                }
            }
            finally
            {
                logger.LogInformation($"Peer #{peerId} disconnected.");

                peers[peerId] = null;
                connection.Client.Dispose();
            }
        }

        public Task SendAsync(Message message, int peerId)
        {
            var peer = peerId < 0 || peerId >= peers.Count ?
                null : peers[peerId];
            return peer.IsNull() ?
                Task.CompletedTask :
                SendAsync(message, peer);
        }

        public Task BroadcastAsync(Message message, int? exceptPeerId = null)
        {
            return Task.WhenAll(
                from peer in peers.Where((_, i) => i != exceptPeerId)
                where !peer.IsNull()
                select SendAsync(message, peer));
        }

        async Task SendAsync(Message message, ConnectionInfo connection)
        {
            // This method may be called concurrently.
            var bytes = Serialize(message);

            try
            {
                await sendLock.WaitAsync(token);
                await connection.Stream.WriteChunkAsync(bytes, token);
            }
            finally { sendLock.Release(); }
        }

        public IEnumerable<EndPoint> GetPeers()
        {
            return peers
                .Select(x => x?.Client.Client.RemoteEndPoint as IPEndPoint)
                .Where(x => !x.IsNull());
        }

        public void Close(int peerId)
        {
            var peer = peers[peerId];
            if (peer.IsNull())
            {
                peers[peerId] = null;
                peer.Client.Dispose();
            }
        }
    }
}
