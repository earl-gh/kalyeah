# Kalyeah game server (`unity/Kalyeah.Server`)

The public Online server is a standalone headless **`.NET 8`** executable (`unity/Kalyeah.Server/Kalyeah.Server.csproj`) sharing the exact engine-free `Kalyeah.Sim` and `Kalyeah.Net` assemblies used by the Unity 6 Android client. It listens for WebSocket connections and HTTP health checks (`200 kalyeah`) on `$PORT` (default `8080`), manages 10-slot rooms, and steps every match authoritatively at `30 Hz` (clients send `PlayerInput` frames only). There is no database and no accounts: rooms live in memory and reset when the server restarts.

## Local (Hotspot) rooms need no internet server

A Local room is hosted inside the Android app itself (`HotspotNet` + `GameServer` over managed `TcpListener`, paired with `kalyeah_local.androidlib` for `LocalOnlyHotspot`, Wi-Fi QR, and NFC Tap-to-Join). Friends on the same hotspot or Wi-Fi tap Join and discover the host automatically via UDP `HotspotBeacon` (no room code or IP entry). This guide covers the public **Online** server.

## Run locally

```sh
dotnet run --project unity/Kalyeah.Server               # port 8080, loads unity/Data/*.json
PORT=9000 dotnet run --project unity/Kalyeah.Server     # custom port
```

Or build and run the root multi-stage `.NET 8` Docker image:

```sh
docker build -t kalyeah-server .
docker run --rm -p 8080:8080 kalyeah-server
```

## Deploy for free (Render or Fly.io)

Online play uses TLS (`wss://`), which Render and Fly.io terminate automatically in front of container port `8080`:

### Render
1. Sign in at `render.com` with GitHub → **New → Web Service** → select the repository.
2. Runtime **Docker** (uses the root `Dockerfile`, which publishes `unity/Kalyeah.Server` and copies `unity/Data` to `/app/Data`), instance type **Free**, then **Deploy**.
3. Once live (`https://kalyeah-xxxx.onrender.com`), set `wss://kalyeah-xxxx.onrender.com` as `KALYEAH_SERVER_URL` below.

Free-tier instances sleep after ~15 minutes of inactivity and take ~60 s to cold-start. Online connections use `connect_timeout` (`75 s` in `unity/Data/rules__net_rules.json`) and display *"Waking up the server. This can take a minute…"* after `wake_hint_after` (`4 s`), whereas Local Hotspot connections keep a fast `3 s` timeout.

### Fly.io
```sh
fly launch --no-deploy        # uses root Dockerfile, internal port 8080
fly deploy
```
Address: `wss://<app-name>.fly.dev`.

## Stamp the server URL into the Android APK

GitHub → **Settings → Secrets and variables → Actions → Variables** → `KALYEAH_SERVER_URL` = `wss://kalyeah-xxxx.onrender.com`. `KalyeahBuild.BuildAndroid` writes `Assets/Imported/Resources/Data/build_stamp.txt` during the **Unity 6 CI** Android job, which `AppSession` reads at startup.

## How it works (`unity/Kalyeah.Net` & `unity/Kalyeah.Server`)

- `unity/Kalyeah.Server/Program.cs`: parses `$PORT` and `--data`, loads `SimData` from `/app/Data` (`unity/Data`), serves HTTP health checks, and ticks `GameServer` on a monotonic `30 Hz` loop.
- `unity/Kalyeah.Net/WsServerTransport.cs` & `GameServer.cs`: managed RFC-6455 WebSocket packet pipe and binary `Protocol` router.
- `unity/Kalyeah.Net/RoomService.cs`: 8-character room passcodes, 5v5 team slots, ready checks, start rules, host handover, and the `60 s` reconnect window.
- `unity/Kalyeah.Net/ServerMatch.cs`: loading sync, character/superpower pick, authoritative `30 Hz` `MatchSim` stepping, `20 Hz` quantized snapshots (with *Langit Lupa* brownout position masking), and discrete combat events.
- `unity/Kalyeah.Net/GameClient.cs`: client mirror handling local movement prediction/reconciliation and `100 ms` remote entity interpolation.
