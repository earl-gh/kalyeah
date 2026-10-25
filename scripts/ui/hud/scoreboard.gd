class_name Scoreboard
extends Control
## The score in the middle of the header: your team's points (blue) and theirs (red), with how many points win the match between them
## (the match clock is the chip at the header's right). It stands on the header deck without a card of its own. Team-relative: you are always on the left.

const BIG_FONT: int = 44
const CENTER_CAPTION: int = UiMetrics.CAPTION
const SMALL_FONT: int = UiMetrics.CAPTION

var mine: int = 0
var theirs: int = 0
var first_to: int = 3
## Whole seconds of play shown on the clock.
var seconds: int = 0
## How far the running capture is, 0 to 1 (-1: none runs), and whether it is your team's.
var capture: float = -1.0
var capture_ours: bool = true
## One entry per kid of your team and of theirs, in id order: true while the kid is free, false while it is tagged (the pips under the score).
var roster_mine: Array[bool] = []
var roster_theirs: Array[bool] = []

const CAPTURE_HEIGHT: float = 7.0
const CAPTURE_INSET: float = 14.0
const CAPTURE_TRACK: Color = Color(0.0, 0.0, 0.0, 0.5)
## Progress is shown in steps of this much, so the card is not redrawn every frame for nothing.
const CAPTURE_STEP: float = 0.02
const PIP_RADIUS: float = 5.5
const PIP_SPACING: float = 17.0
## The pips sit this far above the cell's lower edge; the number rides a little above the middle to leave them room.
const PIP_LIFT: float = 14.0
const NUMBER_RISE: float = 0.06
## A team's cell is this much wider than tall; its corners are this round.
const CELL_ASPECT: float = 1.2
const CELL_RADIUS: int = 16


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
	accessibility_name = "Score: you %d, them %d, first to %d, time %s. Kids free: you %d of %d, them %d of %d" % [
		mine, theirs, first_to, clock_text(), roster_mine.count(true), roster_mine.size(), roster_theirs.count(true), roster_theirs.size()]


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


## The pips under each team's number: a filled dot for a free kid, a hollow one for a tagged kid, so who is still running is
## readable at a glance (the tagged cards are gone). Both rows count what the sim says (you already
## know how many kids you tagged; which of the kids in their box is free is still hidden).
func show_roster(sim: MatchSim, my_team: int) -> void:
	var mine_now: Array[bool] = []
	var theirs_now: Array[bool] = []
	var ids: Array = sim.players.keys()
	ids.sort()
	for id: int in ids:
		var state: PlayerState = sim.players[id]
		if state.team == my_team:
			mine_now.append(not state.tagged)
		else:
			theirs_now.append(not state.tagged)
	if mine_now != roster_mine or theirs_now != roster_theirs:
		roster_mine = mine_now
		roster_theirs = theirs_now
		_describe()
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
	var box: float = h - 12.0
	var cell: Vector2 = Vector2(box * CELL_ASPECT, box)
	var mine_cell: Rect2 = Rect2(Vector2(6.0, 6.0), cell)
	var theirs_cell: Rect2 = Rect2(Vector2(w - 6.0 - cell.x, 6.0), cell)
	_draw_cell(mine_cell, TeamColors.ui_own())
	_draw_cell(theirs_cell, TeamColors.ui_enemy())
	var number_y: float = h / 2.0 - box * NUMBER_RISE
	_center_text(KalyeahTheme.ACTION_FONT, str(mine), Vector2(mine_cell.get_center().x, number_y), BIG_FONT, Color.WHITE)
	_center_text(KalyeahTheme.ACTION_FONT, str(theirs), Vector2(theirs_cell.get_center().x, number_y), BIG_FONT, Color.WHITE)
	_draw_pips(roster_mine, Vector2(mine_cell.get_center().x, mine_cell.end.y - PIP_LIFT))
	_draw_pips(roster_theirs, Vector2(theirs_cell.get_center().x, theirs_cell.end.y - PIP_LIFT))
	# between the cells: how many points win the match, FIRST TO over the number (the clock is the chip at the header's right)
	_center_text(KalyeahTheme.ACTION_FONT, UiText.t("FIRST TO"), Vector2(w / 2.0, h * 0.34), CENTER_CAPTION, KalyeahTheme.AMBER)
	_center_text(KalyeahTheme.ACTION_FONT, str(first_to), Vector2(w / 2.0, h * 0.66), BIG_FONT - 6, KalyeahTheme.AMBER)
	if capture >= 0.0:
		var track: Rect2 = Rect2(CAPTURE_INSET, h - 6.0 - CAPTURE_HEIGHT, w - CAPTURE_INSET * 2.0, CAPTURE_HEIGHT)
		_round_rect(track, CAPTURE_TRACK, CAPTURE_HEIGHT / 2.0)
		if capture > 0.0:
			_round_rect(Rect2(track.position, Vector2(track.size.x * capture, track.size.y)), TeamColors.capture_own() if capture_ours else TeamColors.capture_enemy(), CAPTURE_HEIGHT / 2.0)


## A team's cell: its colour in a thick navy outline with a glossy sheen on the upper half, like a Clash Royale panel.
func _draw_cell(rect: Rect2, color: Color) -> void:
	RoyaleStyle.chunky(self, rect, color.lightened(0.1), CELL_RADIUS)


## A row of dots centred on `centre`: filled white for a free kid, a dark hollow ring for a tagged one.
func _draw_pips(roster: Array[bool], centre: Vector2) -> void:
	var first: float = -PIP_SPACING * float(roster.size() - 1) / 2.0
	for index: int in roster.size():
		var at: Vector2 = centre + Vector2(first + PIP_SPACING * float(index), 0.0)
		if roster[index]:
			draw_circle(at, PIP_RADIUS + 1.5, Color(0.0, 0.0, 0.0, 0.45))
			draw_circle(at, PIP_RADIUS, Color.WHITE)
		else:
			draw_circle(at, PIP_RADIUS + 1.5, Color(1.0, 1.0, 1.0, 0.55))
			draw_circle(at, PIP_RADIUS - 1.0, Color(0.05, 0.05, 0.08, 0.85))


func _round_rect(rect: Rect2, color: Color, radius: float) -> void:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = color
	box.set_corner_radius_all(int(radius))
	draw_style_box(box, rect)


func _center_text(font: Font, text: String, center: Vector2, font_size: int, color: Color) -> void:
	var text_size: Vector2 = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size)
	var origin: Vector2 = center + Vector2(-text_size.x / 2.0, font_size * 0.35)
	draw_string_outline(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, UiSkin.TEXT_LINE, UiSkin.LINE_COLOR)
	draw_string(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, color)
