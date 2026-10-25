# Kalyeah game server

The server is the same Godot project run headless. It listens for WebSocket
connections on `$PORT` (default 8080), keeps the rooms, and runs every match
(server-authoritative; clients only send inputs). There's no database and no
accounts. Rooms live in memory and vanish when the server restarts.

## Hotspot rooms need no server

A hotspot room is hosted by the Android app itself (the same server code runs inside the app,
`Net.start_host`). Friends on the same hotspot tap Join and the app finds the host by itself (no code, no address).
This file is about the public **Online** server.

## Run it on a computer

```sh
godot --headless -- --server            # port 8080
PORT=9000 godot --headless -- --server  # another port
```

The game has no server field: it uses the address the APK was built with (below,
the `kalyeah/network/server_url` project setting).

## Deploy for free (from a phone works too)

Online play should use a **wss://** (TLS) server. Both hosts below give you https/wss
automatically. (Hotspot rooms are the exception: the host's phone is the server and
friends reach it with a plain `ws://` address on the local network.)

### Render (easiest, all in the browser)

1. render.com → sign in with GitHub → **New → Web Service** → pick `earl-gh/kalyeah`.
2. Runtime **Docker** (it finds the `Dockerfile`), instance type **Free**, then **Deploy**.
3. When it's live you get an address like `https://kalyeah-xxxx.onrender.com`.
   In the game use `wss://kalyeah-xxxx.onrender.com`.

The free tier sleeps after ~15 minutes without traffic. The first connection
after that takes about a minute while it wakes up.

### Fly.io

```sh
fly launch --no-deploy        # accept the Dockerfile, internal port 8080
fly deploy
```

Address: `wss://<app-name>.fly.dev`.

## Tell the game where the server is

GitHub → Settings → Secrets and variables → Actions → **Variables** → New variable
`KALYEAH_SERVER_URL` = `wss://kalyeah-xxxx.onrender.com`. The next `Android APK` build
stamps it into the app.

## How it works (for developers)

- `scripts/net/game_server.gd`: WebSocketMultiplayerPeer used as a packet pipe,
  with messages from `scripts/net/protocol.gd`.
- `scripts/net/room_service.gd`: passcodes, team slots, ready, start rules, host
  handover, and the 60 s reconnect window.
- `scripts/net/server_match.gd`: loading sync, then the weapon pick, then the
  MatchSim at 30 Hz. Snapshots go out at 20 Hz, and sim signals are relayed as events.
- `scripts/net/game_client.gd`: the client mirror. It predicts and reconciles the
  local player's movement and draws the other players 0.1 s in the past between
  snapshots.
