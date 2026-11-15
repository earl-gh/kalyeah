using System;
using System.Collections.Generic;
using System.IO;
using Kalyeah.Net;
using Kalyeah.Sim;
using NUnit.Framework;

namespace Kalyeah.UI.Tests
{
    /// <summary>Shared test helpers: the shipped data, a scratch folder for saved files, and a MemoryHub host with clients.</summary>
    public static class UiKit
    {
        static string Root => Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../..");
        static SimData _data;
        static MenuData _menu;

        public static SimData Data => _data ??= SimData.Load(Path.Combine(Root, "Data"));
        public static MenuData Menu => _menu ??= new MenuData(Data);

        /// <summary>A fresh empty folder for this test's files.</summary>
        public static string Scratch()
        {
            string dir = Path.Combine(Path.GetTempPath(), "kalyeah-ui-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Polls everything until `done` (at most `frames` frames of 1/30 s).</summary>
        public static bool PollUntil(Func<bool> done, Action poll, int frames = 300)
        {
            for (int i = 0; i < frames; i++)
            {
                if (done()) return true;
                poll();
            }
            return done();
        }
    }

    /// <summary>A Local host phone and guests wired over a MemoryHub (no sockets).</summary>
    public sealed class LocalRig
    {
        public readonly MemoryHub Hub = new MemoryHub();
        public readonly NetHub HostPhone;
        public readonly List<GameClient> Guests = new List<GameClient>();
        int _nextPeer = 10;

        public LocalRig()
        {
            HostPhone = new NetHub(UiKit.Menu) { UseBeacon = false, ServerTransportFactory = _ => Hub };
            HostPhone.Client.TransportFactory = (url, timeout) => Hub.Connect(_nextPeer++);
        }

        public GameClient Guest(string name)
        {
            GameClient guest = UiKit.Menu.NewClient();
            guest.PlayerName = name;
            guest.TransportFactory = (url, timeout) => Hub.Connect(_nextPeer++);
            Guests.Add(guest);
            return guest;
        }

        public void Poll()
        {
            HostPhone.Poll(1.0 / 30.0);
            foreach (GameClient guest in Guests) guest.Poll(1.0 / 30.0);
        }
    }
}
