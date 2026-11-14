using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Kalyeah.Net;
using Kalyeah.Sim;

namespace Kalyeah.Server
{
    /// <summary>
    /// The headless dedicated server (port of server/main.gd and the Dockerfile's role): loads the rules from unity/Data, listens for
    /// WebSocket clients on $PORT (default: net_rules.default_port, 8080) and polls the GameServer about 60 times a second until stopped.
    /// Godot clients and Unity clients both connect to it (the wire format is Godot's).
    /// Run: `dotnet run --project unity/Kalyeah.Server [-- --port N] [--data DIR]`.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            string dataDir = Environment.GetEnvironmentVariable("KALYEAH_DATA");
            int port = -1;
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "--port" && int.TryParse(args[i + 1], out int p)) port = p;
                if (args[i] == "--data") dataDir = args[i + 1];
            }
            dataDir ??= FindData();
            if (dataDir == null || !Directory.Exists(dataDir))
            {
                Console.Error.WriteLine("[server] no rules data: set KALYEAH_DATA or pass --data unity/Data");
                return 1;
            }
            SimData data = SimData.Load(dataDir);
            NetRules net = NetRules.FromData(data);
            if (port < 0) port = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out int fromEnv) ? fromEnv : net.DefaultPort;

            var server = new GameServer(data.GameRules, data.MapLayout, net, Stopwatch.GetTimestamp(), new StreetCatalog(data));
            server.LogMessage += text => Console.WriteLine("[server] " + text);
            try
            {
                server.Listen(port);
            }
            catch (System.Net.Sockets.SocketException e)
            {
                Console.Error.WriteLine("[server] could not listen on port " + port + " (" + e.Message + ")");
                return 1;
            }

            using var stop = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                stop.Cancel();
            };
            AppDomain.CurrentDomain.ProcessExit += (_, _) => stop.Cancel();

            var clock = Stopwatch.StartNew();
            double last = 0.0;
            while (!stop.IsCancellationRequested)
            {
                double now = clock.Elapsed.TotalSeconds;
                server.Poll(now - last);
                last = now;
                Thread.Sleep(16);
            }
            server.CloseRooms();
            server.Poll(0.0);
            Thread.Sleep(200);
            server.Stop();
            return 0;
        }

        /// <summary>The Data folder copied next to the program, else unity/Data above the working directory.</summary>
        static string FindData()
        {
            string beside = Path.Combine(AppContext.BaseDirectory, "Data");
            if (Directory.Exists(beside)) return beside;
            for (string dir = Directory.GetCurrentDirectory(); dir != null; dir = Path.GetDirectoryName(dir))
            {
                string candidate = Path.Combine(dir, "unity", "Data");
                if (Directory.Exists(candidate)) return candidate;
            }
            return null;
        }
    }
}
