class_name RankedSystem
extends RefCounted
## Tagalog competitive ranked progression ("Kahoy" to "Batang Kalyeah").
##
## Progression rules:
## - Online & Local Hotspot matches award full competitive LP:
##   Win = +30 LP base + up to +10 LP score-margin bonus (+2 per point lead, capped at 5).
##   Loss = -18 LP (clamped at 0 LP floor).
## - Practice matches award sparring LP across all tiers so offline players also feel steady growth
##   while Online / Hotspot remains 2.5x faster:
##   Win = +12 LP base + up to +3 LP score-margin bonus (+1 per point lead, capped at 3).
##   Loss = -6 LP (clamped at 0 LP floor).

const ONLINE_WIN_LP: int = 30
const ONLINE_LOSS_LP: int = -18
const ONLINE_MARGIN_STEP: int = 2
const ONLINE_MARGIN_CAP: int = 5

const PRACTICE_WIN_LP: int = 12
const PRACTICE_LOSS_LP: int = -6
const PRACTICE_MARGIN_CAP: int = 3

## The 7 Tagalog competitive tiers. `max_lp = -1` on `Batang Kalyeah` denotes uncapped `1500+ LP`.
const TIERS: Array[Dictionary] = [
	{
		"id": &"kahoy",
		"name": "Kahoy",
		"min_lp": 0,
		"max_lp": 199,
		"color": Color("c8874b"),
		"accent": Color("f2be86"),
		"dark": Color("5c3416"),
		"ink": Color("241207"),
		"flavor": "Warm Carved Wood",
	},
	{
		"id": &"bakal",
		"name": "Bakal",
		"min_lp": 200,
		"max_lp": 399,
		"color": Color("8a99b0"),
		"accent": Color("d0dced"),
		"dark": Color("364052"),
		"ink": Color("141923"),
		"flavor": "Forged Dark Iron",
	},
	{
		"id": &"tanso",
		"name": "Tanso",
		"min_lp": 400,
		"max_lp": 599,
		"color": Color("dc7d3e"),
		"accent": Color("ffba80"),
		"dark": Color("6b3110"),
		"ink": Color("281004"),
		"flavor": "Burnished Bronze",
	},
	{
		"id": &"pilak",
		"name": "Pilak",
		"min_lp": 600,
		"max_lp": 849,
		"color": Color("d6e5f7"),
		"accent": Color("ffffff"),
		"dark": Color("536987"),
		"ink": Color("182233"),
		"flavor": "Gleaming Silver",
	},
	{
		"id": &"ginto",
		"name": "Ginto",
		"min_lp": 850,
		"max_lp": 1149,
		"color": Color("ffc938"),
		"accent": Color("fff29c"),
		"dark": Color("8c5400"),
		"ink": Color("2e1900"),
		"flavor": "Radiant Barrio Gold",
	},
	{
		"id": &"brilyante",
		"name": "Brilyante",
		"min_lp": 1150,
		"max_lp": 1499,
		"color": Color("4ce4ff"),
		"accent": Color("d9fcff"),
		"dark": Color("0d5e8a"),
		"ink": Color("062338"),
		"flavor": "Prism Cyan Diamond",
	},
	{
		"id": &"batang_kalyeah",
		"name": "Batang Kalyeah",
		"min_lp": 1500,
		"max_lp": -1,
		"color": Color("ff6b2b"),
		"accent": Color("ffe14d"),
		"dark": Color("7d1818"),
		"ink": Color("24070c"),
		"flavor": "Mythic Komiks Street Crown",
	},
]

## Persistence file path (unit tests redirect `RankedSystem.path` just like `MatchHistory.path`).
static var path: String = "user://ranked.cfg"

static var _last_result: Dictionary = {}
static var _last_result_path: String = ""


## Returns the index (`0..6`) in `TIERS` for `lp`.
static func tier_index(lp: int) -> int:
	var clamped: int = maxi(0, lp)
	for i: int in range(TIERS.size() - 1, -1, -1):
		if clamped >= (TIERS[i]["min_lp"] as int):
			return i
	return 0


## Returns the tier definition Dictionary for `lp`.
static func tier_for_lp(lp: int) -> Dictionary:
	return TIERS[tier_index(lp)]


## Returns the Tagalog tier name (`"Kahoy"` .. `"Batang Kalyeah"`) for `lp`.
static func tier_name(lp: int) -> String:
	return TIERS[tier_index(lp)]["name"] as String


## Returns the `min_lp` threshold of the next tier above `lp`, or `-1` at `Batang Kalyeah`.
static func next_tier_lp(lp: int) -> int:
	var idx: int = tier_index(lp)
	if idx + 1 >= TIERS.size():
		return -1
	return TIERS[idx + 1]["min_lp"] as int


## Returns `0.0..1.0` progress across the current tier (`1.0` once in `Batang Kalyeah`).
static func tier_progress(lp: int) -> float:
	var clamped: int = maxi(0, lp)
	var idx: int = tier_index(clamped)
	if idx + 1 >= TIERS.size():
		return 1.0
	var floor_lp: int = TIERS[idx]["min_lp"] as int
	var ceiling_lp: int = TIERS[idx + 1]["min_lp"] as int
	var span: int = maxi(1, ceiling_lp - floor_lp)
	return clampf(float(clamped - floor_lp) / float(span), 0.0, 1.0)


## Reads the player's current saved LP (`0` when no file exists yet).
static func current_lp() -> int:
	var file: ConfigFile = ConfigFile.new()
	if file.load(path) != OK:
		return 0
	return maxi(0, int(file.get_value("ranked", "lp", 0)))


## Reads the player's highest-ever saved LP (`>= current_lp()`).
static func peak_lp() -> int:
	var file: ConfigFile = ConfigFile.new()
	if file.load(path) != OK:
		return 0
	var cur: int = maxi(0, int(file.get_value("ranked", "lp", 0)))
	var peak: int = maxi(cur, int(file.get_value("ranked", "peak_lp", cur)))
	return peak


## Computes the LP outcome without mutating disk (used by `record_match` and UI previews).
static func preview_match(mode: String, won: bool, my_score: int, their_score: int, base_lp: int = -1) -> Dictionary:
	var old_lp: int = current_lp() if base_lp < 0 else maxi(0, base_lp)
	var clean_mode: String = mode.strip_edges().to_lower()
	var margin: int = maxi(0, my_score - their_score)
	var raw_delta: int = 0
	if clean_mode == "practice":
		if won:
			raw_delta = PRACTICE_WIN_LP + mini(margin, PRACTICE_MARGIN_CAP)
		else:
			raw_delta = PRACTICE_LOSS_LP
	else:
		if won:
			raw_delta = ONLINE_WIN_LP + mini(margin, ONLINE_MARGIN_CAP) * ONLINE_MARGIN_STEP
		else:
			raw_delta = ONLINE_LOSS_LP
	var new_lp: int = maxi(0, old_lp + raw_delta)
	var delta: int = new_lp - old_lp
	var old_idx: int = tier_index(old_lp)
	var new_idx: int = tier_index(new_lp)
	return {
		"old_lp": old_lp,
		"new_lp": new_lp,
		"delta": delta,
		"tier": TIERS[new_idx]["name"] as String,
		"promoted": new_idx > old_idx,
	}


## Updates saved LP & peak LP in `RankedSystem.path` and returns:
## `{"old_lp": int, "new_lp": int, "delta": int, "tier": String, "promoted": bool}`.
static func record_match(mode: String, won: bool, my_score: int, their_score: int) -> Dictionary:
	var file: ConfigFile = ConfigFile.new()
	var loaded: bool = file.load(path) == OK
	var old_lp: int = maxi(0, int(file.get_value("ranked", "lp", 0))) if loaded else 0
	var prev_peak: int = maxi(old_lp, int(file.get_value("ranked", "peak_lp", old_lp))) if loaded else 0
	var matches: int = maxi(0, int(file.get_value("ranked", "matches", 0))) if loaded else 0
	var wins: int = maxi(0, int(file.get_value("ranked", "wins", 0))) if loaded else 0
	var outcome: Dictionary = preview_match(mode, won, my_score, their_score, old_lp)
	var new_lp: int = outcome["new_lp"] as int
	var new_peak: int = maxi(prev_peak, new_lp)
	file.set_value("ranked", "lp", new_lp)
	file.set_value("ranked", "peak_lp", new_peak)
	file.set_value("ranked", "matches", matches + 1)
	file.set_value("ranked", "wins", wins + (1 if won else 0))
	file.save(path)
	_last_result = outcome.duplicate()
	_last_result_path = path
	return outcome


## Consumes and returns the most recent `record_match` result for the active `path`, or `{}` if none.
static func consume_last_result() -> Dictionary:
	if _last_result.is_empty() or _last_result_path != path:
		return {}
	var out: Dictionary = _last_result.duplicate()
	_last_result = {}
	return out


## Deletes the saved ranked file and clears cached match state.
static func clear() -> void:
	_last_result = {}
	_last_result_path = ""
	DirAccess.remove_absolute(path)


## Draws a bold 2D 90s Filipino Komiks shield/sun/diamond/crown crest for `lp` at `center`.
## Pure CanvasItem geometry — zero StyleBox or Font allocations inside `_draw()`.
static func draw_emblem(canvas: CanvasItem, center: Vector2, radius: float, lp: int) -> void:
	if canvas == null or radius <= 1.0:
		return
	var idx: int = tier_index(lp)
	var spec: Dictionary = TIERS[idx]
	var body: Color = spec["color"] as Color
	var accent: Color = spec["accent"] as Color
	var dark: Color = spec["dark"] as Color
	var ink: Color = spec["ink"] as Color
	var shadow_off: Vector2 = Vector2(0.0, maxf(1.5, radius * 0.09))
	var stroke: float = maxf(2.0, radius * 0.11)

	# High-tier radiant sun rays behind the shield (Ginto, Brilyante, Batang Kalyeah).
	if idx >= 4:
		var ray_count: int = 8 if idx < 6 else 12
		var ray_outer: float = radius * (1.06 if idx < 6 else 1.12)
		var ray_inner: float = radius * 0.72
		var ray_poly: PackedVector2Array = _star_polygon(center, ray_count, ray_outer, ray_inner, -PI * 0.5)
		canvas.draw_colored_polygon(_offset_poly(ray_poly, shadow_off * 0.7), Color(0.0, 0.0, 0.0, 0.35))
		canvas.draw_colored_polygon(ray_poly, ink)
		var inner_rays: PackedVector2Array = _star_polygon(center, ray_count, ray_outer - stroke * 0.75, ray_inner * 0.96, -PI * 0.5)
		canvas.draw_colored_polygon(inner_rays, accent if idx == 6 else body.lightened(0.18))

	# Side wings for Pilak (3) and above.
	if idx >= 3:
		for side: float in [-1.0, 1.0]:
			var wing: PackedVector2Array = PackedVector2Array([
				center + Vector2(side * radius * 0.42, -radius * 0.28),
				center + Vector2(side * radius * 1.04, -radius * 0.52),
				center + Vector2(side * radius * 0.88, -radius * 0.05),
				center + Vector2(side * radius * 0.96, radius * 0.16),
				center + Vector2(side * radius * 0.44, radius * 0.22),
			])
			canvas.draw_colored_polygon(wing, ink)
			var wing_fill: PackedVector2Array = _inset_toward(wing, center + Vector2(side * radius * 0.65, -radius * 0.1), 0.76)
			canvas.draw_colored_polygon(wing_fill, accent if idx >= 5 else body.lightened(0.12))

	# Main shield silhouette: diamond for Brilyante, kalasag shield for all other tiers.
	var outer_shield: PackedVector2Array = _diamond_polygon(center, radius * 0.92) if idx == 5 else _shield_polygon(center, radius * 0.9)
	canvas.draw_colored_polygon(_offset_poly(outer_shield, shadow_off), Color(0.0, 0.0, 0.0, 0.42))
	canvas.draw_colored_polygon(outer_shield, ink)

	var mid_shield: PackedVector2Array = _inset_toward(outer_shield, center, 0.84)
	canvas.draw_colored_polygon(mid_shield, dark)

	var face_shield: PackedVector2Array = _inset_toward(outer_shield, center + Vector2(0.0, -radius * 0.03), 0.72)
	canvas.draw_colored_polygon(face_shield, body)

	# Upper-half komiks gloss bevel.
	var bevel: PackedVector2Array = PackedVector2Array([
		face_shield[0],
		face_shield[1],
		center + Vector2(radius * 0.36, -radius * 0.02),
		center + Vector2(-radius * 0.36, -radius * 0.02),
		face_shield[face_shield.size() - 1],
	])
	canvas.draw_colored_polygon(bevel, accent.lerp(body, 0.35))

	# Tier-specific komiks centerpiece motif.
	if idx == 0:
		# Kahoy: carved wood grain chevron.
		var chev: PackedVector2Array = _chevron_polygon(center + Vector2(0.0, radius * 0.05), radius * 0.36, radius * 0.14)
		canvas.draw_colored_polygon(chev, dark)
	elif idx == 1:
		# Bakal: forged iron cross-bar + rivets.
		var bar: PackedVector2Array = _chevron_polygon(center, radius * 0.38, radius * 0.15)
		canvas.draw_colored_polygon(bar, accent)
		for side_r: float in [-1.0, 1.0]:
			canvas.draw_circle(center + Vector2(side_r * radius * 0.44, -radius * 0.24), maxf(1.5, radius * 0.07), ink)
			canvas.draw_circle(center + Vector2(side_r * radius * 0.44, -radius * 0.24), maxf(1.0, radius * 0.04), accent)
	elif idx == 2:
		# Tanso: double burnished bronze chevrons.
		for step_y: float in [-0.1, 0.16]:
			var bronze_chev: PackedVector2Array = _chevron_polygon(center + Vector2(0.0, radius * step_y), radius * 0.36, radius * 0.12)
			canvas.draw_colored_polygon(bronze_chev, ink)
			canvas.draw_colored_polygon(_inset_toward(bronze_chev, center + Vector2(0.0, radius * step_y), 0.74), accent)
	elif idx == 3:
		# Pilak: 4-point gleaming silver star.
		var star4: PackedVector2Array = _star_polygon(center, 4, radius * 0.44, radius * 0.16, -PI * 0.5)
		canvas.draw_colored_polygon(star4, ink)
		canvas.draw_colored_polygon(_inset_toward(star4, center, 0.75), accent)
	elif idx == 4:
		# Ginto: bold 5-point barrio gold star.
		var star5: PackedVector2Array = _star_polygon(center, 5, radius * 0.46, radius * 0.2, -PI * 0.5)
		canvas.draw_colored_polygon(star5, ink)
		canvas.draw_colored_polygon(_inset_toward(star5, center, 0.76), accent)
	elif idx == 5:
		# Brilyante: inner cut prism gem + 4-point sparkle.
		var gem: PackedVector2Array = _diamond_polygon(center, radius * 0.42)
		canvas.draw_colored_polygon(gem, ink)
		canvas.draw_colored_polygon(_inset_toward(gem, center, 0.76), accent)
		var glint: PackedVector2Array = _star_polygon(center + Vector2(0.0, -radius * 0.04), 4, radius * 0.24, radius * 0.07, -PI * 0.5)
		canvas.draw_colored_polygon(glint, Color.WHITE)
	else:
		# Batang Kalyeah: mythic 90s komiks 3-peak street crown + radiant center star!
		var crown_c: Vector2 = center + Vector2(0.0, -radius * 0.62)
		var crown: PackedVector2Array = PackedVector2Array([
			crown_c + Vector2(-radius * 0.54, radius * 0.22),
			crown_c + Vector2(-radius * 0.64, -radius * 0.22),
			crown_c + Vector2(-radius * 0.26, -radius * 0.02),
			crown_c + Vector2(0.0, -radius * 0.36),
			crown_c + Vector2(radius * 0.26, -radius * 0.02),
			crown_c + Vector2(radius * 0.64, -radius * 0.22),
			crown_c + Vector2(radius * 0.54, radius * 0.22),
		])
		canvas.draw_colored_polygon(crown, ink)
		canvas.draw_colored_polygon(_inset_toward(crown, crown_c, 0.76), accent)
		var legend_star: PackedVector2Array = _star_polygon(center + Vector2(0.0, radius * 0.06), 5, radius * 0.44, radius * 0.19, -PI * 0.5)
		canvas.draw_colored_polygon(legend_star, ink)
		canvas.draw_colored_polygon(_inset_toward(legend_star, center + Vector2(0.0, radius * 0.06), 0.76), accent)


static func _shield_polygon(center: Vector2, r: float) -> PackedVector2Array:
	return PackedVector2Array([
		center + Vector2(0.0, -r * 0.96),
		center + Vector2(r * 0.78, -r * 0.66),
		center + Vector2(r * 0.84, r * 0.04),
		center + Vector2(r * 0.5, r * 0.62),
		center + Vector2(0.0, r * 1.0),
		center + Vector2(-r * 0.5, r * 0.62),
		center + Vector2(-r * 0.84, r * 0.04),
		center + Vector2(-r * 0.78, -r * 0.66),
	])


static func _diamond_polygon(center: Vector2, r: float) -> PackedVector2Array:
	return PackedVector2Array([
		center + Vector2(0.0, -r * 1.02),
		center + Vector2(r * 0.76, -r * 0.26),
		center + Vector2(r * 0.58, r * 0.5),
		center + Vector2(0.0, r * 1.02),
		center + Vector2(-r * 0.58, r * 0.5),
		center + Vector2(-r * 0.76, -r * 0.26),
	])


static func _chevron_polygon(center: Vector2, half_w: float, thickness: float) -> PackedVector2Array:
	var drop: float = half_w * 0.45
	return PackedVector2Array([
		center + Vector2(-half_w, -thickness),
		center + Vector2(0.0, drop - thickness),
		center + Vector2(half_w, -thickness),
		center + Vector2(half_w, thickness),
		center + Vector2(0.0, drop + thickness),
		center + Vector2(-half_w, thickness),
	])


static func _star_polygon(center: Vector2, points: int, outer_r: float, inner_r: float, start_angle: float) -> PackedVector2Array:
	var poly: PackedVector2Array = PackedVector2Array()
	var total: int = maxi(3, points) * 2
	poly.resize(total)
	var step: float = TAU / float(total)
	for i: int in total:
		var angle: float = start_angle + step * float(i)
		var rad: float = outer_r if (i % 2) == 0 else inner_r
		poly[i] = center + Vector2(cos(angle), sin(angle)) * rad
	return poly


static func _inset_toward(poly: PackedVector2Array, pivot: Vector2, scale_factor: float) -> PackedVector2Array:
	var out: PackedVector2Array = PackedVector2Array()
	out.resize(poly.size())
	for i: int in poly.size():
		out[i] = pivot.lerp(poly[i], scale_factor)
	return out


static func _offset_poly(poly: PackedVector2Array, offset: Vector2) -> PackedVector2Array:
	var out: PackedVector2Array = PackedVector2Array()
	out.resize(poly.size())
	for i: int in poly.size():
		out[i] = poly[i] + offset
	return out
