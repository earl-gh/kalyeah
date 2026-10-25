class_name OverheadHud
extends Control
## MOBA-style overhead display drawn in screen space over each hero, like League
## of Legends: a solid health bar (green you, blue ally, red enemy), a status
## in plain words above it ("Stunned", "Slowed", "Exhausted"). Under your own bar a thin stamina bar shows while it is not full (your eyes
## are on your kid while you tap to walk, and stamina only matters while walking); at 0 it reads EXHAUSTED and runs out over the 2 s. No names and no damage numbers (heals still pop a green "+N"). While a
## team holds the enemy base, a spinner fills over its kids' heads. Reads the sim
## and projects through the match camera.

## The bar before `bar_scale` (visual pass: 92 x 13 read as a hairline at phone size; now a chunky rounded bar).
const BAR_SIZE: Vector2 = Vector2(96.0, 18.0)
## The dark outline round the bar: thin (screen px, kept at any bar_scale).
const BAR_OUTLINE: float = 2.0
## The health number at the bar's top left corner (its left edge lines up with the bar's, its middle level with the bar's top outline),
## and how far it reaches above the bar.
const COUNT_FONT: int = UiMetrics.WORLD_SMALL
const COUNT_LINE: float = 10.0
## The bar sits just above the top of the head (the kid's model is KID_HEIGHT tall).
const HEAD_HEIGHT: float = KidModel.KID_HEIGHT + 0.2
const POPUP_TIME: float = 0.9
const POPUP_RISE: float = 56.0
const STATUS_FONT: int = UiMetrics.CAPTION
## Your stamina under your health bar (before `bar_scale`): a thin bar, a gap above it; it warns below `STAMINA_LOW` of full. When it runs
## out you are overclocked (owner): the bar blinks blank and red for `exhaust_seconds`, then refills in amber while you only walk, until
## it is full. Other kids' stamina is never drawn (docs/GDD.md D33), only their *Exhausted*.
const STAMINA_SIZE: Vector2 = Vector2(96.0, 8.0)
const STAMINA_LOW: float = 0.25
## Stamina is blue (health is green, yours; blue for an ally's bar too, but the stamina bar is only ever under your own).
const STAMINA_COLOR: Color = Color(0.22, 0.52, 1.0)
const STAMINA_LOW_COLOR: Color = ConsolePanel.HEALTH_AMBER
const EXHAUSTED_COLOR: Color = ConsolePanel.HEALTH_RED
## How long the overclock's red / blank blink holds each state (seconds); steady red with Reduce motion.
const BLINK_PERIOD: float = 0.25
## The word over your own kid while it is exhausted (owner: "EXHAUSTED shown on your kid"); other kids show the plain status word.
const EXHAUSTED_SELF: String = "EXHAUSTED"
const EXHAUSTED_OTHER: String = "Exhausted"
const POPUP_FONT: int = UiMetrics.BODY
## A quick chat bubble: the words' size, the padding round them and how far over the health number it floats.
const SAY_FONT: int = UiMetrics.DETAIL
const SAY_PAD: Vector2 = Vector2(16.0, 8.0)
const SAY_LIFT: float = 12.0
const BACK: Color = Color(0.08, 0.06, 0.1, 0.85)
## The empty part of a health or stamina bar: a flat, opaque dark gray (just the colour: no sheen, no texture).
const EMPTY_BAR: Color = Color(0.18, 0.19, 0.21)
const SELF_COLOR: Color = Color(0.35, 0.95, 0.35)
const ALLY_COLOR: Color = Color(0.35, 0.65, 1.0)
const ENEMY_COLOR: Color = Color(1.0, 0.3, 0.3)
const STATUS_COLOR: Color = Color(1.0, 0.86, 0.3)
const DAMAGE_COLOR: Color = Color(1.0, 0.95, 0.85)
const HEAL_COLOR: Color = Color(0.55, 1.0, 0.45)
## The spinner above a hero holding the enemy base.
const SPINNER_RADIUS: float = 15.0
const SPINNER_LIFT: float = 36.0
const SPINNER_COLOR: Color = Color(1.0, 0.86, 0.3)
## The rescuer's ring (green: it frees a teammate), the capturer's stays gold.
const RESCUE_COLOR: Color = Color(0.45, 0.9, 0.5)
const SPINNER_BACK: Color = Color(0.08, 0.06, 0.1, 0.7)
const SPINNER_SPEED: float = 5.0

## How much bigger than real the kids are drawn (CameraRules.kid_view_scale): the bar rides on their heads.
var view_scale: float = 1.0
## How much smaller the health bar is drawn (CameraRules.bar_scale): the kids are small on a screen that shows the whole street.
var bar_scale: float = 1.0
## Words and counters never ride higher than this (the header's bottom edge), nor past the screen's sides: a kid at the street's far end
## keeps its status readable instead of hiding under the header.
var top_limit: float = 0.0

var _sim: MatchSim
var _camera: Camera3D
var _local_id: int = -1
var _local_team: int = 0
## The bar's back, fill and gloss, made once and recoloured as each kid is drawn (no allocation per frame).
var _bar_back: StyleBoxFlat
var _bar_fill: StyleBoxFlat
var _bar_gloss: StyleBoxFlat
## {id, amount, age, heal, jitter}
var _popups: Array[Dictionary] = []
## Quick chat bubbles over kids: {id, text, age}.
var _says: Array[Dictionary] = []


func setup(sim: MatchSim, camera: Camera3D, local_id: int, local_team: int) -> void:
	_sim = sim
	_camera = camera
	_local_id = local_id
	_local_team = local_team
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	sim.player_healed.connect(_on_healed)
	_bar_back = StyleBoxFlat.new()
	_bar_back.bg_color = EMPTY_BAR
	_bar_back.border_color = Icons.OUTLINE
	_bar_back.anti_aliasing = true
	_bar_fill = StyleBoxFlat.new()
	_bar_fill.anti_aliasing = true
	_bar_gloss = StyleBoxFlat.new()
	_bar_gloss.bg_color = Color(1.0, 1.0, 1.0, 0.35)
	_bar_gloss.anti_aliasing = true


## The status words shown above a hero's bar: "Stunned", "Slowed", ...
## Empty when nothing is wrong. Heroes have no name text.
func text_for(id: int) -> String:
	return _status_text(_sim.players[id])


## A tagged kid stands in the box, which already says it: no "Tagged" over each one (two of them ran into one word).
func _status_text(state: PlayerState) -> String:
	var words: PackedStringArray = PackedStringArray()
	if state.overclocked:
		words.append(UiText.t(EXHAUSTED_SELF if state.id == _local_id else EXHAUSTED_OTHER))
	for word: String in state.effects.display_names():
		words.append(UiText.t(word))
	return " · ".join(words)


## True while your own stamina bar is drawn: stamina is on, not full, or you are exhausted (never for another kid).
func stamina_visible(state: PlayerState) -> bool:
	if state.id != _local_id or state.tagged or not _sim.rules.stamina_enabled or not state.has_stamina:
		return false
	return state.overclocked or state.stamina < _sim.rules.stamina_max - 0.01


## The share and colour of your stamina bar: the stamina left (amber when low). Overclocked: blinking blank and red while the blink
## runs (steady red with Reduce motion), then the refill in amber.
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


## A quick chat bubble over kid `id` saying `text` for QuickChat.SAY_TIME (a new one replaces that kid's old one).
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


## False for a tagged kid (owner: no health for tagged players). Free kids always show their bar, wherever they stand.
func bar_visible(state: PlayerState) -> bool:
	return not state.tagged


func bar_color(state: PlayerState) -> Color:
	if state.id == _local_id:
		return TeamColors.bar_self()
	return TeamColors.bar_ally() if state.team == _local_team else TeamColors.bar_enemy()


func _on_healed(id: int, amount: int) -> void:
	_popups.append({"id": id, "amount": amount, "age": 0.0, "heal": true, "jitter": 0.0})


func _head(state: PlayerState) -> Variant:
	var world: Vector3 = Vector3(state.position.x, HEAD_HEIGHT * view_scale, state.position.y)
	if _camera == null or _camera.is_position_behind(world):
		return null
	return _camera.screen_position(world) if _camera is ArenaCamera else _camera.unproject_position(world)


func _draw() -> void:
	if _sim == null:
		return
	for id: int in _sim.players:
		var state: PlayerState = _sim.players[id]
		var at: Variant = _head(state)
		if at != null and bar_visible(state):
			_draw_hero(state, at as Vector2)
	for said: Dictionary in _says:
		_draw_say(said)
	for popup: Dictionary in _popups:
		_draw_popup(popup)


## The quick chat bubble over a kid: a white rounded bubble with a dark outline and a little tail, the words inside.
func _draw_say(said: Dictionary) -> void:
	var state: PlayerState = _sim.players.get(said["id"]) as PlayerState
	if state == null:
		return
	var at: Variant = _head(state)
	if at == null:
		return
	var text: String = UiText.t(said["text"] as String)
	var font: Font = KalyeahTheme.ACTION_FONT
	var words: Vector2 = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, SAY_FONT)
	var bubble: Vector2 = words + SAY_PAD * 2.0
	var rise: float = BAR_SIZE.y * bar_scale + COUNT_LINE + SAY_LIFT
	var centre: Vector2 = keep_on_screen(at as Vector2 + Vector2(0.0, -rise - bubble.y / 2.0), font, text, SAY_FONT)
	var rect: Rect2 = Rect2(centre - bubble / 2.0, bubble)
	var fade: float = clampf((QuickChat.SAY_TIME - (said["age"] as float)) / 0.3, 0.0, 1.0)
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color(1.0, 1.0, 1.0, fade)
	box.border_color = Color(0.0, 0.0, 0.0, fade)
	box.set_border_width_all(3)
	box.set_corner_radius_all(UiSkin.RADIUS_M)
	box.anti_aliasing = true
	draw_style_box(box, rect)
	var tail_x: float = clampf((at as Vector2).x, rect.position.x + 14.0, rect.end.x - 14.0)
	draw_colored_polygon(PackedVector2Array([Vector2(tail_x - 8.0, rect.end.y - 1.0), Vector2(tail_x + 8.0, rect.end.y - 1.0), Vector2(tail_x - 2.0, rect.end.y + 10.0)]), Color(0.0, 0.0, 0.0, fade))
	draw_colored_polygon(PackedVector2Array([Vector2(tail_x - 5.0, rect.end.y - 2.0), Vector2(tail_x + 5.0, rect.end.y - 2.0), Vector2(tail_x - 2.0, rect.end.y + 5.0)]), Color(1.0, 1.0, 1.0, fade))
	draw_string(font, Vector2(rect.position.x + SAY_PAD.x, rect.end.y - SAY_PAD.y - 4.0), text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, SAY_FONT, Color(0.0, 0.0, 0.0, fade))


## True while `state`'s team holds the enemy base and this kid stands in it.
func spinner_visible(state: PlayerState) -> bool:
	return (
		not state.tagged and _sim.capture_team == state.team
		and _sim.capture_kind == MatchSim.CaptureKind.BASE
		and _sim.layout.in_base(-_sim.side_for_team(state.team), state.position)
	)


## True while this kid is the teammate rescuing a tagged one (it stands in the rescue area and the count is running).
func rescue_visible(state: PlayerState) -> bool:
	return not state.tagged and state.team >= 0 and state.team < _sim.rescue_rescuer.size() and _sim.rescue_rescuer[state.team] == state.id


func _draw_hero(state: PlayerState, head: Vector2) -> void:
	var bar: Vector2 = BAR_SIZE * bar_scale
	var rect: Rect2 = Rect2(head - Vector2(bar.x / 2.0, 0.0), bar)
	var max_hp: float = float(_sim.rules.player_max_hp)
	var fill: float = clampf(float(state.hp) / max_hp, 0.0, 1.0)
	var color: Color = bar_color(state)
	_draw_bar(rect, fill, color)
	_draw_count(state, rect)
	_draw_team_mark(state, rect, color)
	if stamina_visible(state):
		var stamina: Vector2 = STAMINA_SIZE * bar_scale
		var under: Rect2 = Rect2(Vector2(head.x - stamina.x / 2.0, stamina_top(rect)), stamina)
		var look: Array = stamina_fill(state)
		_draw_thin(under, look[0] as float, look[1] as Color)
	var status: String = _status_text(state)
	var lift: float = COUNT_LINE
	if not status.is_empty():
		_text(KalyeahTheme.BODY_FONT, status, keep_on_screen(head + Vector2(0.0, -8.0 - COUNT_LINE), KalyeahTheme.BODY_FONT, status, STATUS_FONT), STATUS_FONT, STATUS_COLOR, 6)
		lift += 22.0
	if spinner_visible(state):
		_draw_spinner(head + Vector2(0.0, -SPINNER_LIFT - lift), _sim.capture_elapsed / maxf(_sim.capture_needed, 0.001), SPINNER_COLOR)
	elif rescue_visible(state):
		_draw_spinner(head + Vector2(0.0, -SPINNER_LIFT - lift), _sim.rescue_elapsed[state.team] / maxf(_sim.rules.rescue_hold_time, 0.001), RESCUE_COLOR)


## The health number at the bar's top left corner: left-aligned with the bar, its middle level with the bar's top outline.
func _draw_count(state: PlayerState, rect: Rect2) -> void:
	var text: String = str(maxi(state.hp, 0))
	var width: float = KalyeahTheme.ACTION_FONT.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, COUNT_FONT).x
	_text(KalyeahTheme.ACTION_FONT, text, Vector2(rect.position.x + width / 2.0, count_baseline(rect)), COUNT_FONT, Color.WHITE, 5)


## Where the stamina bar starts under a health bar at `rect`: no gap, the two bars share the line between them (the stamina bar's top
## outline is the health bar's bottom outline).
static func stamina_top(rect: Rect2) -> float:
	return rect.end.y + BAR_OUTLINE


## The baseline of the health number over a bar at `rect`: its middle level with the middle of the bar's top outline.
static func count_baseline(rect: Rect2) -> float:
	return rect.position.y - BAR_OUTLINE / 2.0 + float(COUNT_FONT) * 0.36


## A rounded bar with a dark outline: the fill in `color`, and a gloss on its upper third.
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


## A thin bar with a dark outline, filled to `fill` in `color` (your stamina).
func _draw_thin(rect: Rect2, fill: float, color: Color) -> void:
	_bar_back.set_border_width_all(int(BAR_OUTLINE))
	_bar_back.set_corner_radius_all(UiSkin.bar_radius(rect.size.y + BAR_OUTLINE * 2.0))
	draw_style_box(_bar_back, rect.grow(BAR_OUTLINE))
	if fill <= 0.0:
		return
	_bar_fill.bg_color = color
	_bar_fill.set_corner_radius_all(UiSkin.bar_radius(rect.size.y))
	draw_style_box(_bar_fill, Rect2(rect.position, Vector2(maxf(rect.size.x * fill, rect.size.y), rect.size.y)))


## Where `text` is drawn: its centre pulled inside the screen's sides and its baseline kept under `top_limit`.
func keep_on_screen(centre: Vector2, font: Font, text: String, font_size: int) -> Vector2:
	var half: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size).x / 2.0 + 4.0
	var x: float = clampf(centre.x, half, maxf(size.x - half, half))
	return Vector2(x, maxf(centre.y, top_limit + float(font_size) + 4.0))


## A small shape at the left end of the bar that says whose kid it is without colour: a circle for an ally, a diamond for an enemy
## (you have none: the bar itself is brighter and the kid wears your ring).
func _draw_team_mark(state: PlayerState, rect: Rect2, color: Color) -> void:
	if state.id == _local_id:
		return
	var radius: float = maxf(rect.size.y * 0.62, 4.5)
	var centre: Vector2 = Vector2(rect.position.x - radius - 3.0, rect.position.y + rect.size.y / 2.0)
	if state.team == _local_team:
		draw_circle(centre, radius + 1.5, BACK)
		draw_circle(centre, radius, color)
		return
	var outer: PackedVector2Array = _diamond(centre, radius + 2.0)
	draw_colored_polygon(outer, BACK)
	draw_colored_polygon(_diamond(centre, radius + 0.5), color)


static func _diamond(centre: Vector2, radius: float) -> PackedVector2Array:
	return PackedVector2Array([centre + Vector2(0.0, -radius), centre + Vector2(radius, 0.0), centre + Vector2(0.0, radius), centre + Vector2(-radius, 0.0)])


## A spinning ring over a hero holding the enemy base: the ring fills as the point
## gets closer (it scores when it is full).
func _draw_spinner(center: Vector2, progress: float, color: Color) -> void:
	draw_circle(center, SPINNER_RADIUS + 4.0, SPINNER_BACK)
	draw_arc(center, SPINNER_RADIUS, 0.0, TAU, 28, Color(1.0, 1.0, 1.0, 0.25), 4.0)
	var spin: float = float(Time.get_ticks_msec()) / 1000.0 * SPINNER_SPEED
	draw_arc(center, SPINNER_RADIUS, spin, spin + TAU * 0.28, 12, Color(1.0, 1.0, 1.0, 0.9), 4.0)
	draw_arc(center, SPINNER_RADIUS - 6.0, -PI / 2.0, -PI / 2.0 + TAU * clampf(progress, 0.0, 1.0), 24, color, 4.0)


func _draw_popup(popup: Dictionary) -> void:
	var state: PlayerState = _sim.players.get(popup["id"] as int) as PlayerState
	if state == null:
		return
	var at: Variant = _head(state)
	if at == null:
		return
	var age: float = (popup["age"] as float) / POPUP_TIME
	var pos: Vector2 = (at as Vector2) + Vector2(popup["jitter"] as float, -30.0 - POPUP_RISE * age)
	var healed: bool = popup["heal"] as bool
	var color: Color = HEAL_COLOR if healed else (DAMAGE_COLOR if state.team == _local_team else Color(1.0, 0.85, 0.3))
	color.a = 1.0 - age * age
	var size: int = int(POPUP_FONT * (1.25 - 0.25 * age) * (1.2 if healed else 1.0))
	_text(KalyeahTheme.ACTION_FONT, ("+%d" if healed else "-%d") % (popup["amount"] as int), pos, size, color, 6)


func _text(font: Font, text: String, center: Vector2, font_size: int, color: Color, outline: int) -> void:
	if text.is_empty():
		return
	var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size).x
	var origin: Vector2 = center - Vector2(width / 2.0, 0.0)
	var dark: Color = Icons.OUTLINE
	dark.a = color.a
	draw_string_outline(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, outline, dark)
	draw_string(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, color)
