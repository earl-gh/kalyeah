class_name Scoreboard
extends Control
## Top-right scoreboard pill: your team's points (blue) and theirs (red), with the
## match clock in the middle counting up ("2:45"), like the street-game video.
## Team-relative: you are always on the left.

const OWN: Color = KalyeahTheme.TEAM_BLUE
const ENEMY: Color = KalyeahTheme.TEAM_RED
const PANEL: Color = Color(0.055, 0.09, 0.16, 0.94)
const RIM: Color = Color(1.0, 0.82, 0.35)
const BIG_FONT: int = 40
const SMALL_FONT: int = UiMetrics.CAPTION

var mine: int = 0
var theirs: int = 0
var best_of: int = 3
## Whole seconds of play shown on the clock.
var seconds: int = 0
## How far the running capture is, 0 to 1 (-1: none runs), and whether it is your team's.
var capture: float = -1.0
var capture_ours: bool = true

const CAPTURE_HEIGHT: float = 7.0
## Lighter than the score boxes it runs along, so the fill reads against them.
const CAPTURE_OWN: Color = Color(0.62, 0.8, 1.0)
const CAPTURE_ENEMY: Color = Color(1.0, 0.62, 0.6)
const CAPTURE_INSET: float = 14.0
const CAPTURE_TRACK: Color = Color(0.0, 0.0, 0.0, 0.5)
## Progress is shown in steps of this much, so the card is not redrawn every frame for nothing.
const CAPTURE_STEP: float = 0.02


func show_score(score: MatchScore, my_team: int, match_time: float) -> void:
	var values: Array[int] = [score.points[my_team], score.points[1 - my_team], score.best_of, floori(match_time)]
	if values != [mine, theirs, best_of, seconds]:
		mine = values[0]
		theirs = values[1]
		best_of = values[2]
		seconds = values[3]
		_describe()
		queue_redraw()


func _ready() -> void:
	_describe()


func _describe() -> void:
	accessibility_name = "Score: you %d, them %d, best of %d, time %s" % [mine, theirs, best_of, clock_text()]


## The strip along the card's lower edge that fills while a team holds the other base (or the other team is all
## tagged): blue when it is yours, red when it is theirs. Hidden when no capture runs.
func show_capture(sim: MatchSim, my_team: int) -> void:
	var value: float = -1.0
	var ours: bool = capture_ours
	if sim.capture_team >= 0 and sim.phase == MatchSim.Phase.PLAYING:
		value = snappedf(clampf(sim.capture_elapsed / maxf(sim.rules.capture_time, 0.001), 0.0, 1.0), CAPTURE_STEP)
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
	var w: float = size.x
	var h: float = size.y
	# the same night panel as every other card, with its lit edge and soft shadow
	draw_style_box(UiSkin.plate("night", 0.0, 18), Rect2(0.0, 0.0, w, h))
	var box: float = h - 12.0
	_round_rect(Rect2(6.0, 6.0, box * 1.25, box), OWN, 12.0)
	_round_rect(Rect2(w - 6.0 - box * 1.25, 6.0, box * 1.25, box), ENEMY, 12.0)
	_center_text(KalyeahTheme.ACTION_FONT, str(mine), Vector2(6.0 + box * 0.625, h / 2.0), BIG_FONT, Color.WHITE)
	_center_text(KalyeahTheme.ACTION_FONT, str(theirs), Vector2(w - 6.0 - box * 0.625, h / 2.0), BIG_FONT, Color.WHITE)
	_center_text(KalyeahTheme.BODY_FONT, clock_text(), Vector2(w / 2.0, h * 0.36), SMALL_FONT + 4, Color.WHITE)
	_center_text(KalyeahTheme.ACTION_FONT, UiText.f("BO%d", [best_of]), Vector2(w / 2.0, h * 0.72), SMALL_FONT, KalyeahTheme.AMBER)
	if capture >= 0.0:
		var track: Rect2 = Rect2(CAPTURE_INSET, h - 6.0 - CAPTURE_HEIGHT, w - CAPTURE_INSET * 2.0, CAPTURE_HEIGHT)
		_round_rect(track, CAPTURE_TRACK, CAPTURE_HEIGHT / 2.0)
		if capture > 0.0:
			_round_rect(Rect2(track.position, Vector2(track.size.x * capture, track.size.y)), CAPTURE_OWN if capture_ours else CAPTURE_ENEMY, CAPTURE_HEIGHT / 2.0)


func _round_rect(rect: Rect2, color: Color, radius: float) -> void:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = color
	box.set_corner_radius_all(int(radius))
	draw_style_box(box, rect)


func _center_text(font: Font, text: String, center: Vector2, font_size: int, color: Color) -> void:
	var text_size: Vector2 = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size)
	var origin: Vector2 = center + Vector2(-text_size.x / 2.0, font_size * 0.35)
	draw_string_outline(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, 6, Icons.OUTLINE)
	draw_string(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, color)
