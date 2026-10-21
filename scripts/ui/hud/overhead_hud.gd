class_name OverheadHud
extends Control
## MOBA-style overhead display drawn in screen space over each hero, like League
## of Legends: a solid health bar (green you, blue ally, red enemy), a status
## in *italic* above it ("Stunned", "Tagged"). No names and no damage numbers (heals still pop a green "+N"). While a
## team holds the enemy base, a spinner fills over its kids' heads. Reads the sim
## and projects through the match camera.

const BAR_SIZE: Vector2 = Vector2(92.0, 13.0)
## The bar sits just above the top of the head (the kid's model is KID_HEIGHT tall).
const HEAD_HEIGHT: float = KidModel.KID_HEIGHT + 0.2
const POPUP_TIME: float = 0.9
const POPUP_RISE: float = 56.0
const STATUS_FONT: int = 22
const POPUP_FONT: int = 30
## Italic = the regular font slanted (the bundled font has no italic face).
const ITALIC_SLANT: float = -0.22
const BACK: Color = Color(0.08, 0.06, 0.1, 0.85)
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
const SPINNER_BACK: Color = Color(0.08, 0.06, 0.1, 0.7)
const SPINNER_SPEED: float = 5.0

## How much bigger than real the kids are drawn (CameraRules.kid_view_scale): the bar rides on their heads.
var view_scale: float = 1.0
## How much smaller the health bar is drawn (CameraRules.bar_scale): the kids are small on a screen that shows the whole street.
var bar_scale: float = 1.0

var _sim: MatchSim
var _camera: Camera3D
var _local_id: int = -1
var _local_team: int = 0
var _italic: FontVariation
## {id, amount, age, heal, jitter}
var _popups: Array[Dictionary] = []


func setup(sim: MatchSim, camera: Camera3D, local_id: int, local_team: int) -> void:
	_sim = sim
	_camera = camera
	_local_id = local_id
	_local_team = local_team
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_italic = FontVariation.new()
	_italic.base_font = KalyeahTheme.BODY_FONT
	_italic.variation_transform = Transform2D(Vector2(1.0, 0.0), Vector2(ITALIC_SLANT, 1.0), Vector2.ZERO)
	sim.player_healed.connect(_on_healed)


## The status words shown (in italic) above a hero's bar: "Stunned", "Tagged", ...
## Empty when nothing is wrong. Heroes have no name text.
func text_for(id: int) -> String:
	return _status_text(_sim.players[id])


func _status_text(state: PlayerState) -> String:
	var words: PackedStringArray = PackedStringArray()
	if state.tagged:
		words.append(UiText.t("Tagged"))
	for word: String in state.effects.display_names():
		words.append(UiText.t(word))
	return " · ".join(words)


func popup_count() -> int:
	return _popups.size()


func tick(delta: float) -> void:
	for popup: Dictionary in _popups:
		popup["age"] = (popup["age"] as float) + delta
	_popups = _popups.filter(func(p: Dictionary) -> bool: return (p["age"] as float) < POPUP_TIME)
	queue_redraw()


## False for an enemy standing in its own team's tagged box: it blends in with the tagged kids
## (owner), so no bar, status or spinner is drawn over it. Your own team always sees its kids.
func bar_visible(state: PlayerState) -> bool:
	return not _sim.hides_in_box(state, _local_team)


func bar_color(state: PlayerState) -> Color:
	if state.id == _local_id:
		return SELF_COLOR
	return ALLY_COLOR if state.team == _local_team else ENEMY_COLOR


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
	for popup: Dictionary in _popups:
		_draw_popup(popup)


## True while `state`'s team holds the enemy base and this kid stands in it.
func spinner_visible(state: PlayerState) -> bool:
	return (
		not state.tagged and _sim.capture_team == state.team
		and _sim.capture_kind == MatchSim.CaptureKind.BASE
		and _sim.layout.in_base(-_sim.side_for_team(state.team), state.position)
	)


func _draw_hero(state: PlayerState, head: Vector2) -> void:
	var bar: Vector2 = BAR_SIZE * bar_scale
	var rect: Rect2 = Rect2(head - Vector2(bar.x / 2.0, 0.0), bar)
	draw_rect(rect.grow(2.0), BACK)
	var max_hp: float = float(_sim.rules.player_max_hp)
	var fill: float = clampf(float(state.hp) / max_hp, 0.0, 1.0)
	var color: Color = bar_color(state)
	draw_rect(Rect2(rect.position, Vector2(rect.size.x * fill, rect.size.y)), color)
	draw_rect(Rect2(rect.position, Vector2(rect.size.x * fill, rect.size.y * 0.35)), color.lightened(0.35))
	var status: String = _status_text(state)
	var lift: float = 0.0
	if not status.is_empty():
		_text(_italic, status, head + Vector2(0.0, -8.0), STATUS_FONT, STATUS_COLOR, 6)
		lift = 22.0
	if spinner_visible(state):
		_draw_spinner(head + Vector2(0.0, -SPINNER_LIFT - lift), _sim.capture_elapsed / maxf(_sim.capture_needed, 0.001))


## A spinning ring over a hero holding the enemy base: the ring fills as the point
## gets closer (it scores when it is full).
func _draw_spinner(center: Vector2, progress: float) -> void:
	draw_circle(center, SPINNER_RADIUS + 4.0, SPINNER_BACK)
	draw_arc(center, SPINNER_RADIUS, 0.0, TAU, 28, Color(1.0, 1.0, 1.0, 0.25), 4.0)
	var spin: float = float(Time.get_ticks_msec()) / 1000.0 * SPINNER_SPEED
	draw_arc(center, SPINNER_RADIUS, spin, spin + TAU * 0.28, 12, Color(1.0, 1.0, 1.0, 0.9), 4.0)
	draw_arc(center, SPINNER_RADIUS - 6.0, -PI / 2.0, -PI / 2.0 + TAU * clampf(progress, 0.0, 1.0), 24, SPINNER_COLOR, 4.0)


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
