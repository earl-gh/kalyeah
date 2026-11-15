using System;
using System.Collections.Generic;
using Kalyeah.Net;

namespace Kalyeah.UI
{
    /// <summary>
    /// What happens behind a Local (hotspot) modal, with no widget in it (port of hotspot_flow.gd). Create: is this phone's hotspot
    /// on, or is it on a Wi-Fi? Join: is this phone on a Wi-Fi, and is there a verified room on it? It never creates or joins a room by
    /// itself: Join lists every verified room (`RoomsChanged`) and the player taps one (`Choose`), since several friends may host at
    /// once; `Finished` tells the screen to go on (with the room's address when joining). Drive it with `Tick(delta)` every frame;
    /// every state can be cancelled and ends by itself, and after `Cancel` or `Finished` nothing is raised again.
    /// Not ported: the Android plugin side (`LocalLink`: starting a game hotspot, joining a friend's from a QR or NFC); without it the
    /// flow behaves as the Godot game on a phone without the plugin.
    /// </summary>
    public sealed class HotspotFlow
    {
        public enum FlowMode { Create, Join }

        /// <summary>Searching may already list rooms; Found = the search is over and rooms are listed (pick one, or Try again).</summary>
        public enum Step { Checking, NoNetwork, NetworkOk, NoWifi, Searching, Found, NoRoom }

        public event Action<Step> StepChanged;
        /// <summary>The check passed: the room to join ("" when creating).</summary>
        public event Action<string> Finished;
        /// <summary>Join: the list of verified rooms grew.</summary>
        public event Action<List<HotspotFinder.FoundRoom>> RoomsChanged;

        public readonly FlowMode Mode;
        public Step Current { get; private set; } = Step.Checking;
        public readonly HotspotFinder Finder;
        /// <summary>Why no room was found (full, started, other version, none), for NoRoom.</summary>
        public string Failure = "";

        readonly NetRules _rules;
        readonly Func<List<NetInterface>> _interfaces;
        readonly Func<List<NetInterface>, HotspotNet.Plan> _planFor;
        bool _checkNext;
        int _listed;
        bool _ended;

        /// <param name="interfaces">The phone's interfaces (NetInterface.FromSystem by default).</param>
        /// <param name="planFor">How to search from those interfaces (HotspotNet.SearchPlan by default).</param>
        public HotspotFlow(FlowMode mode, NetRules rules, Func<List<NetInterface>> interfaces = null,
            Func<List<NetInterface>, HotspotNet.Plan> planFor = null, HotspotFinder finder = null)
        {
            Mode = mode;
            _rules = rules;
            _interfaces = interfaces ?? NetInterface.FromSystem;
            _planFor = planFor ?? (list => HotspotNet.SearchPlan(list, _rules));
            Finder = finder ?? new HotspotFinder();
        }

        public bool Ended => _ended;

        /// <summary>Starts (or restarts, for Try again): the verdict comes on the next tick, so "Checking…" is on screen first.</summary>
        public void Begin()
        {
            if (_ended) return;
            Finder.Stop();
            _listed = 0;
            _checkNext = true;
            Enter(Step.Checking);
        }

        /// <summary>The app came back from the phone's settings: look again if it was waiting for them.</summary>
        public void Resumed()
        {
            if (Current == Step.NoNetwork || Current == Step.NoWifi) Begin();
        }

        /// <summary>Join: the player tapped the room at `url` in the list.</summary>
        public void Choose(string url)
        {
            if (_ended || Mode != FlowMode.Join) return;
            foreach (HotspotFinder.FoundRoom room in Finder.Rooms)
                if (room.Url == url)
                {
                    End(url);
                    return;
                }
        }

        public void Cancel()
        {
            _ended = true;
            Finder.Stop();
        }

        public void Tick(double delta)
        {
            if (_ended) return;
            if (_checkNext)
            {
                _checkNext = false;
                Check();
                return;
            }
            if (Current != Step.Searching) return;
            Finder.Poll(delta);
            if (Finder.Rooms.Count != _listed)
            {
                _listed = Finder.Rooms.Count;
                RoomsChanged?.Invoke(new List<HotspotFinder.FoundRoom>(Finder.Rooms));
            }
            if (Finder.IsSearching) return;
            if (Finder.Rooms.Count > 0) Enter(Step.Found);
            else if (Finder.Failed)
            {
                Failure = Finder.FailureMessage;
                Enter(Step.NoRoom);
            }
        }

        /// <summary>What the modal says now (no hardware or network details: only what to do).</summary>
        public string Message()
        {
            switch (Current)
            {
                case Step.Checking: return Mode == FlowMode.Create ? "Checking your network…" : "Checking your Wi-Fi…";
                case Step.NoNetwork: return "Turn on your hotspot or Wi-Fi. Then tap Try again.";
                case Step.NetworkOk: return "Starting the room…";
                case Step.NoWifi: return "Join your friend's Wi-Fi or hotspot. Then tap Try again.";
                case Step.Searching: return Finder.Rooms.Count == 0 ? "Looking for rooms…" : "Tap a room to join. Still looking…";
                case Step.Found: return "Tap a room to join.";
            }
            return Failure.Length > 0 ? Failure : HotspotFinder.NoRoom;
        }

        /// <summary>Try again shows on a failure and once the search is over.</summary>
        public bool ShowTryAgain => Current == Step.NoNetwork || Current == Step.NoWifi || Current == Step.NoRoom || Current == Step.Found;

        /// <summary>The button to the phone's network settings shows where the network is what is missing.</summary>
        public bool ShowNetworkSettings => Current == Step.NoNetwork || Current == Step.NoWifi;

        void Check()
        {
            List<NetInterface> interfaces;
            try
            {
                interfaces = _interfaces() ?? new List<NetInterface>();
            }
            catch (Exception e) when (e is System.Net.NetworkInformation.NetworkInformationException || e is NotSupportedException
                || e is PlatformNotSupportedException || e is InvalidOperationException)
            {
                interfaces = new List<NetInterface>();
            }
            if (Mode == FlowMode.Create)
            {
                // the host has two ways to make a room: its own hotspot, or the Wi-Fi it is connected to
                if (HotspotNet.HotspotOn(interfaces, _rules) || HotspotNet.WifiConnected(interfaces, _rules))
                {
                    Enter(Step.NetworkOk);
                    End("");
                }
                else Enter(Step.NoNetwork);
            }
            else if (!HotspotNet.WifiConnected(interfaces, _rules)) Enter(Step.NoWifi);
            else
            {
                Finder.Start(_planFor(interfaces));
                Enter(Step.Searching);
            }
        }

        void End(string url)
        {
            _ended = true;
            Finder.Stop();
            Finished?.Invoke(url);
        }

        void Enter(Step next)
        {
            Current = next;
            StepChanged?.Invoke(next);
        }
    }
}
