class_name Scoreboard
extends Control
## Top-right scoreboard: your team's points (blue) and theirs (red), with the match clock in
## the middle counting up ("2:45"), like the street-game video. Just the numbers with an outline
## (no card, no tiles); the team colour is the number's colour. Team-relative: you are always on the left.

const OWN: Color = Color(0.45, 0.7, 1.0)
const ENEMY: Color = Color(1.0, 0.4, 0.38)
const BIG_FONT: int = 46
const SMALL_FONT: int = UiMetrics.CAPTION

var mine: int = 0
var theirs: int = 0
var best_of: int = 3
## Whole seconds of play shown on the clock.
var seconds: int = 0


func show_score(score: MatchScore, my_team: int, match_time: float) -> void:
	var values: Array[int] = [score.points[my_team], score.points[1 - my_team], score.best_of, floori(match_time)]
	if values != [mine, theirs, best_of, seconds]:
		mine = values[0]
		theirs = values[1]
		best_of = values[2]
		seconds = values[3]
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
	_center_text(KalyeahTheme.ACTION_FONT, str(mine), Vector2(w * 0.17, h / 2.0), BIG_FONT, OWN)
	_center_text(KalyeahTheme.ACTION_FONT, str(theirs), Vector2(w * 0.83, h / 2.0), BIG_FONT, ENEMY)
	_center_text(KalyeahTheme.BODY_FONT, clock_text(), Vector2(w / 2.0, h * 0.36), SMALL_FONT + 4, Color.WHITE)
	_center_text(KalyeahTheme.ACTION_FONT, "BO%d" % best_of, Vector2(w / 2.0, h * 0.72), SMALL_FONT, KalyeahTheme.AMBER)


func _center_text(font: Font, text: String, center: Vector2, font_size: int, color: Color) -> void:
	var text_size: Vector2 = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size)
	var origin: Vector2 = center + Vector2(-text_size.x / 2.0, font_size * 0.35)
	draw_string_outline(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, 6, Icons.OUTLINE)
	draw_string(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, color)
