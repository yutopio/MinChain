using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Console;

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
        static readonly Dictionary<string, Action<string[]>> commands =
            new Dictionary<string, Action<string[]>>
            {
                { "genkey", KeyGenerator.Exec },
                { "config", Configurator.Exec },
                { "genesis", Genesis.Exec },
                { "run", Runner.Run },
            };

        public static void Main(string[] args)
        {
            Logging.Factory.AddConsole(LogLevel.Debug);

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Converters = new List<JsonConverter>
                {
                    new IPEndPointConverter()
                }
            };

            ByteString.CopyFrom(new byte[0]);

            if (args.Length == 0)
            {
                WriteLine("No command provided.");
                goto ListCommands;
            }

            Action<string[]> func;
            var cmd = (args[0] ?? string.Empty).ToLower();
            if (!commands.TryGetValue(cmd, out func))
            {
                WriteLine($"Command '{cmd}' not found.");
                goto ListCommands;
            }

            func(args.Skip(1).ToArray());
            return;

        ListCommands:
            WriteLine("List of commands are:");
            commands.Keys.ToList().ForEach(name => WriteLine($"\t{name}"));
        }
    }
}
