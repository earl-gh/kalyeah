class_name HotspotNet
extends RefCounted
## What the phone's network interfaces say about a hotspot, without anyone typing an address or a code.
##
## An interface is a CLIENT (Wi-Fi or Ethernet of somebody else's network), IGNORED (loopback, mobile data, VPN, Wi-Fi
## Direct: never a hotspot) or a TETHER (everything else: the access point `ap0`, `swlan0`, `wlan1`..., USB, Bluetooth).
## The names are data (`NetRules`). A phone hosts a hotspot when a TETHER has a private IPv4 address; it is on a Wi-Fi when a
## CLIENT has one. A phone on an ordinary Wi-Fi has only `wlan0`, so it is never taken for a hotspot host.
##
## The host's address is not guessed: Android gives the access point `192.168.<random>.<random>` (AOSP
## `PrivateAddressCoordinator`, `TetherInterfaceStateMachine`), so the guest asks the network with a broadcast and the host
## answers (HotspotBeacon, HotspotFinder). The ".1" of each network and the classic tether addresses stay as a fallback.

enum Role { IGNORED, CLIENT, TETHER }

const LIMITED_BROADCAST: String = "255.255.255.255"
## The last number of a /24 that is the broadcast address, and the one a router (or a stock-Android-9-era hotspot) usually holds.
const BROADCAST_HOST: int = 255
const GATEWAY_HOST: int = 1


## Everything a guest needs to look for a host, built from the phone's interfaces.
class Plan extends RefCounted:
	## `ws://` addresses to try without being asked (the fallback).
	var candidates: PackedStringArray = PackedStringArray()
	## Where to send the probe: {"from": this phone's address, "to": a broadcast address}.
	var routes: Array[Dictionary] = []
	var beacon_port: int = 0
	var ws_port: int = 0
	var seconds: float = 0.0
	var interval: float = 0.0


static func role_of(interface_name: String, rules: NetRules) -> Role:
	if rules.client_interface_names.has(interface_name):
		return Role.CLIENT
	for prefix: String in rules.ignored_interface_prefixes:
		if interface_name.begins_with(prefix):
			return Role.IGNORED
	return Role.TETHER


## The private IPv4 addresses of the interfaces that have `role`. `interfaces` is `IP.get_local_interfaces()`.
static func addresses_of(interfaces: Array, role: Role, rules: NetRules) -> PackedStringArray:
	var found: PackedStringArray = PackedStringArray()
	for interface: Dictionary in interfaces:
		if role_of(String(interface.get("name", "")), rules) != role:
			continue
		for address: String in interface.get("addresses", []) as Array:
			if is_private(address) and not found.has(address):
				found.append(address)
	return found


## True when this phone is handing out a network: a hotspot (or a USB or Bluetooth tether) is on.
static func hotspot_on(interfaces: Array, rules: NetRules) -> bool:
	return not addresses_of(interfaces, Role.TETHER, rules).is_empty()


## True when this phone is a client of a Wi-Fi network (not its own hotspot).
static func wifi_connected(interfaces: Array, rules: NetRules) -> bool:
	return not addresses_of(interfaces, Role.CLIENT, rules).is_empty()


## How to look for a host from the Wi-Fi this phone is on. The probe leaves by each client address twice: to the limited
## broadcast (it goes out of the interface whose address the socket is bound to, so any netmask is covered) and to the
## ".255" of the /24 (what Android gives a hotspot; some routers filter the limited broadcast).
static func search_plan(interfaces: Array, rules: NetRules) -> Plan:
	var plan: Plan = Plan.new()
	plan.beacon_port = rules.discovery_port
	plan.ws_port = rules.default_port
	plan.seconds = rules.discovery_seconds
	plan.interval = rules.discovery_interval
	var hosts: Array[String] = []
	for address: String in addresses_of(interfaces, Role.CLIENT, rules):
		plan.routes.append({"from": address, "to": LIMITED_BROADCAST})
		plan.routes.append({"from": address, "to": _in_network(address, BROADCAST_HOST)})
		hosts.append(_in_network(address, GATEWAY_HOST))
	hosts.append_array(rules.fallback_hosts)
	for host: String in hosts:
		var url: String = "ws://%s:%d" % [host, rules.default_port]
		if not plan.candidates.has(url):
			plan.candidates.append(url)
	return plan
## True for 10.x.x.x, 172.16-31.x.x and 192.168.x.x.
static func is_private(ip: String) -> bool:
	var parts: PackedStringArray = ip.split(".")
	if parts.size() != 4:
		return false
	for part: String in parts:
		if not part.is_valid_int() or part.to_int() < 0 or part.to_int() > 255:
			return false
	var a: int = parts[0].to_int()
	var b: int = parts[1].to_int()
	return a == 10 or (a == 172 and b >= 16 and b <= 31) or (a == 192 and b == 168)


## `address` with its last number replaced: the ".1" or ".255" of its /24.
static func _in_network(address: String, last: int) -> String:
	var parts: PackedStringArray = address.split(".")
	return "%s.%s.%s.%d" % [parts[0], parts[1], parts[2], last]
