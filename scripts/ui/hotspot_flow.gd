class_name HotspotFlow
extends RefCounted
## What happens behind a hotspot modal, with no widget in it. Create: is this phone's hotspot on? Join: is this phone on a
## Wi-Fi, and is there a verified room on it? It decides, it never creates or joins anything: `finished` tells the screen
## to go on (with the room's address when joining). Drive it with `tick(delta)` every frame; every state can be cancelled
## and ends by itself, and after `cancel` or `finished` nothing is emitted again.

signal step_changed(step: Step)
## The check passed. `url` is the room to join ("" when creating).
signal finished(url: String)

enum Mode { CREATE, JOIN }
enum Step { CHECKING, HOTSPOT_OFF, HOTSPOT_ON, NO_WIFI, SEARCHING, FOUND, NO_ROOM }

const RULES: NetRules = preload("res://data/rules/net_rules.tres")

var mode: Mode
var step: Step = Step.CHECKING
var finder: HotspotFinder = HotspotFinder.new()
## Why the room was not found (full, started, other version, or none), and this phone's network, for a failure step.
var failure: String = ""
var detail: String = ""
## Create only: no hotspot was found but this phone is on a private network (a Wi-Fi, or a hotspot that an older phone runs on
## its Wi-Fi interface), and the beacon works on any such network, so it may host here.
var can_host_here: bool = false

var _interfaces: Callable
var _plan_for: Callable
var _check_next: bool = false
var _ended: bool = false


## `interfaces` returns the phone's `IP.get_local_interfaces()`; `plan_for(interfaces)` returns the HotspotNet.Plan to
## search with. Both default to the real ones.
func _init(flow_mode: Mode, interfaces: Callable = Callable(), plan_for: Callable = Callable()) -> void:
	mode = flow_mode
	_interfaces = interfaces if interfaces.is_valid() else IP.get_local_interfaces
	_plan_for = plan_for if plan_for.is_valid() else _plan

## Starts (or restarts, for Try again): the verdict comes on the next tick, so "checking" is on screen first.
func begin() -> void:
	if _ended:
		return
	finder.stop()
	_check_next = true
	_enter(Step.CHECKING)


## The app came back from the phone's settings: look again if it was waiting for them.
func resumed() -> void:
	if step == Step.HOTSPOT_OFF or step == Step.NO_WIFI:
		begin()


## Create, from the "off" step: host on this network anyway. Only offered when the phone is on a private network.
func host_here() -> void:
	if _ended or step != Step.HOTSPOT_OFF or not can_host_here:
		return
	_enter(Step.HOTSPOT_ON)
	_end("")


func cancel() -> void:
	_ended = true
	finder.stop()


func tick(delta: float) -> void:
	if _ended:
		return
	if _check_next:
		_check_next = false
		_check()
	elif step == Step.SEARCHING:
		finder.poll(delta)
		if not finder.url.is_empty():
			_enter(Step.FOUND)
			_end(finder.url)
		elif finder.failed:
			failure = finder.failure_message
			detail = _detail_with_search()
			_enter(Step.NO_ROOM)


func _check() -> void:
	var interfaces: Array = _interfaces.call() as Array
	detail = UiText.f("This phone: %s", [HotspotNet.describe(interfaces, RULES)])
	if mode == Mode.CREATE:
		if HotspotNet.hotspot_on(interfaces, RULES):
			_enter(Step.HOTSPOT_ON)
			_end("")
		else:
			can_host_here = HotspotNet.wifi_connected(interfaces, RULES)
			_enter(Step.HOTSPOT_OFF)
	elif not HotspotNet.wifi_connected(interfaces, RULES):
		_enter(Step.NO_WIFI)
	else:
		finder.start(_plan_for.call(interfaces) as HotspotNet.Plan)
		_enter(Step.SEARCHING)


func _end(url: String) -> void:
	_ended = true
	finder.stop()
	finished.emit(url)


func _enter(next: Step) -> void:
	step = next
	step_changed.emit(next)


func _detail_with_search() -> String:
	return detail + "\n" + UiText.f("Asked %d · answered %d", [finder.asked, finder.heard])


func _plan(interfaces: Array) -> HotspotNet.Plan:
	return HotspotNet.search_plan(interfaces, RULES)
