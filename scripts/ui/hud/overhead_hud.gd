class_name OverheadHud
extends Control
## MOBA-style overhead display drawn in screen space over each hero, like League
## of Legends: a solid health bar (green you, blue ally, red enemy) with the player's number on a team-coloured badge at its left end, the word
## one word over the bar's left end: a status in plain words ("Stunned", "Slowed", "Exhausted") while there is one, else the player's name (yours, your allies' and your enemies'). Under your own bar a thin stamina bar shows
## while it is not full (your eyes are on your kid while you tap to walk, and stamina only matters while walking); at 0 it reads Exhausted and
## runs out over the 2 s. No health numbers and no damage numbers (heals still pop a green "+N"). While a
## team holds the enemy base, a spinner fills over its kids' heads. Reads the sim
## and projects through the match camera.

## The bar before `bar_scale` (visual pass: 92 x 13 read as a hairline at phone size; now a chunky rounded bar).
const BAR_SIZE: Vector2 = Vector2(96.0, 18.0)
## The dark outline round the bar: thin (screen px, kept at any bar_scale).
const BAR_OUTLINE: float = 2.0
## The word over your own bar, at its top left (its middle level with the bar's top outline), and how far it reaches above the bar (a
## little under its own size: half of it sits on the outline).
const LABEL_FONT: int = UiMetrics.WORLD_SMALL
const LABEL_LINE: float = float(LABEL_FONT) * 0.8
## The word is `LABEL_FONT` (its largest) while it fits the bar's FULL width (never the filled part: it does not move with the health), and
## shrinks one pixel at a time until it does (owner: it is never cut, never wrapped, never ends in dots). `LABEL_FONT_MIN` is only the safety
## floor: at the game's bar (96 x 0.75 = 72 px, 64 px of room after `LABEL_INSET`) a 12 letter name of the widest letter (the name limit) and every
## status word, alone or with AFK, fit at 5 px or more. It starts `LABEL_INSET` in from the bar's left end, clear of the badge.
const LABEL_FONT_MIN: int = UiMetrics.WORLD_MIN
## The word's black outline is this share of its size (5 px at 16, so the look at the largest size is what it was), at least 1 px, so a small word is
## not drowned in a thick outline.
const LABEL_OUTLINE_SHARE: float = 0.3
## The badge at the bar's left end (owner): the player's number, as the room shows it, in white on the team's colour. A rounded square a
## little taller than the bar, covering the first `BADGE_OVERLAP` px of it, its number the world text size with the bars' dark outline.
const BADGE_FONT: int = UiMetrics.WORLD
const BADGE_PAD: float = 4.0
const BADGE_SIZE: float = float(BADGE_FONT) + BADGE_PAD * 2.0
const BADGE_OVERLAP: float = 6.0
const BADGE_TEXT_OUTLINE: int = 3
const LABEL_INSET: float = BADGE_OVERLAP + BAR_OUTLINE
## A bar keeps this far from the screen's sides (its badge included), so a kid against a wall never loses its number.
const EDGE_PAD: float = 4.0
## The bar sits just above the top of the head (the kid's model is KID_HEIGHT tall).
const HEAD_HEIGHT: float = KidModel.KID_HEIGHT + 0.2
## The dark outline round the word over a bar and the status words (screen px).
const TEXT_OUTLINE: int = 5
const TAG_TIME: float = 0.42
const TAG_POP_TIME: float = 0.08
const TAG_FONT: int = LABEL_FONT
const POPUP_TIME: float = 0.9
const POPUP_RISE: float = 56.0
## The status word takes the place of the name over the bar (owner): the same size, in the same spot, never stacked above it.
const STATUS_FONT: int = LABEL_FONT
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
## The word over an exhausted kid, yours or another's, in sentence case like every status word (owner).
const EXHAUSTED: String = "Exhausted"
## The word over a kid whose player has been gone too long (online): their kid stands still until they come back.
const AWAY: String = "AFK"
const POPUP_FONT: int = UiMetrics.BODY
## A quick chat bubble: the words' size, the padding round them and how far over the health number it floats.
const SAY_FONT: int = UiMetrics.DETAIL
const SAY_PAD: Vector2 = Vector2(16.0, 8.0)
const SAY_LIFT: float = 12.0
## The empty part of a health or stamina bar: a flat, opaque dark gray (just the colour: no sheen, no texture).
const EMPTY_BAR: Color = Color(0.18, 0.19, 0.21)
const SELF_COLOR: Color = Color(0.35, 0.95, 0.35)
const ALLY_COLOR: Color = Color(0.35, 0.65, 1.0)
const ENEMY_COLOR: Color = Color(1.0, 0.3, 0.3)
const DAMAGE_COLOR: Color = Color(1.0, 0.95, 0.85)
const HEAL_COLOR: Color = Color(0.55, 1.0, 0.45)
## The spinner above a hero holding the enemy base.
const SPINNER_RADIUS: float = 15.0
const SPINNER_LIFT: float = 36.0
const SPINNER_COLOR: Color = Color(1.0, 0.86, 0.3)
## The rescuer's ring (green: it frees a teammate), the capturer's stays gold.
const RESCUE_COLOR: Color = Color(0.45, 0.9, 0.5)
## On each rescue area: the seconds until it is ready (`MatchSim.rescue_ready_left`) in white, then nothing: the area just turns the team's
## colour (`StreetMap.sync_rescue`).
const AREA_COUNT_FONT: int = UiMetrics.HEADLINE
## The number's outline is fully opaque (the kid-label outline `Icons.OUTLINE` is 0.9), so the white number reads on its box.
const AREA_COUNT_OUTLINE: Color = Color(Icons.OUTLINE, 1.0)
const SPINNER_BACK: Color = Color(0.08, 0.06, 0.1, 0.7)
const SPINNER_SPEED: float = 5.0

## Where the screen draws each kid between two sim ticks (null: where the sim has it): the bar rides on the drawn kid.
var blend: TickBlend
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
var _badge_box: StyleBoxFlat
## The bar groups drawn so far this frame (`_clear_of_others`).
var _placed: Array[Rect2] = []
## {id, amount, age, heal, jitter}
var _popups: Array[Dictionary] = []
## Quick chat bubbles over kids: {id, text, age}.
var _says: Array[Dictionary] = []
## One brief tag marker per kid, fixed at the last visible street position.
var _tags: Dictionary[int, Dictionary] = {}
## Online: the kids whose players are away (AFK), set from the room.
var away_ids: Dictionary[int, bool] = {}
## Each player's name by kid id (you: your name; online: the room's names). A kid with none is named after its character.
var names: Dictionary[int, String] = {}


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
	_badge_box = StyleBoxFlat.new()
	_badge_box.border_color = Icons.OUTLINE
	_badge_box.set_border_width_all(int(BAR_OUTLINE))
	_badge_box.set_corner_radius_all(UiSkin.RADIUS_S)
	_badge_box.anti_aliasing = true


## The status words shown above a hero's bar: "Stunned", "Slowed", ...
## Empty when nothing is wrong. Heroes have no name text.
func text_for(id: int) -> String:
	return _status_text(_sim.players[id])


## A tagged kid stands on the sidewalk, which already says it: no "Tagged" over each one (two of them ran into one word).
## A negative status shows instead of "Exhausted" (never stacked); the exhaustion keeps counting in the sim meanwhile, so the word returns when the status ends.
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


func show_tag(id: int, world: Vector3) -> void:
	_tags[id] = {"world": world, "age": 0.0}
	queue_redraw()


func tag_count() -> int:
	return _tags.size()


func tick(delta: float) -> void:
	for id: int in _tags.keys():
		_tags[id]["age"] = (_tags[id]["age"] as float) + delta
		if (_tags[id]["age"] as float) >= TAG_TIME:
			_tags.erase(id)
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


## The one word over a kid's bar (owner): its status while it has one ("Stunned", "Slowed", ...), else the player's name, for you, your
## allies and your enemies alike.
func label_for(state: PlayerState) -> String:
	var status: String = _status_text(state)
	if not status.is_empty():
		return status
	return name_of(state)


## The player's name over a kid: the one it was given, else its character's name (the bots').
func name_of(state: PlayerState) -> String:
	var given: String = names.get(state.id, "") as String
	if not given.is_empty():
		return given
	for character: CharacterDef in _sim.rules.characters:
		if character.id == state.character_id:
			return character.display_name
	return ""


## The number on a kid's badge, the same one the room shows: blue's places first, then red's (`RoomPlayerCards.slot_number`), each team's
## in the order its players were added.
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


## The badge's colour: your team's blue or the other team's red (orange and blue with the colour-blind setting), as the bases show them.
func badge_color(state: PlayerState) -> Color:
	return TeamColors.world_own() if state.team == _local_team else TeamColors.world_enemy()


func _on_healed(id: int, amount: int) -> void:
	_popups.append({"id": id, "amount": amount, "age": 0.0, "heal": true, "jitter": 0.0})


func _head(state: PlayerState) -> Variant:
	var at: Vector2 = blend.kid(state) if blend != null else state.position
	var world: Vector3 = Vector3(at.x, HEAD_HEIGHT * view_scale, at.y)
	if _camera == null or _camera.is_position_behind(world):
		return null
	return _camera.screen_position(world) if _camera is ArenaCamera else _camera.unproject_position(world)


func _draw() -> void:
	if _sim == null:
		return
	_draw_tags()
	if _sim.phase in [MatchSim.Phase.POINT_FREEZE, MatchSim.Phase.MATCH_OVER]:
		return
	_draw_rescue_areas()
	for id: int in _sim.players:
		_draw_progress(_sim.players[id])
	# your own bar first: it stays on your head and the others stack above it where they would overlap (`_clear_of_others`)
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
	var rise: float = BAR_SIZE.y * bar_scale + LABEL_LINE + SAY_LIFT
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


## The health bar over `state`'s head, centred on it and pulled inside the screen's sides with its badge; null when it has no bar to show.
func _bar_rect(state: PlayerState) -> Variant:
	var head: Variant = _head(state)
	if head == null or not bar_visible(state):
		return null
	var bar: Vector2 = BAR_SIZE * bar_scale
	var left: float = BADGE_SIZE - BADGE_OVERLAP + EDGE_PAD
	return Rect2(Vector2(clampf((head as Vector2).x - bar.x / 2.0, left, maxf(size.x - bar.x - EDGE_PAD, left)), (head as Vector2).y), bar)


## Where a bar group (its badge, its word and its bar) over `rect` goes: `rect`, or the nearest place above it that no bar group already
## drawn this frame covers (never above the header), so kids standing together each keep a readable number. Remembers the place it gives.
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
	_draw_bar(rect, fill, color)
	_draw_label(label_for(state), rect)
	_draw_badge(state, rect)
	if stamina_visible(state):
		var stamina: Vector2 = STAMINA_SIZE * bar_scale
		var under: Rect2 = Rect2(Vector2(rect.position.x, stamina_top(rect)), stamina)
		var look: Array = stamina_fill(state)
		_draw_thin(under, look[0] as float, look[1] as Color)


## The ring over a kid holding the enemy base or rescuing a teammate, drawn under the bars at the kid's own head (kids who stand together
## show the same ring on the same spot).
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


## The word at the bar's top left corner, clear of the badge, its middle level with the bar's top outline; kept inside the screen sides.
func _draw_label(text: String, rect: Rect2) -> void:
	if text.is_empty():
		return
	var font: Font = KalyeahTheme.ACTION_FONT
	var room: float = label_room(rect.size.x)
	var font_size: int = label_font_size(text, room)
	var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size).x
	var centre: Vector2 = keep_on_screen(Vector2(rect.position.x + LABEL_INSET + width / 2.0, label_baseline(rect, font_size)), font, text, font_size)
	_text(font, text, centre, font_size, Color.WHITE, label_outline(font_size), Color.BLACK)


## The width the word over a bar may fill: the bar's full width (`bar_width`, at the current `bar_scale`) less the badge side's inset.
static func label_room(bar_width: float) -> float:
	return maxf(bar_width - LABEL_INSET, 0.0)


## The font size of `text` over a bar with `room` to spare: `LABEL_FONT`, or smaller (never under `LABEL_FONT_MIN`) until it fits. It depends
## on the word and the room only, never on the health.
static func label_font_size(text: String, room: float) -> int:
	var font: Font = KalyeahTheme.ACTION_FONT
	var font_size: int = LABEL_FONT
	while font_size > LABEL_FONT_MIN and font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size).x > room:
		font_size -= 1
	return font_size


## The black outline round a word drawn at `font_size`: it shrinks with the word.
static func label_outline(font_size: int) -> int:
	return maxi(roundi(float(font_size) * LABEL_OUTLINE_SHARE), 1)


## The badge at the bar's left end: the player's number, white, on a rounded square in the team's colour.
func _draw_badge(state: PlayerState, rect: Rect2) -> void:
	var badge: Rect2 = Rect2(Vector2(rect.position.x + BADGE_OVERLAP - BADGE_SIZE, rect.get_center().y - BADGE_SIZE / 2.0), Vector2.ONE * BADGE_SIZE)
	_badge_box.bg_color = badge_color(state)
	draw_style_box(_badge_box, badge)
	_text(KalyeahTheme.ACTION_FONT, str(player_number(state)), Vector2(badge.get_center().x, badge.get_center().y + float(BADGE_FONT) * 0.36), BADGE_FONT, Color.WHITE, BADGE_TEXT_OUTLINE)


## Where the stamina bar starts under a health bar at `rect`: no gap, the two bars share the line between them (the stamina bar's top
## outline is the health bar's bottom outline).
static func stamina_top(rect: Rect2) -> float:
	return rect.end.y + BAR_OUTLINE


## The baseline of the word over a bar at `rect`: its middle level with the middle of the bar's top outline.
static func label_baseline(rect: Rect2, font_size: int = LABEL_FONT) -> float:
	return rect.position.y - BAR_OUTLINE / 2.0 + float(font_size) * 0.36


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


## A spinning ring over a hero holding the enemy base: the ring fills as the point
## gets closer (it scores when it is full).
func _draw_spinner(center: Vector2, progress: float, color: Color) -> void:
	draw_circle(center, SPINNER_RADIUS + 4.0, SPINNER_BACK)
	draw_arc(center, SPINNER_RADIUS, 0.0, TAU, 28, Color(1.0, 1.0, 1.0, 0.25), 4.0)
	var spin: float = float(Time.get_ticks_msec()) / 1000.0 * SPINNER_SPEED
	draw_arc(center, SPINNER_RADIUS, spin, spin + TAU * 0.28, 12, Color(1.0, 1.0, 1.0, 0.9), 4.0)
	draw_arc(center, SPINNER_RADIUS - 6.0, -PI / 2.0, -PI / 2.0 + TAU * clampf(progress, 0.0, 1.0), 24, color, 4.0)


## What each rescue area says in its middle: its countdown, and nothing once a teammate can rescue there.
func rescue_area_text(team: int) -> String:
	var left: float = _sim.rescue_ready_left[team]
	return str(ceili(left)) if left > 0.0 else ""


func _draw_rescue_areas() -> void:
	if _camera == null:
		return
	for team: int in [0, 1]:
		var middle: Vector2 = _sim.layout.rescue_rect(_sim.side_for_team(team)).get_center()
		var world: Vector3 = Vector3(middle.x, 0.0, middle.y)
		if _camera.is_position_behind(world):
			continue
		var at: Vector2 = _camera.screen_position(world) if _camera is ArenaCamera else _camera.unproject_position(world)
		var text: String = rescue_area_text(team)
		if text.is_empty():
			continue
		# the text's middle on the area's middle (`_text` takes the baseline); white, like the area while it counts down
		_text(KalyeahTheme.ACTION_FONT, text, at + Vector2(0.0, float(AREA_COUNT_FONT) * 0.36), AREA_COUNT_FONT, Color.WHITE, TEXT_OUTLINE, AREA_COUNT_OUTLINE)


func _draw_tags() -> void:
	if _camera == null:
		return
	for tag: Dictionary in _tags.values():
		var world: Vector3 = tag["world"] as Vector3
		world.y += HEAD_HEIGHT * view_scale
		if _camera.is_position_behind(world):
			continue
		var at: Vector2 = _camera.screen_position(world) if _camera is ArenaCamera else _camera.unproject_position(world)
		at = keep_on_screen(at, KalyeahTheme.ACTION_FONT, "TAYA!", TAG_FONT)
		var age: float = tag["age"] as float
		var pop: float = clampf(age / TAG_POP_TIME, 0.0, 1.0)
		var scale: float = 1.0 if Settings.reduced_motion else lerpf(0.8, 1.0, 1.0 - pow(1.0 - pop, 3.0))
		var color: Color = Color.WHITE
		color.a = clampf((TAG_TIME - age) / (TAG_TIME - TAG_POP_TIME), 0.0, 1.0)
		draw_set_transform(at, 0.0, Vector2.ONE * scale)
		_text(KalyeahTheme.ACTION_FONT, "TAYA!", Vector2.ZERO, TAG_FONT, color, TEXT_OUTLINE)
		draw_set_transform(Vector2.ZERO)


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


func _text(font: Font, text: String, center: Vector2, font_size: int, color: Color, outline: int, outline_color: Color = Icons.OUTLINE) -> void:
	if text.is_empty():
		return
	var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size).x
	var origin: Vector2 = center - Vector2(width / 2.0, 0.0)
	var dark: Color = outline_color
	dark.a = color.a
	draw_string_outline(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, outline, dark)
	draw_string(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, color)
