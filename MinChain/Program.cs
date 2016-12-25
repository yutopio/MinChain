using Microsoft.Extensions.Logging;

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
        static readonly ILogger logger = Logging.Logger<Program>();

        public static void Main(string[] args)
        {
            Logging.Factory.AddConsole(LogLevel.Debug);
            logger.LogInformation("Hello world!");
        }
    }
}
