using System;
using System.Net.Sockets;
using Kalyeah.Net;

namespace Kalyeah.UI
{
    /// <summary>
    /// The network side of the whole app (port of scripts/autoload/net.gd): one GameClient that outlives the screens (room, loading,
    /// match), and, on the phone that hosts a Local room, the room server and the beacon that answers the guests' "who is hosting?"
    /// probe. The Unity side calls `Poll` every frame from an object kept across scenes (`MenuServices`).
    /// </summary>
    public sealed class NetHub
    {
        /// <summary>A host that just stopped keeps running this long, so its ROOM_CLOSED reaches the players.</summary>
        public const double CloseGrace = 0.5;

        public readonly MenuData Data;
        public readonly GameClient Client;
        /// <summary>The room server this phone runs for a Local room it hosts, or null.</summary>
        public GameServer Host { get; private set; }
        public readonly HotspotBeacon Beacon = new HotspotBeacon();
        /// <summary>How the host serves (a WebSocket on the port by default; a MemoryHub in tests).</summary>
        public Func<int, IServerTransport> ServerTransportFactory = port => WsServerTransport.Listen(port);
        /// <summary>Whether the beacon is started with the host (off in tests that have no UDP).</summary>
        public bool UseBeacon = true;

        GameServer _closing;
        double _closingLeft;

        public NetHub(MenuData data)
        {
            Data = data;
            Client = data.NewClient();
        }

        public void Poll(double delta)
        {
            Client.Poll(delta);
            if (Host != null)
            {
                Host.Poll(delta);
                Beacon.Poll();
            }
            if (_closing != null)
            {
                _closing.Poll(delta);
                _closingLeft -= delta;
                if (_closingLeft <= 0.0) FinishClosing();
            }
        }

        /// <summary>
        /// Starts a room server on this phone for friends on its hotspot or Wi-Fi (they find it by themselves, no code). False when
        /// the port is taken. A beacon that cannot start only costs the broadcast: guests still find the room at the fallback addresses.
        /// </summary>
        public bool StartHost(long seed)
        {
            StopHost();
            FinishClosing();
            var server = new GameServer(Data.Rules, Data.Layout, Data.Net, seed, Data.Games) { AutoJoin = true };
            try
            {
                server.Attach(ServerTransportFactory(Data.Net.DefaultPort));
            }
            catch (Exception e) when (e is SocketException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                return false;
            }
            Host = server;
            if (UseBeacon) Beacon.Listen(Data.Net.DiscoveryPort, Data.Net.DiscoveryBurst);
            return true;
        }

        public void StopHost()
        {
            Beacon.Stop();
            if (Host == null) return;
            FinishClosing();
            Host.CloseRooms();
            _closing = Host;
            _closingLeft = CloseGrace;
            Host = null;
        }

        public bool IsHosting => Host != null;

        /// <summary>Where this phone's own client connects to reach the server it hosts.</summary>
        public string HostUrl() => "ws://127.0.0.1:" + Data.Net.DefaultPort;

        void FinishClosing()
        {
            if (_closing == null) return;
            _closing.Stop();
            _closing = null;
        }
    }
}
