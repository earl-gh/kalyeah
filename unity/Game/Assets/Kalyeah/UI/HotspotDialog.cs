using System;
using Kalyeah.Net;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.UI
{
    /// <summary>
    /// The two modals in front of a Local room (hotspot_dialogs.gd; the decisions are HotspotFlow's). Create checks that this phone's
    /// hotspot is on or that it is on a Wi-Fi, then goes on by itself; Join checks the Wi-Fi, looks for rooms and lists every verified
    /// one (a button each: "Gabo's room  1/6"), and the player taps the one to join. Each says what it is doing and what to do when it
    /// cannot go on, with Network settings (where Android can open them), Try again and Cancel; nothing runs or calls back once it is
    /// closed, and coming back from the phone's settings checks again. No interface names, addresses or counts are ever shown.
    /// </summary>
    public sealed class HotspotDialog : MonoBehaviour
    {
        HotspotFlow _flow;
        MenuDialog _dialog;
        Text _message;
        RectTransform _rooms;
        Button _settings;
        Button _again;
        Action<string> _done;

        public static void Create(MenuHost host, Action ready) => Open(host, HotspotFlow.FlowMode.Create, _ => ready());

        public static void Join(MenuHost host, Action<string> found) => Open(host, HotspotFlow.FlowMode.Join, found);

        static void Open(MenuHost host, HotspotFlow.FlowMode mode, Action<string> done)
        {
            MenuDialog dialog = MenuDialog.Open(host, "Local", MenuDialog.WidthSmall + 80f);
            var driver = dialog.Root.gameObject.AddComponent<HotspotDialog>();
            driver._dialog = dialog;
            driver._done = done;
            driver._flow = new HotspotFlow(mode, MenuServices.Data.Net);
            driver._message = dialog.AddText("", MenuKit.Body);
            MenuKit.Size(driver._message, -1f, 96f);
            driver._rooms = MenuKit.Rect("Rooms", dialog.Body);
            MenuKit.Column(driver._rooms, 10f);
            driver._settings = dialog.AddButton("Network settings", "blue", () => MenuServices.OpenNetworkSettings(), MenuKit.Detail + 2);
            driver._again = dialog.AddButton("Try again", "blue", () => driver._flow.Begin(), MenuKit.Detail + 2);
            dialog.AddButton("Cancel", "grey", dialog.Close, MenuKit.Detail + 2);
            driver._flow.StepChanged += driver.OnStep;
            driver._flow.RoomsChanged += _ => driver.FillRooms();
            driver._flow.Finished += driver.OnFinished;
            dialog.Closed += driver._flow.Cancel;
            driver._flow.Begin();
        }

        void OnStep(HotspotFlow.Step step)
        {
            _message.text = _flow.Message();
            _again.gameObject.SetActive(_flow.ShowTryAgain);
            _settings.gameObject.SetActive(_flow.ShowNetworkSettings && MenuServices.CanOpenNetworkSettings);
            if (step == HotspotFlow.Step.Checking) FillRooms();
        }

        void FillRooms()
        {
            _message.text = _flow.Message();
            MenuKit.Clear(_rooms);
            if (_flow.Current == HotspotFlow.Step.Checking) return;
            foreach (HotspotFinder.FoundRoom room in _flow.Finder.Rooms)
            {
                string url = room.Url;
                Button button = MenuKit.Button("Room", _rooms, MenuCatalog.LocalRoomText(room), "green", () => _flow.Choose(url), MenuKit.Body);
                MenuKit.Size(button, -1f, 88f);
            }
        }

        void OnFinished(string url)
        {
            if (!_dialog.IsOpen) return;
            _dialog.Close();
            _done?.Invoke(url);
        }

        void Update() => _flow?.Tick(Time.unscaledDeltaTime);

        void OnApplicationFocus(bool focused)
        {
            if (focused) _flow?.Resumed();
        }

        void OnDestroy() => _flow?.Cancel();
    }
}
