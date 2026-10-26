class_name NetRules
extends Resource
## Networking and lobby numbers (docs/GDD.md "Lobby rules", HANDOFF M4). Values
## live in data/rules/net_rules.tres.

## Passcode: `passcode_length` characters from `passcode_alphabet` (no I, L, O, 0, 1).
@export var passcode_length: int = 0
@export var passcode_alphabet: String = ""
## Seconds a player who dropped out of a running match may reconnect into their slot.
@export var reconnect_window: float = 0.0
## Server snapshots per second (inputs go up every sim tick, 30 Hz).
@export var snapshot_rate: int = 0
## Clients draw other players this far in the past, between two snapshots.
@export var interpolation_delay: float = 0.0
## Seconds the server waits for every client to finish loading before starting anyway.
@export var load_timeout: float = 0.0
## Extra seconds the server waits after the 10 s weapon pick for late picks.
@export var pick_grace: float = 0.0
## Inputs a server queues per player before dropping the oldest (keeps lag bounded).
@export var max_queued_inputs: int = 0
@export var max_name_length: int = 0
@export var default_port: int = 0

## Hotspot discovery (docs/GDD.md "Hotspot rooms"). The guest sends a UDP probe to `discovery_port` for `discovery_seconds`,
## again every `discovery_interval`; the host answers it.
@export var discovery_port: int = 0
@export var discovery_seconds: float = 0.0
@export var discovery_interval: float = 0.0
## Probes the host answers in one frame (a flood from the network cannot stall the game).
@export var discovery_burst: int = 0
## Interface names (prefixes) that never carry a hotspot or a Wi-Fi: loopback, mobile data, VPN, Wi-Fi Direct.
## An interface in neither this list nor `client_interface_names` is a tether the phone hosts (`ap0`, `swlan0`, `wlan1`...).
@export var ignored_interface_prefixes: PackedStringArray = PackedStringArray()
## The interfaces a phone uses as a client of somebody else's network.
@export var client_interface_names: PackedStringArray = PackedStringArray()
## Where a room server may be on a hotspot when nothing answers the probe: Android's classic tether address and iPhone's.
@export var fallback_hosts: PackedStringArray = PackedStringArray()
