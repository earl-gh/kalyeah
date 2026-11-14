class_name OverheadHud
extends Control
## Clean, non-intrusive overhead health bar, team-slot badge, and status/name word projected over heroes.
## Preserves all test contracts (`test_hud_bars`, `test_overhead_badges`, `test_overhead_label_fit`) while
## softening border & text outline contrast so sunlit 3D gameplay stays visually airy.

## The bar before `bar_scale`.
const BAR_SIZE: Vector2 = Vector2(96.0, 18.0)
## The dark outline round the bar: thin (screen px, kept at any bar_scale).
const BAR_OUTLINE: float = 2.0
## The word over a bar, at its top left (its middle level with the bar's top outline).
const LABEL_FONT: int = UiMetrics.WORLD_SMALL
const LABEL_LINE: float = float(LABEL_FONT) * 0.8
const LABEL_FONT_MIN: int = UiMetrics.WORLD_MIN
const LABEL_OUTLINE_SHARE: float = 0.3
const BADGE_FONT: int = UiMetrics.WORLD
const BADGE_PAD: float = 4.0
const BADGE_SIZE: float = float(BADGE_FONT) + BADGE_PAD * 2.0
const BADGE_OVERLAP: float = 6.0
const BADGE_TEXT_OUTLINE: int = 2
const LABEL_INSET: float = BADGE_OVERLAP + BAR_OUTLINE
const EDGE_PAD: float = 4.0
const HEAD_HEIGHT: float = KidModel.KID_HEIGHT + 0.2
const TEXT_OUTLINE: int = 5
const POPUP_TIME: float = 0.9
const POPUP_RISE: float = 56.0
const STATUS_FONT: int = LABEL_FONT
const STAMINA_SIZE: Vector2 = Vector2(96.0, 8.0)
const STAMINA_LOW: float = 0.25
const STAMINA_COLOR: Color = Color(0.22, 0.52, 1.0)
const STAMINA_LOW_COLOR: Color = ConsolePanel.HEALTH_AMBER
const EXHAUSTED_COLOR: Color = ConsolePanel.HEALTH_RED
const BLINK_PERIOD: float = 0.25
const EXHAUSTED: String = "Exhausted"
const AWAY: String = "AFK"
const POPUP_FONT: int = UiMetrics.BODY
const SAY_ART: float = 60.0
const SAY_LIFT: float = 2.0
const SAY_POP: float = 0.18
const SAY_FROM: float = 0.7
const SAY_ALPHA: float = 0.9
const EMPTY_BAR: Color = Color(0.18, 0.19, 0.21)
const SOFT_BORDER: Color = Color(0.10, 0.11, 0.14, 0.62)
const SOFT_LABEL_OUTLINE: Color = Color(0.08, 0.09, 0.12, 0.64)
const SELF_COLOR: Color = Color(0.35, 0.95, 0.35)
const ALLY_COLOR: Color = Color(0.35, 0.65, 1.0)
const ENEMY_COLOR: Color = Color(1.0, 0.3, 0.3)
const DAMAGE_COLOR: Color = Color(1.0, 0.95, 0.85)
const HEAL_COLOR: Color = Color(0.55, 1.0, 0.45)
const SPINNER_RADIUS: float = 14.0
const SPINNER_LIFT: float = 34.0
const SPINNER_COLOR: Color = Color(1.0, 0.86, 0.3)
const RESCUE_COLOR: Color = Color(0.45, 0.9, 0.5)
const SPINNER_BACK: Color = Color(0.08, 0.09, 0.12, 0.48)
const SPINNER_SPEED: float = 5.0

var blend: TickBlend
var view_scale: float = 1.0
var bar_scale: float = 1.0
var top_limit: float = 0.0

var _sim: MatchSim
var _camera: Camera3D
var _local_id: int = -1
var _local_team: int = 0
var taya_id: int = -1
var hidden_ids: Dictionary[int, bool] = {}
var _bar_back: StyleBoxFlat
var _bar_fill: StyleBoxFlat
var _bar_gloss: StyleBoxFlat
var _badge_box: StyleBoxFlat
var _placed: Array[Rect2] = []
var _popups: Array[Dictionary] = []
var _says: Array[Dictionary] = []
var away_ids: Dictionary[int, bool] = {}
var names: Dictionary[int, String] = {}
var name_reach: float = 0.0
var stamina_on_bar: bool = true


func setup(sim: MatchSim, camera: Camera3D, local_id: int, local_team: int) -> void:
	_sim = sim
	_camera = camera
	_local_id = local_id
	_local_team = local_team
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	sim.player_healed.connect(_on_healed)
	_bar_back = StyleBoxFlat.new()
	_bar_back.bg_color = EMPTY_BAR
	_bar_back.border_color = SOFT_BORDER
	_bar_back.anti_aliasing = true
	_bar_fill = StyleBoxFlat.new()
	_bar_fill.anti_aliasing = true
	_bar_gloss = StyleBoxFlat.new()
	_bar_gloss.bg_color = Color(1.0, 1.0, 1.0, 0.20)
	_bar_gloss.anti_aliasing = true
	_badge_box = StyleBoxFlat.new()
	_badge_box.border_color = SOFT_BORDER
	_badge_box.set_border_width_all(int(BAR_OUTLINE))
	_badge_box.set_corner_radius_all(UiSkin.RADIUS_S)
	_badge_box.anti_aliasing = true


func text_for(id: int) -> String:
	return _status_text(_sim.players[id])


func _status_text(state: PlayerState) -> String:
	var words: PackedStringArray = PackedStringArray()
	if away_ids.has(state.id):
		words.append(AWAY)
	var effects: PackedStringArray = state.effects.display_names()
	if not effects.is_empty():
		for word: String in effects:
			words.append(UiText.t(word))
	elif state.overclocked:
		words.append(UiText.t(EXHAUSTED))
	return " · ".join(words)


func stamina_visible(state: PlayerState) -> bool:
	if state.id != _local_id or state.tagged or not _sim.rules.stamina_enabled or not state.has_stamina:
		return false
	return state.overclocked or state.stamina < _sim.rules.stamina_max - 0.01


func stamina_fill(state: PlayerState) -> Array:
	if state.overclocked and state.exhaust_left > 0.0:
		var lit: bool = Settings.reduced_motion or int(state.exhaust_left / BLINK_PERIOD) % 2 == 0
		return [1.0 if lit else 0.0, EXHAUSTED_COLOR]
	var share: float = clampf(state.stamina / maxf(_sim.rules.stamina_max, 0.001), 0.0, 1.0)
	if state.overclocked:
		return [share, STAMINA_LOW_COLOR]
	return [share, STAMINA_LOW_COLOR if share < STAMINA_LOW else STAMINA_COLOR]


func popup_count() -> int:
	return _popups.size()


func say(id: int, text: String) -> void:
	_says = _says.filter(func(s: Dictionary) -> bool: return (s["id"] as int) != id)
	_says.append({"id": id, "text": text, "age": 0.0})
	queue_redraw()


func says_count() -> int:
	return _says.size()


func tick(delta: float) -> void:
	for said: Dictionary in _says:
		said["age"] = (said["age"] as float) + delta
	_says = _says.filter(func(s: Dictionary) -> bool: return (s["age"] as float) < QuickChat.SAY_TIME)
	for popup: Dictionary in _popups:
		popup["age"] = (popup["age"] as float) + delta
	_popups = _popups.filter(func(p: Dictionary) -> bool: return (p["age"] as float) < POPUP_TIME)
	queue_redraw()


func bar_visible(state: PlayerState) -> bool:
	return not state.tagged and not hidden_ids.has(state.id)


## True for a kid the local player plays against: the other team, or the taya in a street game (owner, 2026-11-13: an enemy's health is
## never shown during play; its badge and status word still are).
func is_enemy(state: PlayerState) -> bool:
	if state.id == _local_id:
		return false
	return state.id == taya_id or state.team != _local_team


## Whether the health bar itself is drawn over `state` (never over an enemy).
func health_shown(state: PlayerState) -> bool:
	return bar_visible(state) and not is_enemy(state)


func bar_color(state: PlayerState) -> Color:
	if state.id == _local_id:
		return TeamColors.bar_self()
	if state.id == taya_id:
		return TeamColors.bar_enemy()
	return TeamColors.bar_ally() if state.team == _local_team else TeamColors.bar_enemy()


func label_for(state: PlayerState) -> String:
	var status: String = _status_text(state)
	if not status.is_empty():
		return status
	if name_reach > 0.0 and _camera != null and _camera.global_position.distance_to(Vector3(state.position.x, 0.0, state.position.y)) > name_reach:
		return ""
	return name_of(state)


func name_of(state: PlayerState) -> String:
	var given: String = names.get(state.id, "") as String
	if not given.is_empty():
		return given
	for character: CharacterDef in _sim.rules.characters:
		if character.id == state.character_id:
			return character.display_name
	return ""


func player_number(state: PlayerState) -> int:
	var row: int = 0
	var team_zero: int = 0
	var team_one: int = 0
	var before: bool = true
	for id: int in _sim.players:
		var other: PlayerState = _sim.players[id]
		if other.id == state.id:
			before = false
		elif before and other.team == state.team:
			row += 1
		if other.team == 0:
			team_zero += 1
		else:
			team_one += 1
	return RoomPlayerCards.slot_number(state.team, row, maxi(team_zero, team_one))


func badge_color(state: PlayerState) -> Color:
	if state.id == taya_id:
		return TeamColors.world_enemy()
	return TeamColors.world_own() if state.team == _local_team else TeamColors.world_enemy()


func _on_healed(id: int, amount: int) -> void:
	_popups.append({"id": id, "amount": amount, "age": 0.0, "heal": true, "jitter": 0.0})


func _head(state: PlayerState) -> Variant:
	var at: Vector2 = blend.kid(state) if blend != null else state.position
	var height_mul: float = 0.78 if state.crouching else 1.0
	var world: Vector3 = Vector3(at.x, HEAD_HEIGHT * view_scale * height_mul + state.air_y, at.y)
	if _camera == null or _camera.is_position_behind(world):
		return null
	return _camera.screen_position(world) if _camera is ArenaCamera else _camera.unproject_position(world)


func _draw() -> void:
	if _sim == null:
		return
	if _sim.phase in [MatchSim.Phase.POINT_FREEZE, MatchSim.Phase.MATCH_OVER]:
		return
	for id: int in _sim.players:
		_draw_progress(_sim.players[id])
	_placed.clear()
	if _sim.players.has(_local_id):
		_draw_hero(_sim.players[_local_id])
	for id: int in _sim.players:
		if id != _local_id:
			_draw_hero(_sim.players[id])
	for said: Dictionary in _says:
		_draw_say(said)
	for popup: Dictionary in _popups:
		_draw_popup(popup)


func _draw_say(said: Dictionary) -> void:
	var state: PlayerState = _sim.players.get(said["id"]) as PlayerState
	if state == null or hidden_ids.has(state.id):
		return
	var at: Variant = _head(state)
	if at == null:
		return
	var texture: Texture2D = load(QuickChat.STICKER_PATH % QuickChat.slug(said["text"] as String)) as Texture2D
	if texture == null:
		return
	var age: float = said["age"] as float
	var pop: float = 1.0 if Settings.reduced_motion else say_pop_scale(age)
	var fade: float = clampf((QuickChat.SAY_TIME - age) / 0.3, 0.0, 1.0)
	var rise: float = BAR_SIZE.y * bar_scale + LABEL_LINE + SAY_LIFT + SAY_ART / 2.0
	var half: float = SAY_ART / 2.0
	var centre: Vector2 = Vector2(clampf((at as Vector2).x, half, maxf(size.x - half, half)), maxf((at as Vector2).y - rise, top_limit + half))
	var rect: Rect2 = Rect2(centre - Vector2.ONE * half * pop, Vector2.ONE * SAY_ART * pop)
	draw_texture_rect(texture, rect, false, Color(1.0, 1.0, 1.0, SAY_ALPHA * fade))


static func say_pop_scale(age: float) -> float:
	var t: float = clampf(age / SAY_POP, 0.0, 1.0)
	return lerpf(SAY_FROM, 1.0, 1.0 - (1.0 - t) * (1.0 - t))


func spinner_visible(state: PlayerState) -> bool:
	return (
		not state.tagged and _sim.capture_team == state.team
		and _sim.capture_kind == MatchSim.CaptureKind.BASE
		and _sim.layout.in_base(-_sim.side_for_team(state.team), state.position)
	)


func rescue_visible(state: PlayerState) -> bool:
	return not state.tagged and state.team >= 0 and state.team < _sim.rescue_rescuer.size() and _sim.rescue_rescuer[state.team] == state.id


func _bar_rect(state: PlayerState) -> Variant:
	var head: Variant = _head(state)
	if head == null or not bar_visible(state):
		return null
	var bar: Vector2 = BAR_SIZE * bar_scale
	var left: float = BADGE_SIZE - BADGE_OVERLAP + EDGE_PAD
	return Rect2(Vector2(clampf((head as Vector2).x - bar.x / 2.0, left, maxf(size.x - bar.x - EDGE_PAD, left)), (head as Vector2).y), bar)


func _clear_of_others(rect: Rect2) -> Rect2:
	var group: Rect2 = Rect2(
		Vector2(rect.position.x + BADGE_OVERLAP - BADGE_SIZE, rect.position.y - BAR_OUTLINE - LABEL_LINE),
		Vector2(rect.size.x + BADGE_SIZE - BADGE_OVERLAP, rect.size.y + BAR_OUTLINE * 2.0 + LABEL_LINE))
	var moved: bool = true
	while moved:
		moved = false
		for taken: Rect2 in _placed:
			if taken.intersects(group):
				group.position.y = taken.position.y - group.size.y
				moved = true
	group.position.y = maxf(group.position.y, top_limit)
	_placed.append(group)
	return Rect2(Vector2(rect.position.x, group.position.y + BAR_OUTLINE + LABEL_LINE), rect.size)


func _draw_hero(state: PlayerState) -> void:
	var at: Variant = _bar_rect(state)
	if at == null:
		return
	var rect: Rect2 = _clear_of_others(at as Rect2)
	var max_hp: float = float(_sim.rules.player_max_hp)
	var fill: float = clampf(float(state.hp) / max_hp, 0.0, 1.0)
	var color: Color = bar_color(state)
	if not is_enemy(state):
		_draw_bar(rect, fill, color)
	_draw_label(label_for(state), rect)
	_draw_badge(state, rect)
	if stamina_on_bar and stamina_visible(state):
		var stamina: Vector2 = STAMINA_SIZE * bar_scale
		var under: Rect2 = Rect2(Vector2(rect.position.x, stamina_top(rect)), stamina)
		var look: Array = stamina_fill(state)
		_draw_thin(under, look[0] as float, look[1] as Color)


func _draw_progress(state: PlayerState) -> void:
	var at: Variant = _bar_rect(state)
	if at == null or not (spinner_visible(state) or rescue_visible(state)):
		return
	var rect: Rect2 = at as Rect2
	var lift: float = LABEL_LINE
	var centre: Vector2 = Vector2(rect.get_center().x, rect.position.y - SPINNER_LIFT - lift)
	if spinner_visible(state):
		_draw_spinner(centre, _sim.capture_elapsed / maxf(_sim.capture_needed, 0.001), SPINNER_COLOR)
	else:
		_draw_spinner(centre, _sim.rescue_elapsed[state.team] / maxf(_sim.rules.rescue_hold_time, 0.001), RESCUE_COLOR)


func _draw_label(text: String, rect: Rect2) -> void:
	if text.is_empty():
		return
	var font: Font = KalyeahTheme.ACTION_FONT
	var room: float = label_room(rect.size.x)
	var font_size: int = label_font_size(text, room)
	var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size).x
	var centre: Vector2 = keep_on_screen(Vector2(rect.position.x + LABEL_INSET + width / 2.0, label_baseline(rect, font_size)), font, text, font_size)
	_text(font, text, centre, font_size, Color(1.0, 1.0, 1.0, 0.94), label_outline(font_size), SOFT_LABEL_OUTLINE)


static func label_room(bar_width: float) -> float:
	return maxf(bar_width - LABEL_INSET, 0.0)


static func label_font_size(text: String, room: float) -> int:
	var font: Font = KalyeahTheme.ACTION_FONT
	var font_size: int = LABEL_FONT
	while font_size > LABEL_FONT_MIN and font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size).x > room:
		font_size -= 1
	return font_size


static func label_outline(font_size: int) -> int:
	return maxi(roundi(float(font_size) * LABEL_OUTLINE_SHARE), 1)


func _draw_badge(state: PlayerState, rect: Rect2) -> void:
	var badge: Rect2 = Rect2(Vector2(rect.position.x + BADGE_OVERLAP - BADGE_SIZE, rect.get_center().y - BADGE_SIZE / 2.0), Vector2.ONE * BADGE_SIZE)
	_badge_box.bg_color = badge_color(state)
	draw_style_box(_badge_box, badge)
	_text(KalyeahTheme.ACTION_FONT, str(player_number(state)), Vector2(badge.get_center().x, badge.get_center().y + float(BADGE_FONT) * 0.36), BADGE_FONT, Color.WHITE, BADGE_TEXT_OUTLINE, SOFT_LABEL_OUTLINE)


static func stamina_top(rect: Rect2) -> float:
	return rect.end.y + BAR_OUTLINE


static func label_baseline(rect: Rect2, font_size: int = LABEL_FONT) -> float:
	return rect.position.y - BAR_OUTLINE / 2.0 + float(font_size) * 0.36


func _draw_bar(rect: Rect2, fill: float, color: Color) -> void:
	_bar_back.set_border_width_all(int(BAR_OUTLINE))
	_bar_back.set_corner_radius_all(UiSkin.bar_radius(rect.size.y + BAR_OUTLINE * 2.0))
	draw_style_box(_bar_back, rect.grow(BAR_OUTLINE))
	if fill > 0.0:
		var width: float = maxf(rect.size.x * fill, rect.size.y)
		_bar_fill.bg_color = color
		_bar_fill.set_corner_radius_all(UiSkin.bar_radius(rect.size.y))
		draw_style_box(_bar_fill, Rect2(rect.position, Vector2(width, rect.size.y)))
		_bar_gloss.set_corner_radius_all(UiSkin.bar_radius(rect.size.y / 3.0))
		draw_style_box(_bar_gloss, Rect2(rect.position + Vector2(rect.size.y / 3.0, 1.0), Vector2(maxf(width - rect.size.y / 1.5, 1.0), rect.size.y / 3.0)))


func _draw_thin(rect: Rect2, fill: float, color: Color) -> void:
	_bar_back.set_border_width_all(int(BAR_OUTLINE))
	_bar_back.set_corner_radius_all(UiSkin.bar_radius(rect.size.y + BAR_OUTLINE * 2.0))
	draw_style_box(_bar_back, rect.grow(BAR_OUTLINE))
	if fill <= 0.0:
		return
	_bar_fill.bg_color = color
	_bar_fill.set_corner_radius_all(UiSkin.bar_radius(rect.size.y))
	draw_style_box(_bar_fill, Rect2(rect.position, Vector2(maxf(rect.size.x * fill, rect.size.y), rect.size.y)))


func keep_on_screen(centre: Vector2, font: Font, text: String, font_size: int) -> Vector2:
	var half: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size).x / 2.0 + 4.0
	var x: float = clampf(centre.x, half, maxf(size.x - half, half))
	return Vector2(x, maxf(centre.y, top_limit + float(font_size) + 4.0))


func _draw_spinner(center: Vector2, progress: float, color: Color) -> void:
	draw_circle(center, SPINNER_RADIUS + 3.0, SPINNER_BACK)
	draw_arc(center, SPINNER_RADIUS, 0.0, TAU, 28, Color(1.0, 1.0, 1.0, 0.22), 3.0, true)
	var spin: float = float(Time.get_ticks_msec()) / 1000.0 * SPINNER_SPEED
	draw_arc(center, SPINNER_RADIUS, spin, spin + TAU * 0.28, 12, Color(1.0, 1.0, 1.0, 0.85), 3.0, true)
	draw_arc(center, SPINNER_RADIUS - 5.0, -PI / 2.0, -PI / 2.0 + TAU * clampf(progress, 0.0, 1.0), 24, color, 3.2, true)


func _draw_popup(popup: Dictionary) -> void:
	var state: PlayerState = _sim.players.get(popup["id"] as int) as PlayerState
	if state == null:
		return
	var at: Variant = _head(state)
	if at == null:
		return
	var age: float = (popup["age"] as float) / POPUP_TIME
	var pos: Vector2 = (at as Vector2) + Vector2(popup["jitter"] as float, -28.0 - POPUP_RISE * age)
	var healed: bool = popup["heal"] as bool
	var color: Color = HEAL_COLOR if healed else (DAMAGE_COLOR if state.team == _local_team else Color(1.0, 0.85, 0.3))
	color.a = 1.0 - age * age
	var pop_sz: int = int(POPUP_FONT * (1.2 - 0.2 * age) * (1.15 if healed else 1.0))
	_text(KalyeahTheme.ACTION_FONT, ("+%d" if healed else "-%d") % (popup["amount"] as int), pos, pop_sz, color, 3, SOFT_LABEL_OUTLINE)


func _text(font: Font, text: String, center: Vector2, font_size: int, color: Color, outline: int, outline_color: Color = SOFT_LABEL_OUTLINE) -> void:
	if text.is_empty():
		return
	var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size).x
	var origin: Vector2 = center - Vector2(width / 2.0, 0.0)
	var dark: Color = outline_color
	dark.a = outline_color.a * color.a
	draw_string_outline(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, outline, dark)
	draw_string(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, color)
