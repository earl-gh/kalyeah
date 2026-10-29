class_name Scoreboard
extends Control
## Team-relative scores on the right sidewalk: blue in the lower half, red in the upper half.

const BIG_FONT: int = UiMetrics.HEADLINE
const SMALL_FONT: int = UiMetrics.CAPTION
const CELL_SIZE: Vector2 = Vector2(56.0, 76.0)
const PADDING: float = 8.0
const TEXT_OUTLINE: int = 4
const CAPTURE_GAP: float = 4.0

var play_area: Rect2 = Rect2(70, 160, 560, 830)
var mine: int = 0
var theirs: int = 0
var first_to: int = 3
## Whole seconds of play shown on the clock.
var seconds: int = 0
## How far the running capture is, 0 to 1 (-1: none runs), and whether it is your team's.
var capture: float = -1.0
var capture_ours: bool = true

const CAPTURE_HEIGHT: float = 7.0
const CAPTURE_INSET: float = 14.0
const CAPTURE_TRACK: Color = Color(0.0, 0.0, 0.0, 0.5)
## Progress is shown in steps of this much, so the card is not redrawn every frame for nothing.
const CAPTURE_STEP: float = 0.02
## A team's cell is this much wider than tall; its corners are this round.
const CELL_ASPECT: float = 1.2
const CELL_RADIUS: int = UiSkin.RADIUS_M


func show_score(score: MatchScore, my_team: int, match_time: float) -> void:
	var values: Array[int] = [score.points[my_team], score.points[1 - my_team], score.first_to, floori(match_time)]
	if values != [mine, theirs, first_to, seconds]:
		mine = values[0]
		theirs = values[1]
		first_to = values[2]
		seconds = values[3]
		_describe()
		queue_redraw()


func _ready() -> void:
	_describe()


func _describe() -> void:
	accessibility_name = "Score: you %d, them %d, first to %d, time %s" % [mine, theirs, first_to, clock_text()]


## The strip along the card's lower edge that fills while a team holds the other base (or the other team is all
## tagged): blue when it is yours, red when it is theirs. Hidden when no capture runs.
func show_capture(sim: MatchSim, my_team: int) -> void:
	var value: float = -1.0
	var ours: bool = capture_ours
	if sim.capture_team >= 0 and sim.phase == MatchSim.Phase.PLAYING:
		value = snappedf(clampf(sim.capture_elapsed / maxf(sim.capture_needed, 0.001), 0.0, 1.0), CAPTURE_STEP)
		ours = sim.capture_team == my_team
	if not is_equal_approx(value, capture) or ours != capture_ours:
		capture = value
		capture_ours = ours
		queue_redraw()


## The match clock as m:ss.
func clock_text() -> String:
	return "%d:%02d" % [seconds / 60, seconds % 60]


## Plain-text version (tests, accessibility).
func summary() -> String:
	return "YOU %d - %d THEM   %s" % [mine, theirs, clock_text()]


func _draw() -> void:
	var cells: Array[Rect2] = cell_rects()
	var mine_cell: Rect2 = cells[0]
	var theirs_cell: Rect2 = cells[1]
	_draw_cell(mine_cell, TeamColors.score_own())
	_draw_cell(theirs_cell, TeamColors.score_enemy())
	_center_text(KalyeahTheme.ACTION_FONT, str(mine), value_rect(mine_cell, capture >= 0.0 and capture_ours).get_center(), BIG_FONT, Color.WHITE)
	_center_text(KalyeahTheme.ACTION_FONT, str(theirs), value_rect(theirs_cell, capture >= 0.0 and not capture_ours).get_center(), BIG_FONT, Color.WHITE)
	if capture >= 0.0:
		var cell: Rect2 = mine_cell if capture_ours else theirs_cell
		var track: Rect2 = capture_rect(cell)
		_round_rect(track, CAPTURE_TRACK, CAPTURE_HEIGHT / 2.0)
		if capture > 0.0:
			_round_rect(Rect2(track.position, Vector2(track.size.x * capture, track.size.y)), TeamColors.capture_own() if capture_ours else TeamColors.capture_enemy(), CAPTURE_HEIGHT / 2.0)


func set_play_area(rect: Rect2) -> void:
	if play_area.is_equal_approx(rect):
		return
	play_area = rect
	queue_redraw()


## Blue is in the lower half; red in the upper half, on the viewer's right sidewalk.
func cell_rects() -> Array[Rect2]:
	var cell: Vector2 = CELL_SIZE
	var x: float = minf(play_area.end.x + cell.x / 2.0, size.x - cell.x / 2.0)
	return [Rect2(Vector2(x, play_area.position.y + play_area.size.y * 0.75) - cell / 2.0, cell), Rect2(Vector2(x, play_area.position.y + play_area.size.y * 0.25) - cell / 2.0, cell)]


## Space for the outlined score, with a separate lane for capture progress when shown.
func value_rect(cell: Rect2, capturing: bool) -> Rect2:
	var rect: Rect2 = cell.grow(-PADDING)
	if capturing:
		rect.size.y -= CAPTURE_HEIGHT + CAPTURE_GAP
	return rect


func capture_rect(cell: Rect2) -> Rect2:
	return Rect2(Vector2(cell.position.x + PADDING, cell.end.y - PADDING - CAPTURE_HEIGHT), Vector2(cell.size.x - PADDING * 2.0, CAPTURE_HEIGHT))


## A team's cell: its colour in a thick navy outline with a glossy sheen on the upper half, like a Clash Royale panel.
func _draw_cell(rect: Rect2, color: Color) -> void:
	RoyaleStyle.chunky(self, rect, color, CELL_RADIUS)


func _round_rect(rect: Rect2, color: Color, radius: float) -> void:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = color
	box.set_corner_radius_all(int(radius))
	draw_style_box(box, rect)


func _center_text(font: Font, text: String, center: Vector2, font_size: int, color: Color) -> void:
	var text_size: Vector2 = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size)
	var origin: Vector2 = center + Vector2(-text_size.x / 2.0, font.get_ascent(font_size) - font.get_height(font_size) / 2.0)
	draw_string_outline(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, TEXT_OUTLINE, Icons.OUTLINE)
	draw_string(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, color)
