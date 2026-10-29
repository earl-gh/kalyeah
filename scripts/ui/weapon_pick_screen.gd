class_name WeaponPickScreen
extends Control
## Character/loadout interaction. Practice waits for confirmation; multiplayer keeps its timer.

signal confirmed(picks: Array[StringName])
## The kid you play as changed (a swipe or a tap on the carousel, or `pick_character`).
signal character_changed(id: StringName)
## Swap mode: a new pair of weapons was picked (applied at once).
signal swapped(picks: Array[StringName])
signal closed
signal exit_requested

## Weapons mode: the kid tiles (3:4), six across the 656 px column, wide enough for the longest name at the caption floor.
## Cards mode has the `KidCarousel` instead.
const PROFILE_SIZE: Vector2 = Vector2(102.0, 136.0)
const PROFILE_GAP: int = 8
## The weapon grid: tiles across, and how tall each tile is.
const GRID_COLUMNS: int = 3
const TILE_HEIGHT: float = 200.0
const NAME_FONT: int = UiMetrics.BODY
const DESCRIPTION_FONT: int = UiMetrics.BODY
const STAT_FONT: int = UiMetrics.DETAIL
const PICKED_COLOR: Color = Color(1.0, 0.82, 0.25)
const CARD_COLOR: Color = KalyeahTheme.SURFACE
const SURFACE_SELECTED: Color = KalyeahTheme.SURFACE_RAISED
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
var back_button: Button
var _exit_dialog: MenuDialog
var hosting: bool = false
var _hint: Label
## Cards mode (GameRules.card_cycle): there is nothing to pick but the kid. The weapon parts of the screen are hidden, Ready says Play.
var cards_mode: bool = false
var _weapon_parts: Array[Control] = []
var _filler: Control
## The kid pick of cards mode: the card pictures as an endless carousel; the chosen kid's name, large, under it.
var _carousel: KidCarousel
var _hero_name: Label
var _hero_about: Label
var _hero_info: VBoxContainer
## Stat bars on every weapon card are scaled by the biggest number of any weapon.
var _maxima: Dictionary = {}
## The pair offered to a player who has not picked yet (see `WeaponPick.suggestion`), and the button that takes it.
var suggestion: Array[StringName] = []
var _suggest_button: Button
var _mode_label: Label
var _safe_margin: MarginContainer

var _characters: Array[CharacterDef] = []
var _profiles: Dictionary[StringName, Button] = {}
var _taken_labels: Dictionary[StringName, Label] = {}
var _taken: Array[StringName] = []
var _character_row: Control
## The weapon shown in the info panel: the one you touched last (the first of your pair, else the suggested one).
var focused: StringName = &""
var _info_name: Label
var _info_kind: Label
var _info_text: Label
var _info_bars: VBoxContainer
var _info_stats: Label
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
	_title.text = "Change weapons" if swap_mode else ("Choose your kid" if cards_mode else "Loadout")
	_mode_label.text = "Changes apply immediately" if swap_mode else ("Your weapons are cards!" if cards_mode else "Auto-pick at timeout")
	_mode_label.visible = swap_mode or (not untimed and not cards_mode)
	_ring.visible = not untimed and not cards_mode
	for part: Control in _weapon_parts:
		part.visible = not cards_mode
	_filler.visible = cards_mode
	LobbyWidgets.apply_safe_area(_safe_margin, UiMetrics.PAGE_MARGIN, UiMetrics.PAGE_MARGIN)
	_character_row.visible = not swap_mode and not cards_mode and not _characters.is_empty()
	(find_child("Characters", true, false) as Control).visible = _character_row.visible
	_carousel.visible = cards_mode and not _characters.is_empty()
	if _carousel.visible:
		_carousel.setup_kids(_characters, character_id)
		_carousel.set_taken(_taken)
		_fit_kids()
	_focus_first()
	visible = true
	_refresh()


## The kids a teammate already locked in: they cannot be picked (the other team may play the same kid). The carousel keeps them in the
## ring, dark and tagged "Taken", and a swipe steps over them. If yours was taken, the next free kid after it is selected instead.
func set_taken(ids: Array[StringName]) -> void:
	_taken = ids.duplicate()
	if _profiles.is_empty():
		return
	if _taken.has(character_id):
		var at: int = _character_index(character_id)
		for step: int in range(1, _characters.size()):
			var next: CharacterDef = _characters[posmod(at + step, _characters.size())]
			if not _taken.has(next.id):
				character_id = next.id
				break
	_carousel.set_taken(_taken)
	_refresh()


func is_taken(id: StringName) -> bool:
	return _taken.has(id)


func _character_index(id: StringName) -> int:
	for i: int in _characters.size():
		if _characters[i].id == id:
			return i
	return -1


## The carousel settled on a kid (a swipe or a tap): that kid is the pick.
func _on_kid_swiped(at: int) -> void:
	if at < _characters.size() and _characters[at].id != character_id:
		pick_character(_characters[at].id)


## The carousel takes the room the kid pick has: the whole space between the heading and Play, less the kid's name.
func _fit_kids() -> void:
	if _carousel == null or not _carousel.visible or _filler.size.y <= 0.0:
		return
	var room: Vector2 = Vector2(_filler.size.x, _filler.size.y - _hero_info.get_combined_minimum_size().y - UiMetrics.GAP)
	_carousel.custom_minimum_size.y = _carousel.fit(room)


## Picks the kid you play as.
func pick_character(id: StringName) -> void:
	if not is_open() or swap_mode or not _profiles.has(id) or _taken.has(id):
		return
	character_id = id
	_refresh()
	character_changed.emit(id)


func is_open() -> bool:
	return visible and pick != null and not pick.done


## Shows `id` in the info panel (without picking it).
func show_info(id: StringName) -> void:
	if not _defs.has(id):
		return
	focused = id
	var def: WeaponDef = _defs[id]
	_info_name.text = def.display_name
	_info_kind.text = String(WeaponsPage.KIND_NAMES.get(def.kind, ""))
	_info_kind.add_theme_color_override("font_color", kind_color(def.kind))
	_info_text.text = def.description
	for child: Node in _info_bars.get_children():
		_info_bars.remove_child(child)
		child.queue_free()
	var bars: Control = LoadoutCards.bar_strip(def, _maxima)
	_info_bars.add_child(bars)
	_info_stats.text = _stat_line(def)


func _focus_first() -> void:
	var id: StringName = pick.picks[0] if pick != null and not pick.picks.is_empty() else (suggestion[0] if not suggestion.is_empty() else (_defs.keys()[0] as StringName))
	show_info(id)


func tap(id: StringName) -> void:
	if not is_open():
		return
	show_info(id)
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
	elif is_open() and cards_mode:
		force_finish()
	elif is_open() and pick.confirm():
		_finish()


## Counts the pick timer down (call every frame while open).
func tick(delta: float) -> void:
	if not is_open() or swap_mode or untimed or cards_mode:
		return
	if pick.step(delta, _rng):
		_finish()
	else:
		_refresh()


func _finish() -> void:
	_close_exit_dialog()
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
	if cards_mode:
		_hero_name.text = hero_name()
		_hero_about.text = hero_about()
		if _carousel.current_id() != character_id:
			_carousel.show_kid(character_id)
	_hint.text = selection_hint()
	_suggest_button.visible = not swap_mode and not suggestion.is_empty() and pick.picks.size() < WeaponPick.SLOTS
	var can_go: bool = swap_mode or cards_mode or pick.can_confirm()
	_ready_button.disabled = not can_go
	_style_ready(can_go)
	if swap_mode:
		_ready_button.text = "Done"
	else:
		_ready_button.text = "Play" if cards_mode else "Ready"


## The display name of the kid you picked ("" when none is offered).
func hero_name() -> String:
	for character: CharacterDef in _characters:
		if character.id == character_id:
			return character.display_name
	return ""


## The short description of the kid you picked ("" when none is offered).
func hero_about() -> String:
	for character: CharacterDef in _characters:
		if character.id == character_id:
			return character.description
	return ""


func _build(defs: Array[WeaponDef]) -> void:
	LoadoutLayout.build(self, defs)


static func _stat_line(def: WeaponDef) -> String:
	return LoadoutCards.stat_line(def)


## Takes the suggested pair: the two weapons are tapped in, as if the player had chosen them.
func use_suggestion() -> void:
	if not is_open() or swap_mode:
		return
	# exactly the suggested pair, whatever was picked before
	for id: StringName in pick.picks.duplicate():
		pick.toggle(id)
	for id: StringName in suggestion:
		pick.toggle(id)
	replacement_slot = mini(pick.picks.size(), 1)
	_refresh()


func select_slot(slot: int) -> void:
	replacement_slot = clampi(slot, 0, WeaponPick.SLOTS - 1)
	_refresh()


func selection_hint() -> String:
	if pick.picks.size() >= WeaponPick.SLOTS and WeaponPick.deals_no_damage(pick.picks, _defs):
		return "This pair deals no damage"
	if swap_mode:
		return "Tap a weapon for slot %d" % (replacement_slot + 1)
	if pick.picks.size() < WeaponPick.SLOTS:
		return "Choose 2 weapons" if pick.picks.is_empty() else "Choose 1 more weapon"
	return "Tap a weapon for slot %d" % (replacement_slot + 1)


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
	_close_exit_dialog()
	if pick != null:
		pick.done = true
	visible = false


func request_back() -> void:
	if not is_open():
		return
	if swap_mode:
		close()
	elif untimed:
		exit_requested.emit()
	elif not is_instance_valid(_exit_dialog) or not _exit_dialog.is_open:
		_exit_dialog = DialogButtons.leave_match(hosting, exit_requested.emit)
		_exit_dialog.open(self)


func _close_exit_dialog() -> void:
	if is_instance_valid(_exit_dialog):
		_exit_dialog.close(false)


## The phone's back button asks to leave, or closes the swap.
func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_GO_BACK_REQUEST:
		request_back.call_deferred()
