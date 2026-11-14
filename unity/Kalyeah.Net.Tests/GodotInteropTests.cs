using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Kalyeah.Sim;
using NUnit.Framework;

namespace Kalyeah.Net.Tests
{
    /// <summary>
    /// C# clients against a running Godot server (`GameServer.listen` in the Godot game). Explicit: start a Godot server first and set
    /// KALYEAH_GODOT_SERVER (e.g. ws://127.0.0.1:18432), then `dotnet test --filter GodotInterop`.
    /// </summary>
    [Explicit("needs a running Godot server (KALYEAH_GODOT_SERVER)")]
    public class GodotInteropTests
    {
        const double DT = 1.0 / 30.0;

        [Test]
        public void CSharpClientsPlayOnAGodotServer()
        {
            string url = Environment.GetEnvironmentVariable("KALYEAH_GODOT_SERVER");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("KALYEAH_GODOT_SERVER is not set");
            GameClient a = new GameClient(NetKit.Rules, NetKit.Layout, NetKit.Net, NetKit.Games) { PlayerName = "SharpA" };
            GameClient b = new GameClient(NetKit.Rules, NetKit.Layout, NetKit.Net, NetKit.Games) { PlayerName = "SharpB" };
            var clients = new[] { a, b };
            int snapshots = 0, said = 0;
            b.SnapshotApplied += () => snapshots++;
            b.Said += (_, _) => said++;
            a.ConnectTo(url);
            b.ConnectTo(url);
            bool Until(Func<bool> done, Action tick = null)
            {
                var clock = Stopwatch.StartNew();
                while (clock.Elapsed.TotalSeconds < 15)
                {
                    foreach (GameClient c in clients) c.Poll(DT);
                    tick?.Invoke();
                    if (done()) return true;
                    Thread.Sleep(15);
                }
                return false;
            }
            Assert.IsTrue(Until(() => a.IsOnline() && b.IsOnline()), "connected");
            a.CreateRoom();
            Assert.IsTrue(Until(() => a.Room.GetStr("code") != ""), "room");
            b.JoinRoom(a.Room.GetStr("code"));
            Assert.IsTrue(Until(() => b.PlayerId > 0 && a.Room.GetArr("members").Count == 2), "joined");
            a.SetReady(true);
            b.SetReady(true);
            Assert.IsTrue(Until(() => a.Room.GetBool("can_start")), "can start");
            a.StartMatch();
            Assert.IsTrue(Until(() => a.InMatch() && b.InMatch()), "loading");
            a.Loaded();
            b.Loaded();
            Assert.IsTrue(Until(() => a.Stage == "pick"), "pick");
            a.PickWeapons("bato_light", "trumpo", "migo");
            b.PickWeapons("jacks", "bola");
            Assert.IsTrue(Until(() => a.Stage == "play" && b.Stage == "play"), "play");
            Vec2 start = b.Mirror.Players[a.PlayerId].Position;
            int tick = 0;
            Assert.IsTrue(Until(() => snapshots > 30 && said == 1 && !b.Mirror.Players[a.PlayerId].Position.IsEqualApprox(start), () =>
            {
                tick++;
                a.SendInput(PlayerInput.Create(new Vec2(1.0, 0.0), Vec2.Zero, tick % 40 < 3 ? PlayerInput.BtnCard1 : 0, tick), DT);
                b.SendInput(PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, tick), DT);
                if (tick == 60) a.Say(1);
            }), "snapshots, chat and movement from the Godot server");
            TestContext.WriteLine("snapshots " + snapshots + ", A seen by B at " + b.Mirror.Players[a.PlayerId].Position);
            a.Close();
            b.Close();
        }
    }
}
