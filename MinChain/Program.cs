using Microsoft.Extensions.Logging;
using System;
using System.Net;

namespace MinChain
{
    // Taken from https://msdn.microsoft.com/magazine/mt694089.aspx
    public static class Logging
    {
        public static ILoggerFactory Factory { get; } = new LoggerFactory();
        public static ILogger Logger<T>() => Factory.CreateLogger<T>();
    }

    public class Program
    {
        static ConnectionManager connectionManager;

        public static void Main(string[] args)
        {
            Logging.Factory.AddConsole(LogLevel.Debug);

            connectionManager = new ConnectionManager();
            connectionManager.Start(
                new IPEndPoint(IPAddress.Any, int.Parse(args[0])));

            Console.ReadLine();

            connectionManager.Dispose();
        }
    }
}
