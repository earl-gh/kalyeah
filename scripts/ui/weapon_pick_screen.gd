class_name WeaponPickScreen
extends Control
## Character/loadout interaction. Practice waits for confirmation; multiplayer keeps its timer.

signal confirmed(picks: Array[StringName])
## A character card was tapped.
signal character_changed(id: StringName)
## Swap mode: a new pair of weapons was picked (applied at once).
signal swapped(picks: Array[StringName])
signal closed

## The character cards are exactly 3:4 (width:height).
const PROFILE_SIZE: Vector2 = Vector2(126.0, 168.0)
const TAB_FONT: int = 24
const NAME_FONT: int = 28
const DESCRIPTION_FONT: int = 26
const STAT_FONT: int = 24
const PICKED_COLOR: Color = Color(1.0, 0.82, 0.25)
const CARD_COLOR: Color = Color(0.13, 0.16, 0.32, 0.95)
const SURFACE_SELECTED: Color = Color(0.2, 0.27, 0.4)
const INK: Color = Color(0.1, 0.06, 0.06)
const KIND_COLORS: Dictionary[WeaponDef.Kind, Color] = {
	WeaponDef.Kind.ATTACK: Color(1.0, 0.42, 0.34),
	WeaponDef.Kind.CROWD_CONTROL: Color(0.74, 0.52, 1.0),
	WeaponDef.Kind.BLOCK: Color(0.42, 0.72, 1.0),
	WeaponDef.Kind.HEAL: Color(0.45, 0.85, 0.4),
}

var pick: WeaponPick
## The character picked on this screen (the kid you play as).
var character_id: StringName = &""
## Respawn swap mode: no pick timer, every full pair is applied, Done closes.
var swap_mode: bool = false
var untimed: bool = false
var replacement_slot: int = 0
var _hint: Label
var _mode_label: Label
var _kid_label: Label
var _safe_margin: MarginContainer

var _characters: Array[CharacterDef] = []
var _profiles: Dictionary[StringName, Button] = {}
var _taken_labels: Dictionary[StringName, Label] = {}
var _taken: Array[StringName] = []
var _character_row: Control
var _tabs: Dictionary[String, Button] = {}
var _pages: Dictionary[String, Control] = {}
var _buttons: Dictionary[StringName, Button] = {}
var _badges: Dictionary[StringName, Label] = {}
var _defs: Dictionary[StringName, WeaponDef] = {}
var _title: Label
var _timer: Label
var _ring: CountdownRing
var _sockets: Array[LoadoutSocket] = []
var _ready_button: Button
var _rng: RandomNumberGenerator
var _swap_total: float = 0.0


static func kind_color(kind: WeaponDef.Kind) -> Color:
	return KIND_COLORS[kind]


func _ready() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	visible = false


## Shows the screen for `p_pick`. `rng` fills empty slots on timeout. With `characters` the
## kid is picked too (`current` is the one shown selected first).
func open(p_pick: WeaponPick, defs: Array[WeaponDef], heading: String, rng: RandomNumberGenerator,
		characters: Array[CharacterDef] = [], current: StringName = &"", no_timer: bool = false) -> void:
	pick = p_pick
	_rng = rng
	swap_mode = false
	untimed = no_timer
	replacement_slot = mini(pick.picks.size(), 1)
	_characters = characters
	character_id = current if not characters.is_empty() else &""
	if not characters.is_empty() and not characters.any(func(c: CharacterDef) -> bool: return c.id == character_id):
		character_id = characters[0].id
	_show(defs, heading)


## Opens the respawn swap: change weapons as often as you like until `close()`.
func open_swap(p_pick: WeaponPick, defs: Array[WeaponDef], heading: String) -> void:
	pick = p_pick
	swap_mode = true
	replacement_slot = 0
	untimed = true
	_swap_total = 0.0
	_show(defs, heading)


func close() -> void:
	if not visible:
		return
	visible = false
	closed.emit()


## Swap mode shows the respawn countdown instead of a pick timer.
func show_time(seconds: float) -> void:
	if visible:
		_swap_total = maxf(_swap_total, seconds)
		_ring.visible = true
		_timer.text = "Back in %d" % ceili(seconds)
		_ring.show_time(seconds, _swap_total)


func _show(defs: Array[WeaponDef], _heading: String) -> void:
	if _buttons.is_empty():
		_build(defs)
	_title.text = "CHANGE WEAPONS" if swap_mode else "CHOOSE 2 WEAPONS"
	_mode_label.text = "Changes apply immediately" if swap_mode else ("Practice · Take your time" if untimed else "Confirm before the timer ends")
	_ring.visible = not untimed
	LobbyWidgets.apply_safe_area(_safe_margin, 24.0, 24.0)
	_character_row.visible = not swap_mode and not _characters.is_empty()
	(find_child("Characters", true, false) as Control).visible = _character_row.visible
	_open_tab(_first_tab())
	visible = true
	_refresh()


## The tab shown first: the street game of your first weapon, else the first one.
func _first_tab() -> String:
	if pick != null and not pick.picks.is_empty() and _defs.has(pick.picks[0]):
		return _defs[pick.picks[0]].street_game
	return _tabs.keys()[0] as String


## Shows one street game's weapon cards.
func open_tab(group: String) -> void:
	_open_tab(group)


func _open_tab(group: String) -> void:
	for name: String in _pages:
		_pages[name].visible = name == group
		_tabs[name].set_pressed_no_signal(name == group)


func current_tab() -> String:
	for name: String in _pages:
		if _pages[name].visible:
			return name
	return ""


## The kids a teammate already locked in: they are greyed out and cannot be picked (the other
## team may play the same kid). If yours was taken, the first free one is selected instead.
func set_taken(ids: Array[StringName]) -> void:
	_taken = ids.duplicate()
	if _profiles.is_empty():
		return
	if _taken.has(character_id):
		for character: CharacterDef in _characters:
			if not _taken.has(character.id):
				character_id = character.id
				break
	_refresh()


func is_taken(id: StringName) -> bool:
	return _taken.has(id)


## Picks the kid you play as.
func pick_character(id: StringName) -> void:
	if not is_open() or swap_mode or not _profiles.has(id) or _taken.has(id):
		return
	character_id = id
	_refresh()
	character_changed.emit(id)


func is_open() -> bool:
	return visible and pick != null and not pick.done


func tap(id: StringName) -> void:
	if not is_open():
		return
	if pick.picks.size() == WeaponPick.SLOTS and not pick.picks.has(id):
		pick.replace_slot(replacement_slot, id)
		replacement_slot = 1 - replacement_slot
	else:
		pick.toggle(id)
		replacement_slot = mini(pick.picks.size(), 1)
	_refresh()
	if swap_mode and pick.picks.size() == WeaponPick.SLOTS:
		swapped.emit(pick.picks.duplicate())


func press_ready() -> void:
	if swap_mode:
		close()
	elif is_open() and pick.confirm():
		_finish()


## Counts the pick timer down (call every frame while open).
func tick(delta: float) -> void:
	if not is_open() or swap_mode or untimed:
		return
	if pick.step(delta, _rng):
		_finish()
	else:
		_refresh()


func _finish() -> void:
	visible = false
	confirmed.emit(pick.picks.duplicate())


func _refresh() -> void:
	if not swap_mode:
		_timer.text = "%d" % ceili(pick.time_left)
		_ring.show_time(pick.time_left, pick.time_total)
	for id: StringName in _profiles:
		_profiles[id].set_pressed_no_signal(id == character_id)
		_profiles[id].disabled = _taken.has(id)
		_profiles[id].modulate = Color(0.6, 0.6, 0.66) if _taken.has(id) else Color.WHITE
		_taken_labels[id].visible = _taken.has(id)
	for id: StringName in _buttons:
		var slot: int = pick.picks.find(id)
		_buttons[id].set_pressed_no_signal(slot >= 0)
		_badges[id].visible = slot >= 0
		_badges[id].text = "%d" % (slot + 1)
	for slot: int in _sockets.size():
		var id: StringName = pick.picks[slot] if slot < pick.picks.size() else &""
		_sockets[slot].active = slot == replacement_slot
		_sockets[slot].show_weapon(_defs.get(id) as WeaponDef)
	_hint.text = selection_hint()
	_kid_label.text = "Kid: %s · swipe to change" % String(character_id).capitalize()
	var can_go: bool = swap_mode or pick.can_confirm()
	_ready_button.disabled = not can_go
	_style_ready(can_go)
	if swap_mode:
		_ready_button.text = "DONE"
	else:
		_ready_button.text = "READY" if pick.can_confirm() else "CHOOSE 2 WEAPONS"


func _build(defs: Array[WeaponDef]) -> void:
	LoadoutLayout.build(self, defs)


static func _stat_line(def: WeaponDef) -> String:
	return LoadoutCards.stat_line(def)


func select_slot(slot: int) -> void:
	replacement_slot = clampi(slot, 0, WeaponPick.SLOTS - 1)
	_refresh()


func selection_hint() -> String:
	if swap_mode:
		return "Changes apply immediately · tap a slot to replace it"
	if pick.picks.size() < WeaponPick.SLOTS:
		return "Choose %d more weapon%s" % [WeaponPick.SLOTS - pick.picks.size(), "" if pick.picks.size() == 1 else "s"]
	return "Slot %d selected · tap a weapon to replace it" % (replacement_slot + 1)


func _style_ready(enabled: bool) -> void:
	KalyeahTheme.style_button(_ready_button, "orange" if enabled else "grey")


func force_finish() -> void:
	if swap_mode:
		close()
		return
	if not is_open():
		return
	pick.fill(_rng)
	pick.done = true
	_finish()


func dismiss() -> void:
	if pick != null:
		pick.done = true
	visible = false
