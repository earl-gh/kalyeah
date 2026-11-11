class_name TakedownFeed
extends Control
## Top-right 90s Filipino Komiks slanted callout strips for tags and rescues (`push_tag`, `push_rescue`, `clear`, `tick`).

const WIDTH: float = 436.0
const ROW_H: float = 36.0
const ROW_GAP: float = 6.0
const SLANT: float = 8.0
const RIGHT_PAD: float = 28.0
const LEFT_PAD: float = 10.0
const INNER_GAP: float = 10.0
const STRIPE_W: float = 12.0
const MAX_ROWS: int = 4
const HEIGHT: float = ROW_H * float(MAX_ROWS) + ROW_GAP * float(MAX_ROWS - 1) + 8.0
const ITEM_LIFE: float = 3.8
const ENTER_TIME: float = 0.16
const FADE_TIME: float = 0.55

const INK: Color = Color("141016")
const SHADOW: Color = Color(0.08, 0.06, 0.09, 0.82)
const PAPER: Color = Color("f5ebd6")
const GOLD: Color = Color("ffbe2e")
const COBALT: Color = Color("2b7bff")
const CRIMSON: Color = Color("f23838")
const RESCUE_GREEN: Color = Color("38e864")


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


func _init() -> void:
	name = "TakedownFeed"
	custom_minimum_size = Vector2(WIDTH, HEIGHT)
	size = Vector2(WIDTH, HEIGHT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func push_tag(tagger_name: String, victim_name: String, ally_scored: bool, detail: String = "") -> void:
	var item: Entry = Entry.new()
	item.left_name = _trim_name(tagger_name, 14)
	item.right_name = _trim_name(victim_name, 14)
	item.badge = "TAG!"
	item.detail = _trim_name(detail, 10)
	item.friendly = ally_scored
	item.is_rescue = false
	_push(item)


func push_rescue(rescuer_name: String, saved_name: String, ours: bool) -> void:
	var item: Entry = Entry.new()
	item.left_name = _trim_name(rescuer_name, 14)
	item.right_name = _trim_name(saved_name, 14)
	item.badge = "LIGTAS!"
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
		return TeamColors.score_own() if Settings.colorblind else COBALT
	return TeamColors.score_enemy() if Settings.colorblind else CRIMSON


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
			slide_x = t * t * 30.0

		var badge_word: String = item.badge if item.detail.is_empty() else "%s %s" % [item.detail.to_upper(), item.badge]
		var left_w: float = font.get_string_size(item.left_name, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL).x
		var badge_text_w: float = font.get_string_size(badge_word, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL).x
		var bw: float = badge_text_w + 16.0
		var right_w: float = font.get_string_size(item.right_name, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL).x

		var needed_w: float = SLANT + STRIPE_W + LEFT_PAD + left_w + INNER_GAP + bw + INNER_GAP + right_w + RIGHT_PAD + SLANT
		var w: float = maxf(needed_w, 228.0)
		var x0: float = (size.x - 4.0 - w) + slide_x
		var y0: float = float(idx) * (ROW_H + ROW_GAP)
		var h: float = ROW_H

		var ink_a: Color = Color(INK, alpha)
		var paper_a: Color = Color(PAPER, alpha * 0.96)
		var shadow_a: Color = Color(SHADOW, SHADOW.a * alpha)
		var team_col: Color = Color(_team_color(item.friendly), alpha)
		var victim_col: Color = Color(_team_color(item.friendly if item.is_rescue else not item.friendly), alpha)

		# Hard comic shadow + right-aligned newsprint parallelogram plate
		var sh: PackedVector2Array = PackedVector2Array([
			Vector2(x0 + SLANT + 3.0, y0 + 3.5),
			Vector2(x0 + w + 3.0, y0 + 3.5),
			Vector2(x0 + w - SLANT + 3.0, y0 + h + 3.5),
			Vector2(x0 + 3.0, y0 + h + 3.5),
		])
		draw_colored_polygon(sh, shadow_a)

		var plate: PackedVector2Array = PackedVector2Array([
			Vector2(x0 + SLANT, y0),
			Vector2(x0 + w, y0),
			Vector2(x0 + w - SLANT, y0 + h),
			Vector2(x0, y0 + h),
		])
		draw_colored_polygon(plate, paper_a)

		# Left slanted team accent wedge
		var stripe: PackedVector2Array = PackedVector2Array([
			Vector2(x0 + SLANT, y0),
			Vector2(x0 + SLANT + STRIPE_W, y0),
			Vector2(x0 + STRIPE_W, y0 + h),
			Vector2(x0, y0 + h),
		])
		draw_colored_polygon(stripe, team_col)
		draw_polyline(PackedVector2Array([plate[0], plate[1], plate[2], plate[3], plate[0]]), ink_a, 2.8, true)

		# Sequential dynamic layout: [Left Name] [Action Burst] [Right Name + >=24px right pad]
		var left_x: float = x0 + SLANT + STRIPE_W + LEFT_PAD
		var text_y: float = y0 + h - 10.0
		draw_string_outline(font, Vector2(left_x, text_y), item.left_name, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, 4, ink_a)
		draw_string(font, Vector2(left_x, text_y), item.left_name, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, team_col.lightened(0.18))

		var bx: float = left_x + left_w + INNER_GAP
		var by: float = y0 + 5.0
		var bh: float = h - 10.0
		var b_slant: float = 4.0
		var badge_poly: PackedVector2Array = PackedVector2Array([
			Vector2(bx + b_slant, by),
			Vector2(bx + bw, by),
			Vector2(bx + bw - b_slant, by + bh),
			Vector2(bx, by + bh),
		])
		var burst_fill: Color = Color(RESCUE_GREEN if item.is_rescue else GOLD, alpha)
		draw_colored_polygon(badge_poly, burst_fill)
		draw_polyline(PackedVector2Array([badge_poly[0], badge_poly[1], badge_poly[2], badge_poly[3], badge_poly[0]]), ink_a, 2.0, true)
		draw_string(font, Vector2(bx + 8.0, by + bh - 5.0), badge_word, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL, ink_a)

		var right_x: float = bx + bw + INNER_GAP
		draw_string_outline(font, Vector2(right_x, text_y), item.right_name, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, 4, ink_a)
		draw_string(font, Vector2(right_x, text_y), item.right_name, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, victim_col.lightened(0.18))
