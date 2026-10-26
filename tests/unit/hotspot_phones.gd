extends RefCounted
## Interface lists in the shape of `IP.get_local_interfaces()`, for the hotspot tests.
##
## Only REAL_CONTAINER is a capture (this build container, 2026-10-26). Every phone is SYNTHETIC: built from the interface
## names and the tether addresses recalled for that kind of phone and from the AOSP tethering sources (Android 9
## `TetherInterfaceStateMachine`: Wi-Fi 192.168.43.<random>, USB 192.168.42.129; Android 11 `PrivateAddressCoordinator`:
## 192.168.<random>.<random>), not read from a device. They pin the rule; the device test script checks the rule on phones.


static func iface(interface_name: String, addresses: Array) -> Dictionary:
	return {"name": interface_name, "friendly": interface_name, "index": "2", "addresses": addresses}


const REAL_CONTAINER: Array = [
	{"name": "eth0", "friendly": "eth0", "index": "4", "addresses": ["192.0.2.2"]},
	{"name": "lo", "friendly": "lo", "index": "1", "addresses": ["127.0.0.1"]},
]


## A phone on a home router with mobile data on (MediaTek names the mobile interface ccmni, Qualcomm rmnet).
static func on_home_wifi() -> Array:
	return [iface("lo", ["127.0.0.1"]), iface("wlan0", ["192.168.1.37", "fe80::1c2a:3bff:fe4d:5e6f"]), iface("rmnet_data0", ["10.58.2.14"])]


## Mobile data only, with the private address carriers give (CGNAT-style 10.x): not a Wi-Fi, not a hotspot.
static func on_mobile_data_only() -> Array:
	return [iface("lo", ["127.0.0.1"]), iface("ccmni1", ["10.58.2.14"])]


## Stock Android hotspot: the access point is its own interface and its address has a random last number (never .1).
static func stock_hotspot() -> Array:
	return [iface("lo", ["127.0.0.1"]), iface("wlan1", ["192.168.31.167"]), iface("rmnet_data1", ["10.1.2.3"])]


static func samsung_hotspot() -> Array:
	return [iface("lo", ["127.0.0.1"]), iface("swlan0", ["192.168.43.1"]), iface("rmnet_data0", ["10.20.30.40"])]


static func mediatek_hotspot() -> Array:
	return [iface("lo", ["127.0.0.1"]), iface("ap0", ["192.168.43.1"]), iface("ccmni1", ["10.58.2.14"])]


static func qualcomm_softap_hotspot() -> Array:
	return [iface("lo", ["127.0.0.1"]), iface("softap0", ["192.168.43.77"]), iface("rmnet_data0", ["10.9.8.7"])]


## A hotspot on a 10.x network (some skins and newer Android pick another range).
static func ten_net_hotspot() -> Array:
	return [iface("lo", ["127.0.0.1"]), iface("ap0", ["10.45.12.1"])]


## A name this list has never seen: still a tether, because it is not a Wi-Fi client, mobile data, a VPN or Wi-Fi Direct.
static func unknown_vendor_hotspot() -> Array:
	return [iface("lo", ["127.0.0.1"]), iface("vendor_ap1", ["192.168.7.1"]), iface("rmnet_data0", ["10.1.1.1"])]


## Wi-Fi sharing while connected to a Wi-Fi: both a client and a tether.
static func hotspot_and_wifi() -> Array:
	return [iface("lo", ["127.0.0.1"]), iface("wlan0", ["192.168.1.5"]), iface("ap0", ["192.168.43.1"])]


static func usb_tether() -> Array:
	return [iface("lo", ["127.0.0.1"]), iface("rndis0", ["192.168.42.129"])]


static func bluetooth_tether() -> Array:
	return [iface("lo", ["127.0.0.1"]), iface("bt-pan", ["192.168.44.1"])]


## A guest on somebody else's phone hotspot: a Wi-Fi client with a 192.168.43.x address, which is not a hotspot host.
static func guest_on_a_hotspot() -> Array:
	return [iface("lo", ["127.0.0.1"]), iface("wlan0", ["192.168.43.57"]), iface("rmnet_data0", ["10.1.2.3"])]


static func vpn_over_wifi() -> Array:
	return [iface("lo", ["127.0.0.1"]), iface("wlan0", ["192.168.1.5"]), iface("tun0", ["10.8.0.2"])]


## Wi-Fi Direct group only (192.168.49.1): neither.
static func wifi_direct_only() -> Array:
	return [iface("lo", ["127.0.0.1"]), iface("p2p-wlan0-0", ["192.168.49.1"])]


## Wi-Fi just connected, no address from the router yet (link-local only).
static func wifi_without_address() -> Array:
	return [iface("lo", ["127.0.0.1"]), iface("wlan0", ["169.254.12.9", "fe80::1"])]


static func offline() -> Array:
	return [iface("lo", ["127.0.0.1"])]
