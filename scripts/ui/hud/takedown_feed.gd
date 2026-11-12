class_name TakedownFeed
extends Control
## Top-right airy translucent callout pills for tags and rescues (`push_tag`, `push_rescue`, `clear`, `tick`).
## Dynamically measures string widths with >= 24px right padding so player names never clip.

const WIDTH: float = 420.0
const ROW_H: float = 28.0
const ROW_GAP: float = 5.0
const RIGHT_PAD: float = 24.0
const LEFT_PAD: float = 12.0
const INNER_GAP: float = 8.0
const ACCENT_W: float = 4.0
const MAX_ROWS: int = 4
const HEIGHT: float = ROW_H * float(MAX_ROWS) + ROW_GAP * float(MAX_ROWS - 1) + 4.0
const ITEM_LIFE: float = 3.4
const ENTER_TIME: float = 0.14
const FADE_TIME: float = 0.50

const PILL_BG: Color = Color(0.07, 0.09, 0.12, 0.44)
const PILL_RIM: Color = Color(1.0, 1.0, 1.0, 0.20)
const ALLY_SKY: Color = Color(0.42, 0.74, 1.0, 0.96)
const ENEMY_CORAL: Color = Color(1.0, 0.46, 0.40, 0.96)
const BADGE_TAG: Color = Color(1.0, 0.86, 0.52, 0.95)
const BADGE_RESCUE: Color = Color(0.44, 0.96, 0.66, 0.95)


class Entry extends RefCounted:
	var left_name: String = ""
	var right_name: String = ""
	var badge: String = ""
	var detail: String = ""
	var friendly: bool = true
	var is_rescue: bool = false
	var age: float = 0.0
	var life: float = ITEM_LIFE


var _entries: Array[Entry] = []
var _pill_box: StyleBoxFlat


func _init() -> void:
	name = "TakedownFeed"
	custom_minimum_size = Vector2(WIDTH, HEIGHT)
	size = Vector2(WIDTH, HEIGHT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_pill_box = StyleBoxFlat.new()
	_pill_box.bg_color = PILL_BG
	_pill_box.border_color = PILL_RIM
	_pill_box.set_border_width_all(1)
	_pill_box.set_corner_radius_all(UiSkin.RADIUS_S)
	_pill_box.anti_aliasing = true


func push_tag(tagger_name: String, victim_name: String, ally_scored: bool, detail: String = "") -> void:
	var item: Entry = Entry.new()
	item.left_name = _trim_name(tagger_name, 14)
	item.right_name = _trim_name(victim_name, 14)
	item.badge = "TAG"
	item.detail = _trim_name(detail, 10)
	item.friendly = ally_scored
	item.is_rescue = false
	_push(item)


func push_rescue(rescuer_name: String, saved_name: String, ours: bool) -> void:
	var item: Entry = Entry.new()
	item.left_name = _trim_name(rescuer_name, 14)
	item.right_name = _trim_name(saved_name, 14)
	item.badge = "SAVED"
	item.detail = ""
	item.friendly = ours
	item.is_rescue = true
	_push(item)


func clear() -> void:
	if _entries.is_empty():
		return
	_entries.clear()
	queue_redraw()


func tick(delta: float) -> void:
	if _entries.is_empty() or delta <= 0.0:
		return
	var i: int = _entries.size() - 1
	while i >= 0:
		var item: Entry = _entries[i]
		item.age += delta
		if item.age >= item.life:
			_entries.remove_at(i)
		i -= 1
	queue_redraw()


func _push(item: Entry) -> void:
	_entries.push_front(item)
	while _entries.size() > MAX_ROWS:
		_entries.pop_back()
	queue_redraw()


static func _trim_name(raw: String, limit: int) -> String:
	var clean: String = raw.strip_edges()
	if clean.length() <= limit:
		return clean
	return clean.substr(0, maxi(limit - 1, 1)) + "…"


func _team_color(friendly: bool) -> Color:
	if friendly:
		return TeamColors.score_own() if Settings.colorblind else ALLY_SKY
	return TeamColors.score_enemy() if Settings.colorblind else ENEMY_CORAL


func _draw() -> void:
	if _entries.is_empty():
		return
	var font: Font = KalyeahTheme.ACTION_FONT

	for idx: int in _entries.size():
		var item: Entry = _entries[idx]
		var alpha: float = 1.0
		var time_left: float = item.life - item.age
		if time_left < FADE_TIME:
			alpha = clampf(time_left / FADE_TIME, 0.0, 1.0)
		var slide_x: float = 0.0
		if not Settings.reduced_motion and item.age < ENTER_TIME:
			var t: float = 1.0 - item.age / ENTER_TIME
			slide_x = t * t * 22.0

		var badge_word: String = ("· %s ·" % item.badge) if item.detail.is_empty() else ("· %s %s ·" % [item.detail.to_upper(), item.badge])
		var left_w: float = font.get_string_size(item.left_name, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL).x
		var bw: float = font.get_string_size(badge_word, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL).x
		var right_w: float = font.get_string_size(item.right_name, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL).x

		var needed_w: float = ACCENT_W + LEFT_PAD + left_w + INNER_GAP + bw + INNER_GAP + right_w + RIGHT_PAD
		var w: float = maxf(needed_w, 188.0)
		var x0: float = (size.x - 2.0 - w) + slide_x
		var y0: float = float(idx) * (ROW_H + ROW_GAP)

		var team_col: Color = Color(_team_color(item.friendly), alpha)
		var victim_col: Color = Color(_team_color(item.friendly if item.is_rescue else not item.friendly), alpha)
		var sh_col: Color = Color(0.0, 0.0, 0.0, 0.45 * alpha)

		_pill_box.bg_color = Color(PILL_BG.r, PILL_BG.g, PILL_BG.b, PILL_BG.a * alpha)
		_pill_box.border_color = Color(1.0, 1.0, 1.0, PILL_RIM.a * alpha)
		draw_style_box(_pill_box, Rect2(Vector2(x0, y0), Vector2(w, ROW_H)))
		draw_rect(Rect2(Vector2(x0 + 2.0, y0 + 5.0), Vector2(ACCENT_W - 1.0, ROW_H - 10.0)), team_col, true)

		var text_y: float = y0 + ROW_H - 8.0
		var left_x: float = x0 + ACCENT_W + LEFT_PAD
		draw_string_outline(font, Vector2(left_x, text_y), item.left_name, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, 2, sh_col)
		draw_string(font, Vector2(left_x, text_y), item.left_name, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, team_col)

		var bx: float = left_x + left_w + INNER_GAP
		var badge_col: Color = Color(BADGE_RESCUE if item.is_rescue else BADGE_TAG, alpha * 0.92)
		draw_string_outline(font, Vector2(bx, text_y - 1.0), badge_word, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL, 2, sh_col)
		draw_string(font, Vector2(bx, text_y - 1.0), badge_word, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL, badge_col)

		var right_x: float = bx + bw + INNER_GAP
		draw_string_outline(font, Vector2(right_x, text_y), item.right_name, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, 2, sh_col)
		draw_string(font, Vector2(right_x, text_y), item.right_name, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, victim_col)
