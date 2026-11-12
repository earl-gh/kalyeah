class_name AbilityArc
extends CardHand
## Right-thumb action cluster for landscape matches:
## - When `uses_elixir` is true: legacy 4-card arc + NEXT + ice-water & stamina bars (100% compatible with legacy tests).
## - When `not uses_elixir` (5v5 Barangay Hero-Action kit): minimalist pure-white outline discs (`HudStyle.draw_white_button`,
##   `HudStyle.draw_white_sweep`, and `assets/ui/hud_icons/<id>.png` silhouettes) for Primary `Tirador`, 3 Street-Game
##   Superpowers, `Bagsak` Ultimate, `Vault`, `Jump`, and smart context-aware `Crouch / Slide`.

const BUTTON_RADIUS: float = 48.0
const NEXT_RADIUS: float = 30.0
const ARC_RADIUS: float = 160.0
const ANGLES: Array[float] = [175.0, 215.0, 255.0, 295.0]
const AIM_RADIUS: float = 160.0
const AIM_DEADZONE: float = 22.0
const CANCEL_RADIUS: float = 40.0
const CANCEL_GAP: float = 18.0
const BARS_WIDTH: float = 360.0
const SLIM_BAR: float = 14.0
const SLIM_DROP: float = 34.0
const STAMINA_BAR: float = 8.0
const BARS_GAP: float = 8.0
const BAR_TRACK: Color = Color(0.035, 0.1, 0.27, 0.6)
const BAR_DIVIDER: Color = Color(0.035, 0.1, 0.27, 0.7)
const ART_SHARE: float = 0.78
const RIM: Color = Color(0.93, 0.95, 1.0, 0.75)
const RIM_HELD: Color = RoyaleStyle.GOLD
const COOL: Color = Color(0.06, 0.05, 0.08, 0.72)
const CANCEL_RIM: Color = Color(1.0, 0.35, 0.3, 0.9)
const CANCEL_HOT: Color = Color(0.85, 0.15, 0.12, 0.85)
const CIRCLE_STEPS: int = 40

## Hero-Action pure-white outline cluster geometry (`not uses_elixir`)
const PRIMARY_RADIUS: float = 50.0
const HERO_BTN_RADIUS: float = 38.0
const HERO_ARC_RADIUS: float = 122.0
const HERO_ANGLES: Array[float] = [180.0, 225.0, 270.0]
const ULT_RADIUS: float = 37.0
const JUMP_RADIUS: float = 31.0
const VAULT_RADIUS: float = 30.0
const CROUCH_RADIUS: float = 30.0
const TAG_BG: Color = Color(0.05, 0.07, 0.10, 0.48)
const TAG_BORDER: Color = Color(1.0, 1.0, 1.0, 0.35)

var inset: Vector2 = Vector2.ZERO
var aim_way: Vector2 = Vector2.ZERO
var centers: Array[Vector2] = []
var next_center: Vector2 = Vector2.ZERO
var corner: Vector2 = Vector2.ZERO
var cancel_center: Vector2 = Vector2.ZERO
var _press_at: Vector2 = Vector2.ZERO
var shoulder: bool = false
var _last_finger: Vector2 = Vector2.ZERO
var stamina_rect: Rect2 = Rect2()
var stamina_shown: bool = false
var stamina_share: float = 1.0
var stamina_color: Color = OverheadHud.STAMINA_COLOR

## Hero-Action state & button centers
var _primary_center: Vector2 = Vector2.ZERO
var _ult_center: Vector2 = Vector2.ZERO
var _jump_center: Vector2 = Vector2.ZERO
var _vault_center: Vector2 = Vector2.ZERO
var _crouch_center: Vector2 = Vector2.ZERO

var _primary_def: WeaponDef = null
var _ult_def: WeaponDef = null
var _primary_cd: float = 0.0
var _primary_cd_max: float = 0.48
var _slot_cds: Array[float] = [0.0, 0.0, 0.0, 0.0]
var _vault_cd: float = 0.0
var _vault_cd_max: float = 4.2
var _vault_active: bool = false
var _vault_ready: bool = false
var _ult_charge: float = 0.0
var _ult_ready: bool = false

var _jump_cd: float = 0.0
var _jump_cd_max: float = 0.25
var _jump_ready: bool = false
var _in_air: bool = false
var _can_move: bool = false
var _is_running: bool = false
var _can_slide: bool = false
var _sliding: bool = false
var _slide_cd: float = 0.0
var _slide_cd_max: float = 3.2
var _crouching_state: bool = false

var _primary_held: bool = false
var _primary_pointer: int = NO_POINTER
var _primary_last_finger: Vector2 = Vector2.ZERO
var _crouch_held: bool = false
var _crouch_pointer: int = NO_POINTER

var _primary_deny: float = 0.0
var _vault_deny: float = 0.0
var _ult_deny: float = 0.0
var _jump_deny: float = 0.0
var _crouch_deny: float = 0.0
var _vault_flash: float = 0.0
var _ult_flash: float = 0.0
var _jump_flash: float = 0.0
var _slide_flash: float = 0.0
var _ult_pulse: float = 0.0

## Cached styleboxes so `_draw()` never allocates StyleBoxFlat
var _stamina_track_box: StyleBoxFlat
var _stamina_fill_box: StyleBoxFlat
var _bar_track_box: StyleBoxFlat
var _bar_fill_box: StyleBoxFlat
var _tag_pill_box: StyleBoxFlat

signal look_dragged(drag: Vector2)
signal primary_changed(held: bool)
signal dash_pressed
signal ultimate_pressed
signal jump_pressed
signal crouch_changed(held: bool)
signal slide_pressed


func _init() -> void:
	_init_styleboxes()
	super()
	name = "AbilityArc"
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func _init_styleboxes() -> void:
	_stamina_track_box = StyleBoxFlat.new()
	_stamina_track_box.bg_color = BAR_TRACK
	_stamina_track_box.set_corner_radius_all(UiSkin.round_radius(STAMINA_BAR))
	_stamina_track_box.anti_aliasing = true

	_stamina_fill_box = StyleBoxFlat.new()
	_stamina_fill_box.bg_color = OverheadHud.STAMINA_COLOR
	_stamina_fill_box.set_corner_radius_all(UiSkin.round_radius(STAMINA_BAR))
	_stamina_fill_box.anti_aliasing = true

	_bar_track_box = StyleBoxFlat.new()
	_bar_track_box.bg_color = BAR_TRACK
	_bar_track_box.set_corner_radius_all(UiSkin.round_radius(SLIM_BAR))
	_bar_track_box.anti_aliasing = true

	_bar_fill_box = StyleBoxFlat.new()
	_bar_fill_box.bg_color = ICE_WATER
	_bar_fill_box.set_corner_radius_all(UiSkin.round_radius(SLIM_BAR))
	_bar_fill_box.anti_aliasing = true

	_tag_pill_box = StyleBoxFlat.new()
	_tag_pill_box.bg_color = TAG_BG
	_tag_pill_box.border_color = TAG_BORDER
	_tag_pill_box.set_border_width_all(1)
	_tag_pill_box.set_corner_radius_all(UiSkin.RADIUS_S)
	_tag_pill_box.anti_aliasing = true


func set_inset(value: Vector2) -> void:
	inset = value
	_layout()


static func corner_point(screen: Vector2, insets: Vector2) -> Vector2:
	var reach: Vector2 = Vector2.ZERO
	for angle: float in ANGLES:
		var way: Vector2 = Vector2.from_angle(deg_to_rad(angle)) * ARC_RADIUS
		reach = reach.max(way)
	var offset: Vector2 = reach + Vector2.ONE * (BUTTON_RADIUS + HudStyle.EDGE)
	return screen - insets - offset


func _active_slot_radius() -> float:
	return BUTTON_RADIUS if uses_elixir else HERO_BTN_RADIUS


func _layout() -> void:
	corner = corner_point(size, inset)
	centers.clear()
	card_rects.clear()
	if uses_elixir:
		for angle: float in ANGLES:
			var middle: Vector2 = corner + Vector2.from_angle(deg_to_rad(angle)) * ARC_RADIUS
			centers.append(middle)
			card_rects.append(Rect2(middle - Vector2.ONE * BUTTON_RADIUS, Vector2.ONE * BUTTON_RADIUS * 2.0))
		next_center = corner + Vector2(NEXT_RADIUS * 0.6, NEXT_RADIUS * 0.4)
		next_rect = Rect2(next_center - Vector2.ONE * NEXT_RADIUS, Vector2.ONE * NEXT_RADIUS * 2.0)
		var top_legacy: float = card_rects[0].position.y
		for rect: Rect2 in card_rects:
			top_legacy = minf(top_legacy, rect.position.y)
		cancel_center = Vector2(corner.x, top_legacy - CANCEL_GAP - CANCEL_RADIUS)
	else:
		var anchor: Vector2 = size - inset
		_primary_center = anchor - Vector2(86.0, 84.0)
		for angle: float in HERO_ANGLES:
			var hero_mid: Vector2 = _primary_center + Vector2.from_angle(deg_to_rad(angle)) * HERO_ARC_RADIUS
			centers.append(hero_mid)
			card_rects.append(Rect2(hero_mid - Vector2.ONE * HERO_BTN_RADIUS, Vector2.ONE * HERO_BTN_RADIUS * 2.0))
		# Keep 4th slot entry inside safe right-side bounds & non-overlapping so SLOTS == 4 indexing stays safe
		var extra_mid: Vector2 = _primary_center + Vector2(-312.0, -10.0)
		centers.append(extra_mid)
		card_rects.append(Rect2(extra_mid - Vector2.ONE * HERO_BTN_RADIUS, Vector2.ONE * HERO_BTN_RADIUS * 2.0))

		_ult_center = _primary_center + Vector2(-168.0, -134.0)
		_jump_center = _primary_center + Vector2(8.0, -204.0)
		_vault_center = _primary_center + Vector2(-208.0, -44.0)
		_crouch_center = _primary_center + Vector2(-206.0, 34.0)

		next_center = _primary_center
		next_rect = Rect2(_primary_center - Vector2.ONE * NEXT_RADIUS, Vector2.ONE * NEXT_RADIUS * 2.0)
		var top_slot_y: float = card_rects[2].position.y
		cancel_center = Vector2(_primary_center.x - 42.0, maxf(top_slot_y - CANCEL_GAP - CANCEL_RADIUS - 48.0, CANCEL_RADIUS + 112.0))

	chat_rect = Rect2()
	var left: float = (size.x - BARS_WIDTH) / 2.0
	stamina_rect = Rect2(Vector2(left, size.y - inset.y - HudStyle.EDGE - STAMINA_BAR), Vector2(BARS_WIDTH, STAMINA_BAR))
	bar_rect = Rect2(Vector2(left, stamina_rect.position.y - BARS_GAP - SLIM_BAR), Vector2(BARS_WIDTH, SLIM_BAR))
	_refresh()


func show_state(sim: MatchSim, player: PlayerState, playing: bool, block: String, notice: Array[String]) -> void:
	var prev_elixir_mode: bool = uses_elixir
	var next_elixir_mode: bool = sim.pays_elixir(player)
	_primary_def = sim.rules.primary_weapon
	_ult_def = sim.rules.ultimate_weapon
	_primary_cd = maxf(player.primary_cd, 0.0)
	_primary_cd_max = _primary_def.cooldown if (_primary_def != null and _primary_def.cooldown > 0.0) else 0.48
	_vault_cd = maxf(player.dash_cd, 0.0)
	_vault_cd_max = maxf(sim.rules.dash_cooldown, 0.001)
	_vault_active = player.dash_left > 0.0
	_vault_ready = playing and not player.tagged and player.effects.can_cast() and _vault_cd <= 0.001 and not _vault_active
	_ult_charge = clampf(player.ult_charge, 0.0, 100.0)
	_ult_ready = playing and block.is_empty() and _ult_charge >= 99.99 and _ult_def != null
	_ult_pulse = sim.match_time

	var is_walking: bool = player.walking
	_crouching_state = player.crouching
	var air_y: float = player.air_y
	_jump_cd = maxf(player.jump_cd, 0.0)
	_jump_cd_max = maxf(sim.rules.jump_cooldown, 0.05)
	var slide_left: float = player.slide_left
	_slide_cd = maxf(player.slide_cd, 0.0)
	_slide_cd_max = maxf(sim.rules.slide_cooldown, 0.1)

	_can_move = playing and not player.tagged and player.effects.can_move()
	_in_air = air_y > 0.02
	_jump_ready = _can_move and not _in_air and _jump_cd <= 0.01
	_sliding = slide_left > 0.01
	_is_running = _can_move and not is_walking and not _crouching_state and not player.overclocked and (player.run_time > 0.01 or player.sprinting)
	_can_slide = _is_running and not _in_air and _slide_cd <= 0.01 and not _sliding

	for slot: int in SLOTS:
		_slot_cds[slot] = player.weapon_cooldowns[slot] if slot < player.weapon_cooldowns.size() else 0.0

	if _primary_held and (not playing or not block.is_empty() or player.tagged):
		_primary_pointer = NO_POINTER
		_set_primary_held(false)
	if _crouch_held and (not playing or player.tagged or not player.effects.can_move()):
		_crouch_pointer = NO_POINTER
		_set_crouch_held(false)

	super(sim, player, playing, block, notice)
	if prev_elixir_mode != next_elixir_mode:
		_layout()
	elif not uses_elixir:
		_refresh()


func step(delta: float) -> bool:
	var moving: bool = super(delta)
	for val: float in [_primary_deny, _vault_deny, _ult_deny, _jump_deny, _crouch_deny, _vault_flash, _ult_flash, _jump_flash, _slide_flash]:
		if val > 0.0:
			moving = true
	_primary_deny = maxf(0.0, _primary_deny - delta)
	_vault_deny = maxf(0.0, _vault_deny - delta)
	_ult_deny = maxf(0.0, _ult_deny - delta)
	_jump_deny = maxf(0.0, _jump_deny - delta)
	_crouch_deny = maxf(0.0, _crouch_deny - delta)
	_vault_flash = maxf(0.0, _vault_flash - delta)
	_ult_flash = maxf(0.0, _ult_flash - delta)
	_jump_flash = maxf(0.0, _jump_flash - delta)
	_slide_flash = maxf(0.0, _slide_flash - delta)
	if not uses_elixir and _ult_charge >= 99.99 and not Settings.reduced_motion:
		_ult_pulse = fposmod(_ult_pulse + delta, 100.0)
		moving = true
	return moving


func _set_primary_held(on: bool) -> void:
	if _primary_held == on:
		return
	_primary_held = on
	primary_changed.emit(on)
	_refresh()


func _set_crouch_held(on: bool) -> void:
	if _crouch_held == on:
		return
	_crouch_held = on
	crouch_changed.emit(on)
	_refresh()


func cancel_hold() -> void:
	if _primary_held or _primary_pointer != NO_POINTER:
		_primary_pointer = NO_POINTER
		_set_primary_held(false)
	if _crouch_held or _crouch_pointer != NO_POINTER:
		_crouch_pointer = NO_POINTER
		_set_crouch_held(false)
	super()


func _sync_faces() -> void:
	for face: HandCard in [_next_face, _held_face] + _faces:
		face.visible = false


func _card_changed(_slot: int, _old: WeaponDef, _new: WeaponDef, _old_next: WeaponDef) -> void:
	_released_slot = -1


func slot_at(local: Vector2) -> int:
	var r: float = _active_slot_radius()
	var count: int = centers.size()
	for slot: int in count:
		if not uses_elixir and defs[slot] == null:
			continue
		if local.distance_to(centers[slot]) <= r + CARD_GAP:
			return slot
	return -1


func _over_hand(local: Vector2) -> bool:
	return held_slot >= 0 and local.distance_to(cancel_center) <= CANCEL_RADIUS + CARD_GAP


func held_card_rect() -> Rect2:
	return Rect2()


func _input(event: InputEvent) -> void:
	if not uses_elixir:
		if event is InputEventScreenTouch:
			var touch: InputEventScreenTouch = event as InputEventScreenTouch
			if touch.pressed:
				if active and is_visible_in_tree():
					var local: Vector2 = _local(touch.position)
					if _primary_pointer == NO_POINTER and local.distance_to(_primary_center) <= PRIMARY_RADIUS + 6.0:
						if is_inside_tree():
							get_viewport().set_input_as_handled()
						if blocked_text.is_empty():
							_primary_pointer = touch.index
							_primary_last_finger = local
							_set_primary_held(true)
						else:
							_primary_deny = DENY_TIME
							Settings.vibrate(DENY_BUZZ_MS)
							_refresh()
						return
					if local.distance_to(_vault_center) <= VAULT_RADIUS + 6.0:
						if is_inside_tree():
							get_viewport().set_input_as_handled()
						if _vault_ready:
							_vault_flash = 0.18
							dash_pressed.emit()
						else:
							_vault_deny = DENY_TIME
							Settings.vibrate(DENY_BUZZ_MS)
						_refresh()
						return
					if local.distance_to(_ult_center) <= ULT_RADIUS + 6.0:
						if is_inside_tree():
							get_viewport().set_input_as_handled()
						if _ult_ready:
							_ult_flash = 0.24
							ultimate_pressed.emit()
						else:
							_ult_deny = DENY_TIME
							Settings.vibrate(DENY_BUZZ_MS)
						_refresh()
						return
					if local.distance_to(_jump_center) <= JUMP_RADIUS + 6.0:
						if is_inside_tree():
							get_viewport().set_input_as_handled()
						if _jump_ready:
							_jump_flash = 0.16
							jump_pressed.emit()
						else:
							_jump_deny = DENY_TIME
							Settings.vibrate(DENY_BUZZ_MS)
						_refresh()
						return
					if _crouch_pointer == NO_POINTER and local.distance_to(_crouch_center) <= CROUCH_RADIUS + 6.0:
						if is_inside_tree():
							get_viewport().set_input_as_handled()
						if _can_move:
							if _can_slide:
								_slide_flash = 0.20
								slide_pressed.emit()
							else:
								_crouch_pointer = touch.index
								_set_crouch_held(true)
						else:
							_crouch_deny = DENY_TIME
							Settings.vibrate(DENY_BUZZ_MS)
						_refresh()
						return
			else:
				if touch.index == _primary_pointer:
					_primary_pointer = NO_POINTER
					_set_primary_held(false)
					return
				if touch.index == _crouch_pointer:
					_crouch_pointer = NO_POINTER
					_set_crouch_held(false)
					return
		elif event is InputEventScreenDrag:
			var drag: InputEventScreenDrag = event as InputEventScreenDrag
			if drag.index == _primary_pointer:
				var cur_local: Vector2 = _local(drag.position)
				if shoulder:
					look_dragged.emit(cur_local - _primary_last_finger)
				_primary_last_finger = cur_local
				if drag.index != _pointer:
					return
	super(event)


func _press(pointer: int, viewport_position: Vector2) -> void:
	var on_button: bool = active and is_visible_in_tree() and held_slot < 0 and slot_at(_local(viewport_position)) >= 0
	super(pointer, viewport_position)
	if on_button and is_inside_tree():
		get_viewport().set_input_as_handled()
	if held_slot >= 0:
		_press_at = finger
		_last_finger = finger
		aim_way = Vector2.ZERO
		over_hand = false


func _move(viewport_position: Vector2) -> void:
	if held_slot < 0:
		return
	finger = _local(viewport_position)
	if shoulder:
		look_dragged.emit(finger - _last_finger)
		_last_finger = finger
		aim_way = Vector2.ZERO
	else:
		aim_way = way_for(held_slot, finger)
	on_card = aim_way == Vector2.ZERO and not over_hand and finger.distance_to(_press_at) <= AIM_DEADZONE
	over_hand = _over_hand(finger)
	if over_hand:
		on_card = false
	card_moved.emit(held_slot, viewport_position, on_card, over_hand)
	_refresh()


func way_for(slot: int, local: Vector2) -> Vector2:
	var offset: Vector2 = local - centers[slot]
	if offset.length() <= AIM_DEADZONE:
		return Vector2.ZERO
	return (offset / AIM_RADIUS).limit_length(1.0)


func _end_hold() -> void:
	aim_way = Vector2.ZERO
	super()


# ---- drawing --------------------------------------------------------------------

func _draw() -> void:
	if centers.size() < SLOTS:
		return
	_draw_stamina()
	if not uses_elixir:
		_draw_hero_kit()
		return
	_draw_bar()
	if next_def != null:
		_draw_button(next_center, NEXT_RADIUS, next_def, 1.0, false)
		if next_fill < 1.0:
			_draw_cooling_on(self, next_center, NEXT_RADIUS, next_fill)
	else:
		HudStyle.draw_disc(self, next_center, NEXT_RADIUS)
	_draw_next_label()
	for slot: int in SLOTS:
		var middle: Vector2 = centers[slot] + Vector2(deny_offset(slot), 0.0)
		var def: WeaponDef = defs[slot]
		if def == null:
			HudStyle.draw_disc(self, middle, BUTTON_RADIUS)
			continue
		var dim: float = BLOCK_DIM if not blocked_text.is_empty() else 0.0
		_draw_button(middle, BUTTON_RADIUS, def, 1.0 - dim, slot == held_slot)
	if held_slot >= 0:
		_draw_cancel()


func _draw_hero_kit() -> void:
	var blocked: bool = not blocked_text.is_empty()

	# 1. Primary Fire (`Tirador`) — pure white outline disc + white silhouette
	var prim_at: Vector2 = _primary_center + Vector2(_shake_for(_primary_deny), 0.0)
	HudStyle.draw_white_button(self, prim_at, PRIMARY_RADIUS, _primary_held)
	if _primary_cd > 0.02 and _primary_cd_max > 0.05:
		var prim_share: float = 1.0 - clampf(_primary_cd / _primary_cd_max, 0.0, 1.0)
		HudStyle.draw_white_sweep(self, prim_at, PRIMARY_RADIUS, prim_share)
	var prim_id: StringName = &"tirador" if (_primary_def == null or _primary_def.id == &"tirador_bato") else _primary_def.id
	var prim_alpha: float = HudStyle.WHITE_OFF if blocked else 0.96
	HudStyle.draw_white_icon_or_glyph(self, prim_at, PRIMARY_RADIUS, prim_id, prim_alpha)

	# 2. 3 Street-Game Superpowers — pure white outline discs + clockwise white sweeps
	for slot: int in SLOTS:
		var def: WeaponDef = defs[slot]
		if def == null:
			continue
		var middle: Vector2 = centers[slot] + Vector2(deny_offset(slot), 0.0)
		var is_held: bool = slot == held_slot
		var cd_left: float = _slot_cds[slot]
		var ready_now: bool = not blocked and cd_left <= 0.02 and pickable[slot]
		HudStyle.draw_white_button(self, middle, HERO_BTN_RADIUS, is_held)
		if not blocked and cd_left > 0.02:
			HudStyle.draw_white_sweep(self, middle, HERO_BTN_RADIUS, reached[slot])
		var slot_alpha: float = 0.96 if (ready_now or is_held) else HudStyle.WHITE_OFF
		HudStyle.draw_white_icon_or_glyph(self, middle, HERO_BTN_RADIUS, def.id, slot_alpha)

	# 3. Ultimate (`ult_bagsak`) — 0-100% white sweep + soft outer white halo when ready
	var ult_at: Vector2 = _ult_center + Vector2(_shake_for(_ult_deny), 0.0)
	var ult_full: bool = _ult_charge >= 99.99
	HudStyle.draw_white_button(self, ult_at, ULT_RADIUS, _ult_flash > 0.0)
	var charge_share: float = clampf(_ult_charge / 100.0, 0.0, 1.0)
	if not ult_full and charge_share > 0.005:
		HudStyle.draw_white_sweep(self, ult_at, ULT_RADIUS, charge_share)
	elif ult_full:
		var halo_a: float = 0.48 if Settings.reduced_motion else (0.36 + 0.24 * (0.5 + 0.5 * sin(_ult_pulse * 4.2)))
		draw_arc(ult_at, ULT_RADIUS + 4.5, 0.0, TAU, 48, Color(1.0, 1.0, 1.0, halo_a), 1.8, true)
	var ult_id: StringName = _ult_def.id if _ult_def != null else &"ult_bagsak"
	var ult_alpha: float = 0.96 if (ult_full and not blocked) else HudStyle.WHITE_OFF
	HudStyle.draw_white_icon_or_glyph(self, ult_at, ULT_RADIUS, ult_id, ult_alpha)

	# 4. Vault (`vault`) — pure white outline disc + cooldown sweep
	var vault_at: Vector2 = _vault_center + Vector2(_shake_for(_vault_deny), 0.0)
	HudStyle.draw_white_button(self, vault_at, VAULT_RADIUS, _vault_active or _vault_flash > 0.0)
	if _vault_cd > 0.02:
		var vault_share: float = 1.0 - clampf(_vault_cd / _vault_cd_max, 0.0, 1.0)
		HudStyle.draw_white_sweep(self, vault_at, VAULT_RADIUS, vault_share)
	var vault_alpha: float = 0.95 if (_vault_ready or _vault_active) else HudStyle.WHITE_OFF
	HudStyle.draw_white_icon_or_glyph(self, vault_at, VAULT_RADIUS, &"vault", vault_alpha)

	# 5. Jump (`jump`) — pure white outline disc
	var jump_at: Vector2 = _jump_center + Vector2(_shake_for(_jump_deny), 0.0)
	HudStyle.draw_white_button(self, jump_at, JUMP_RADIUS, _in_air or _jump_flash > 0.0)
	if _jump_cd > 0.02:
		var jump_share: float = 1.0 - clampf(_jump_cd / _jump_cd_max, 0.0, 1.0)
		HudStyle.draw_white_sweep(self, jump_at, JUMP_RADIUS, jump_share)
	var jump_alpha: float = 0.95 if (_jump_ready or _in_air) else HudStyle.WHITE_OFF
	HudStyle.draw_white_icon_or_glyph(self, jump_at, JUMP_RADIUS, &"jump", jump_alpha)

	# 6. Smart Crouch / Slide (`crouch` while stationary/walking, `slide` while running)
	var crouch_at: Vector2 = _crouch_center + Vector2(_shake_for(_crouch_deny), 0.0)
	var show_slide: bool = _can_slide or _sliding
	HudStyle.draw_white_button(self, crouch_at, CROUCH_RADIUS, _crouch_held or _crouching_state or _sliding or _slide_flash > 0.0)
	if _slide_cd > 0.02 and not _crouching_state:
		var slide_share: float = 1.0 - clampf(_slide_cd / _slide_cd_max, 0.0, 1.0)
		HudStyle.draw_white_sweep(self, crouch_at, CROUCH_RADIUS, slide_share)
	var cs_icon: StringName = &"slide" if show_slide else &"crouch"
	var cs_alpha: float = 0.95 if (not blocked and _can_move) else HudStyle.WHITE_OFF
	HudStyle.draw_white_icon_or_glyph(self, crouch_at, CROUCH_RADIUS, cs_icon, cs_alpha)

	if held_slot >= 0:
		_draw_cancel()


func _shake_for(timer: float) -> float:
	if Settings.reduced_motion or timer <= 0.0:
		return 0.0
	var t: float = 1.0 - timer / DENY_TIME
	return sin(t * TAU * DENY_WAVES) * DENY_SHAKE * (1.0 - t)


func _draw_next_label() -> void:
	var font: Font = KalyeahTheme.ACTION_FONT
	var label: String = next_label()
	var width: float = font.get_string_size(label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL).x
	var at: Vector2 = Vector2(next_center.x - width / 2.0, next_center.y - NEXT_RADIUS - 6.0)
	draw_string_outline(font, at, label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, HudStyle.outline(HudStyle.SMALL), HudStyle.INK)
	draw_string(font, at, label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, RoyaleStyle.CREAM)


func _draw_button(middle: Vector2, radius: float, def: WeaponDef, light: float, held: bool) -> void:
	var tint: Color = Color(light, light, light)
	var picture: Texture2D = CardStyle.picture(def.id)
	if picture != null:
		var share: Vector2 = Vector2(ART_SHARE, ART_SHARE * float(picture.get_width()) / float(picture.get_height()))
		_draw_round_texture(middle, radius, picture, share, tint)
	else:
		draw_circle(middle, radius, CardStyle.ground(def.kind) * tint)
		var icon: Texture2D = Icons.skill(def.id)
		if icon != null:
			_draw_round_texture(middle, radius * 0.8, icon, Vector2.ONE, tint)
	var rim_color: Color = RIM_HELD if held else RIM
	draw_arc(middle, radius, 0.0, TAU, CIRCLE_STEPS, rim_color, HudStyle.RIM_WIDTH, true)


func _draw_round_texture(middle: Vector2, radius: float, texture: Texture2D, share: Vector2, tint: Color) -> void:
	var points: PackedVector2Array = PackedVector2Array()
	var uvs: PackedVector2Array = PackedVector2Array()
	for i: int in CIRCLE_STEPS:
		var way: Vector2 = Vector2.from_angle(TAU * float(i) / float(CIRCLE_STEPS))
		points.append(middle + way * radius)
		uvs.append(Vector2(0.5, 0.5) + way * share / 2.0)
	draw_polygon(points, PackedColorArray([tint]), uvs, texture)


func _draw_cancel() -> void:
	if not uses_elixir:
		HudStyle.draw_white_button(self, cancel_center, CANCEL_RADIUS, over_hand)
		var arm_w: float = CANCEL_RADIUS * 0.34
		for way_w: Vector2 in [Vector2(1.0, 1.0), Vector2(1.0, -1.0)]:
			draw_line(cancel_center - way_w * arm_w, cancel_center + way_w * arm_w, HudStyle.WHITE_THUMB, 3.2, true)
		return
	HudStyle.draw_disc(self, cancel_center, CANCEL_RADIUS, CANCEL_RIM)
	if over_hand:
		draw_circle(cancel_center, CANCEL_RADIUS - HudStyle.RIM_WIDTH, CANCEL_HOT)
	var arm: float = CANCEL_RADIUS * 0.38
	for way: Vector2 in [Vector2(1.0, 1.0), Vector2(1.0, -1.0)]:
		draw_line(cancel_center - way * arm, cancel_center + way * arm, HudStyle.THUMB, 5.0, true)


func show_stamina(shown: bool, share: float, color: Color) -> void:
	if shown == stamina_shown and is_equal_approx(share, stamina_share) and color == stamina_color:
		return
	stamina_shown = shown
	stamina_share = share
	stamina_color = color
	queue_redraw()


func _draw_stamina() -> void:
	if not stamina_shown or stamina_rect.size.x <= 0.0:
		return
	draw_style_box(_stamina_track_box, stamina_rect)
	if stamina_share > 0.0:
		_stamina_fill_box.bg_color = stamina_color
		draw_style_box(_stamina_fill_box, Rect2(stamina_rect.position, Vector2(maxf(stamina_rect.size.y, stamina_rect.size.x * stamina_share), stamina_rect.size.y)))


func _draw_bar() -> void:
	if not uses_elixir or bar_rect.size.x <= 0.0:
		return
	draw_style_box(_bar_track_box, bar_rect)
	var share: float = clampf(shown_elixir / maxf(elixir_max, 0.001), 0.0, 1.0)
	if share > 0.0:
		draw_style_box(_bar_fill_box, Rect2(bar_rect.position, Vector2(maxf(bar_rect.size.y, bar_rect.size.x * share), bar_rect.size.y)))
	for index: int in range(1, bar_segments()):
		var x: float = bar_rect.position.x + bar_rect.size.x * float(index) / float(bar_segments())
		draw_line(Vector2(x, bar_rect.position.y + 2.0), Vector2(x, bar_rect.end.y - 2.0), BAR_DIVIDER, 2.0)
	HandArt.draw_ice(self, Vector2(bar_rect.position.x, bar_rect.get_center().y), SLIM_DROP, str(floori(elixir + 0.001)), HudStyle.SMALL)


func _draw_over_cards() -> void:
	if centers.size() < SLOTS:
		return
	if not uses_elixir:
		_draw_over_hero_kit()
		return
	for slot: int in SLOTS:
		var def: WeaponDef = defs[slot]
		if def == null:
			continue
		var middle: Vector2 = centers[slot] + Vector2(deny_offset(slot), 0.0)
		var blocked: bool = not blocked_text.is_empty()
		if not blocked and not pickable[slot] and reached[slot] < 1.0:
			_overlay.draw_circle(middle, BUTTON_RADIUS, Color(COOL, COOL.a * 0.4))
			_draw_cooling_on(_overlay, middle, BUTTON_RADIUS, reached[slot])
		if uses_elixir:
			var drop: float = BUTTON_RADIUS * 0.62
			HandArt.draw_ice(_overlay, middle + Vector2(0.0, BUTTON_RADIUS * 0.78), drop, str(def.elixir_cost), COST_FONT_MIN, pickable[slot] or reached[slot] >= 1.0)
		var tag: String = blocked_text if blocked else notices[slot]
		if not tag.is_empty():
			_draw_tag(_overlay, middle, UiText.t(tag))
		if _deny[slot] > 0.0:
			_overlay.draw_circle(middle, BUTTON_RADIUS, Color(DENY_FLASH, DENY_FLASH.a * _deny[slot] / DENY_TIME))
	if held_slot >= 0 and aim_way != Vector2.ZERO:
		_overlay.draw_circle(centers[held_slot] + aim_way * AIM_RADIUS, BUTTON_RADIUS * 0.45, HudStyle.THUMB)


func _draw_over_hero_kit() -> void:
	var blocked: bool = not blocked_text.is_empty()

	var prim_at: Vector2 = _primary_center + Vector2(_shake_for(_primary_deny), 0.0)
	if _primary_deny > 0.0:
		_overlay.draw_circle(prim_at, PRIMARY_RADIUS, Color(1.0, 1.0, 1.0, 0.20 * (_primary_deny / DENY_TIME)))

	for slot: int in SLOTS:
		var def: WeaponDef = defs[slot]
		if def == null:
			continue
		var middle: Vector2 = centers[slot] + Vector2(deny_offset(slot), 0.0)
		var cd_left: float = _slot_cds[slot]
		if not blocked and cd_left > 0.02:
			_draw_cd_seconds(_overlay, middle, cd_left)
		var tag: String = blocked_text if blocked else notices[slot]
		if not tag.is_empty():
			_draw_tag(_overlay, middle, UiText.t(tag))
		if _deny[slot] > 0.0:
			_overlay.draw_circle(middle, HERO_BTN_RADIUS, Color(1.0, 1.0, 1.0, 0.20 * (_deny[slot] / DENY_TIME)))

	var ult_at: Vector2 = _ult_center + Vector2(_shake_for(_ult_deny), 0.0)
	var ult_full: bool = _ult_charge >= 99.99
	if not ult_full:
		var pct_text: String = "%d%%" % floori(_ult_charge)
		_draw_centered_word(_overlay, ult_at + Vector2(0.0, ULT_RADIUS * 0.56), pct_text, UiMetrics.WORLD_SMALL, Color(1.0, 1.0, 1.0, 0.82))
	if _ult_flash > 0.0:
		_overlay.draw_circle(ult_at, ULT_RADIUS, Color(1.0, 1.0, 1.0, 0.26 * (_ult_flash / 0.24)))
	if _ult_deny > 0.0:
		_overlay.draw_circle(ult_at, ULT_RADIUS, Color(1.0, 1.0, 1.0, 0.20 * (_ult_deny / DENY_TIME)))

	var vault_at: Vector2 = _vault_center + Vector2(_shake_for(_vault_deny), 0.0)
	if _vault_cd > 0.02:
		_draw_cd_seconds(_overlay, vault_at, _vault_cd)
	if _vault_flash > 0.0:
		_overlay.draw_circle(vault_at, VAULT_RADIUS, Color(1.0, 1.0, 1.0, 0.24 * (_vault_flash / 0.18)))
	if _vault_deny > 0.0:
		_overlay.draw_circle(vault_at, VAULT_RADIUS, Color(1.0, 1.0, 1.0, 0.20 * (_vault_deny / DENY_TIME)))

	var jump_at: Vector2 = _jump_center + Vector2(_shake_for(_jump_deny), 0.0)
	if _jump_flash > 0.0:
		_overlay.draw_circle(jump_at, JUMP_RADIUS, Color(1.0, 1.0, 1.0, 0.22 * (_jump_flash / 0.16)))

	var crouch_at: Vector2 = _crouch_center + Vector2(_shake_for(_crouch_deny), 0.0)
	if _is_running and _slide_cd > 0.15 and not _crouching_state:
		_draw_cd_seconds(_overlay, crouch_at, _slide_cd)
	if _slide_flash > 0.0:
		_overlay.draw_circle(crouch_at, CROUCH_RADIUS, Color(1.0, 1.0, 1.0, 0.24 * (_slide_flash / 0.20)))

	if held_slot >= 0 and aim_way != Vector2.ZERO:
		_overlay.draw_circle(centers[held_slot] + aim_way * AIM_RADIUS, HERO_BTN_RADIUS * 0.42, HudStyle.WHITE_THUMB)


func _draw_cd_seconds(canvas: CanvasItem, center: Vector2, seconds_left: float) -> void:
	var text: String = "%.1f" % seconds_left if seconds_left < 1.0 else "%d" % ceili(seconds_left)
	_draw_centered_word(canvas, center + Vector2(0.0, 7.0), text, HudStyle.LABEL, Color(1.0, 1.0, 1.0, 0.94))


func _draw_centered_word(canvas: CanvasItem, baseline_center: Vector2, word: String, size_px: int, fill: Color) -> void:
	var font: Font = KalyeahTheme.ACTION_FONT
	var tw: float = font.get_string_size(word, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size_px).x
	var at: Vector2 = Vector2(baseline_center.x - tw * 0.5, baseline_center.y)
	var drop: Vector2 = Vector2(0.0, HudStyle.WORD_SHADOW_DROP)
	canvas.draw_string(font, at + drop, word, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size_px, HudStyle.WORD_SHADOW)
	canvas.draw_string(font, at, word, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size_px, fill)


func _draw_cooling_on(canvas: CanvasItem, middle: Vector2, radius: float, reached_share: float) -> void:
	var missing: float = 1.0 - clampf(reached_share, 0.0, 1.0)
	if missing <= 0.0:
		return
	var points: PackedVector2Array = PackedVector2Array([middle])
	var steps: int = maxi(2, ceili(CIRCLE_STEPS * missing))
	for i: int in steps + 1:
		var angle: float = -PI / 2.0 + TAU * (1.0 - missing) + TAU * missing * float(i) / float(steps)
		points.append(middle + Vector2.from_angle(angle) * radius)
	canvas.draw_colored_polygon(points, COOL)


func _draw_tag(canvas: CanvasItem, at: Vector2, text: String) -> void:
	var font: Font = KalyeahTheme.ACTION_FONT
	var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.CAPTION).x + 18.0
	var pos: Vector2 = at - Vector2(width / 2.0, TAG_HEIGHT / 2.0)
	canvas.draw_style_box(_tag_pill_box, Rect2(pos, Vector2(width, TAG_HEIGHT)))
	var baseline: Vector2 = at + Vector2(-width / 2.0 + 9.0, float(UiMetrics.CAPTION) * 0.34)
	canvas.draw_string(font, baseline + Vector2(0.0, 1.5), text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.CAPTION, HudStyle.WORD_SHADOW)
	canvas.draw_string(font, baseline, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.CAPTION, Color(1.0, 1.0, 1.0, 0.94))
