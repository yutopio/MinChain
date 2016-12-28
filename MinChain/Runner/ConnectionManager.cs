using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MinChain
{
    public class ConnectionManager : IDisposable
    {
        static readonly ILogger logger = Logging.Logger<ConnectionManager>();

        public const int ListenBacklog = 20;

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

                    logger.LogInformation($@"Accepted peer from {
                        peer.Client.RemoteEndPoint}");

                    await AddPeer(peer);
                }
            }
        }

        public async Task ConnectToAsync(IPEndPoint endpoint)
        {
            var peer = new TcpClient(AddressFamily.InterNetwork);
            try { await peer.ConnectAsync(endpoint.Address, endpoint.Port); }
            catch (SocketException exp)
            {
                logger.LogInformation($"Failed to connect to {endpoint}.", exp);
                return;
            }

            await AddPeer(peer);
        }

        async Task AddPeer(TcpClient peer)
        {
            peers.Add(peer);

            await peer.GetStream().WriteChunkAsync(
                Encoding.ASCII.GetBytes("Hello, Goodbye"), token);
            peer.Dispose();
        }
    }
}
