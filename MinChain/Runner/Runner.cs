using Microsoft.Extensions.Logging;
using System;
using System.Net;

namespace MinChain
{
    public partial class Runner
    {
        static readonly ILogger logger = Logging.Logger<Runner>();

        static ConnectionManager connectionManager;

        public static void Run(string[] args)
        {
            connectionManager = new ConnectionManager();
            connectionManager.Start(
                new IPEndPoint(IPAddress.Any, int.Parse(args[0])));

            Console.ReadLine();

            connectionManager.Dispose();
        }
    }
}
