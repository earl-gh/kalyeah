extends GutTest
## The scissors' look and motion (docs/ART.md "Skill effects", gunting): silver blades with red handles; the small snip's volley (each
## scissors above the caster's head, a little behind it, over a shoulder, right then left alternating, flying forward closing, arriving
## shut); the big cut (three times bigger, the caster at the screw, at the waist). View only: the sim's numbers are not touched.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const PICTURE: String = "res://assets/models/weapons/scissor_prop_base_color.jpg"
const DT: float = 1.0 / 30.0
const ORIGIN: Vector2 = Vector2(0.0, 8.0)
const AIMS: Array[Vector2] = [Vector2(0.0, -1.0), Vector2(-1.0, 0.0), Vector2(0.0, 1.0), Vector2(1.0, 0.0)]

var _rules: GameRules
var _motion: bool
var _sim: MatchSim
var _fx: WeaponFxView


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false
	_sim = MatchSim.new(_rules, LAYOUT, 3)
	_sim.add_player(1, 0)
	_sim.add_player(2, 1)
	_sim.players[1].position = ORIGIN
	_sim.players[2].position = Vector2(10.0, -20.0)
	_sim.phase = MatchSim.Phase.PLAYING
	_fx = WeaponFxView.new()
	add_child_autofree(_fx)
	_fx.view_scale = 2.0
	_fx.local_team = 0
	_fx.watch(_sim)


func after_each() -> void:
	Settings.reduced_motion = _motion


func _frame() -> void:
	_sim.step(DT)
	_fx.sync(_sim, DT)


## A volley of `n` small scissors started by hand (the view settles n from the hits; here it is set directly) and posed once.
func _volley(n: int, aim: Vector2) -> FxSnips.Snip:
	var snip: FxSnips.Snip = _fx.cones.snips.start(1, _sim.weapon_defs[&"gunting_light"], ORIGIN, aim)
	snip.n = n
	_fx.sync(_sim, DT)
	return snip


func _tall() -> float:
	return KidModel.KID_HEIGHT * _fx.view_scale


# ---- silver blades ----------------------------------------------------------------------------------------------------------

func _luma(c: Color) -> float:
	return 0.299 * c.r + 0.587 * c.g + 0.114 * c.b


## The shader's rule: a handle texel is clearly redder than it is green or blue (scissors_steel.gdshaderinc).
func _redness(c: Color) -> float:
	return c.r - maxf(c.g, c.b)


func test_the_picture_separates_into_dark_blades_and_red_handles() -> void:
	var picture: Image = Image.new()
	assert_eq(picture.load_jpg_from_buffer(FileAccess.get_file_as_bytes(PICTURE)), OK, "the owner's picture is there, untouched")
	var handles: int = 0
	var blades: int = 0
	var between: int = 0
	var dark_blades: int = 0
	for y: int in range(0, picture.get_height(), 2):
		for x: int in range(0, picture.get_width(), 2):
			var c: Color = picture.get_pixel(x, y)
			var red: float = _redness(c)
			if red >= 0.25:
				handles += 1
			elif red < 0.08:
				blades += 1
				dark_blades += 1 if _luma(c) < 0.5 else 0
			else:
				between += 1
	var total: float = float(handles + blades + between)
	assert_gt(float(handles) / total, 0.1, "the red handles are a good part of the picture")
	assert_gt(float(blades) / total, 0.4, "and everything else (blades, screw, empty space) is grey")
	assert_lt(float(between) / total, 0.05, "the red / grey split is clean: few texels fall in the soft band")
	assert_gt(float(dark_blades) / float(blades), 0.9, "the grey texels are the black blades (the screw and the empty gaps are few)")


func _include_colour(name: String) -> Color:
	var code: String = (load("res://scripts/view/art/scissors_steel.gdshaderinc") as ShaderInclude).code
	var found: RegExMatch = RegEx.create_from_string("const vec3 %s = vec3\\(([^)]+)\\);" % name).search(code)
	assert_not_null(found, "%s is a constant of the steel include" % name)
	if found == null:
		return Color.BLACK
	var parts: PackedStringArray = found.get_string(1).split(",")
	if parts.size() == 1:
		return Color(parts[0].to_float(), parts[0].to_float(), parts[0].to_float())
	return Color(parts[0].to_float(), parts[1].to_float(), parts[2].to_float())


func test_the_steel_palette_is_pale_blue_grey_with_a_deeper_rim() -> void:
	var light: Color = _include_colour("STEEL_LIGHT")
	var dark: Color = _include_colour("STEEL_DARK")
	var rim: Color = _include_colour("STEEL_RIM")
	for steel: Color in [light, dark, rim]:
		assert_gt(_luma(steel), 0.3, "never black")
		assert_gt(steel.b, steel.r, "blue-grey, not neutral")
	assert_gt(_luma(light), 0.7, "the edge is pale silver")
	assert_gt(_luma(light), _luma(dark), "darker toward the screw")
	assert_gt(_luma(dark), _luma(rim), "and a deeper rim")
	assert_eq(_include_colour("STEEL_SHINE"), Color.WHITE, "one white highlight streak")


func test_the_rig_and_the_static_model_both_wear_the_silver() -> void:
	for team: Color in [Color.TRANSPARENT, Color(0.2, 0.5, 1.0)]:
		var rig: ScissorsRig = autofree(ScissorsRig.make(1.0, team)) as ScissorsRig
		for blade: MeshInstance3D in rig.blades():
			var look: ShaderMaterial = blade.get_surface_override_material(0) as ShaderMaterial
			assert_not_null(look, "a shader look, not the black picture (team %s)" % str(team))
			var silver: bool = look.shader == ScissorsRig.STEEL_SHADER or look.get_shader_parameter(&"silver") == 1.0
			assert_true(silver, "the blades are painted silver (team %s)" % str(team))
	var model: Node3D = autofree(WeaponModel.scissors(1.0)) as Node3D
	var mesh: MeshInstance3D = model.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
	var look: ShaderMaterial = mesh.get_surface_override_material(0) as ShaderMaterial
	assert_eq(look.get_shader_parameter(&"silver"), 1.0, "the static model is silver too")
	assert_eq((look.next_pass as ShaderMaterial).get_shader_parameter(&"silver"), 1.0, "and its contour is a deep steel, not black")
	var other: Node3D = autofree(WeaponModel.meteor()) as Node3D
	var rock: ShaderMaterial = (other.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D).get_surface_override_material(0) as ShaderMaterial
	assert_eq(rock.get_shader_parameter(&"silver"), null, "no other model is touched")


# ---- the small snip: a volley from over the shoulders -----------------------------------------------------------------

func test_each_small_scissors_starts_above_the_head_behind_on_a_shoulder_right_first_then_alternating() -> void:
	for aim: Vector2 in AIMS:
		for n: int in range(1, 7):
			_before_volley()
			var direction: Vector3 = Vector3(aim.x, 0.0, aim.y)
			var right: Vector3 = direction.cross(Vector3.UP).normalized()
			var snip: FxSnips.Snip = _volley(n, aim)
			assert_eq(snip.parts.size(), n, "one scissors per snip (aim %s, %d)" % [aim, n])
			for k: int in n:
				var part: FxSnips.Part = snip.parts[k]
				var offset: Vector3 = part.from - Vector3(ORIGIN.x, 0.0, ORIGIN.y)
				assert_eq(part.side, 1 if k % 2 == 0 else -1, "right, left, right...: scissors %d (aim %s)" % [k, aim])
				assert_almost_eq(offset.dot(right), FxSnips.SNIP_SHOULDER * float(part.side), 0.001, "on the %s shoulder line (aim %s, scissors %d)" % ["right" if part.side > 0 else "left", aim, k])
				assert_almost_eq(offset.dot(direction), -FxSnips.SNIP_BEHIND, 0.001, "slightly behind the caster (aim %s)" % aim)
				assert_gt(offset.y, _tall(), "above the head (aim %s)" % aim)
			if n > 1:
				assert_ne(snip.parts[0].from, snip.parts[1].from, "the next starts on the other side")


## Fresh view and sim for the next volley of the sweep (the volleys of one test must not share a pool).
func _before_volley() -> void:
	if _fx != null and is_instance_valid(_fx):
		_fx.queue_free()
	_fx = WeaponFxView.new()
	add_child_autofree(_fx)
	_fx.view_scale = 2.0
	_fx.local_team = 0
	_fx.watch(_sim)


func test_a_small_scissors_flies_forward_closing_and_arrives_shut_on_time() -> void:
	var def: WeaponDef = _sim.weapon_defs[&"gunting_light"]
	var snip: FxSnips.Snip = _volley(3, Vector2(0.0, -1.0))
	var period: int = FxMotion.snip_period(3)
	var arrival: Array[int] = [-1, -1, -1]
	var last_angle: Array[float] = [INF, INF, INF]
	for frame: int in range(1, FxMotion.snip_frames(3) + 3):
		_fx.sync(_sim, DT)
		for k: int in 3:
			var part: FxSnips.Part = snip.parts[k]
			var local: int = frame - part.launch
			if local < 0 or not part.holder.visible:
				continue
			assert_lte(part.rig.angle(), last_angle[k], "scissors %d only closes while it travels (frame %d)" % [k, frame])
			last_angle[k] = part.rig.angle()
			if local < FxMotion.SNIP_FLIGHT_FRAMES:
				assert_gt(part.rig.angle(), ScissorsRig.CLOSED, "still open on the way")
				var ahead: float = (part.holder.position - part.from).dot(snip.aim)
				assert_gte(ahead, -0.001, "moving forward")
			elif arrival[k] < 0:
				arrival[k] = frame
				assert_almost_eq(part.rig.angle(), ScissorsRig.CLOSED, 0.001, "arrives shut")
				assert_true(part.holder.position.is_equal_approx(part.to), "at the strike point")
	for k: int in 3:
		assert_eq(arrival[k], k * period + FxMotion.SNIP_FLIGHT_FRAMES, "scissors %d arrives %d frames after its launch" % [k, FxMotion.SNIP_FLIGHT_FRAMES])
	assert_eq(def.delay, 0.0, "the sim's small snip hits at the cast: the view's flight is the shortest it can be and no scissors waits longer than the volley")
	assert_lte(float(FxMotion.snip_frames(def.snips_max)) * DT, 0.65, "a six-snip volley is over inside the wedge's life")


func test_the_cut_flies_from_the_shoulder_to_the_aim_line() -> void:
	var snip: FxSnips.Snip = _volley(2, Vector2(0.0, -1.0))
	var right: FxSnips.Part = snip.parts[0]
	var left: FxSnips.Part = snip.parts[1]
	assert_gt(right.from.x, ORIGIN.x, "the first scissors comes from the right (east when facing north)")
	assert_lt(left.from.x, ORIGIN.x, "the next from the left")
	assert_almost_eq(right.to.x, ORIGIN.x, 0.001, "both land on the aim line")
	assert_almost_eq(left.to.x, ORIGIN.x, 0.001)
	assert_lt(right.to.y, right.from.y, "descending onto the target")
	assert_lt(right.to.z, right.from.z, "and forward (north is -Z)")


func test_reduced_motion_shows_every_small_scissors_shut_and_still() -> void:
	Settings.reduced_motion = true
	var snip: FxSnips.Snip = _volley(3, Vector2(0.0, -1.0))
	var seen: Array[Vector3] = []
	for part: FxSnips.Part in snip.parts:
		assert_true(part.holder.visible)
		assert_eq(part.rig.angle(), ScissorsRig.CLOSED, "shut")
		for other: Vector3 in seen:
			assert_gt(other.distance_to(part.holder.position), 0.2, "side by side, not on top of each other")
		seen.append(part.holder.position)
	for i: int in 4:
		_frame()
		for k: int in snip.parts.size():
			assert_eq(snip.parts[k].holder.position, seen[k], "no flight")
	assert_eq(_fx.context().ground.live_smears(), 0, "no arc")


# ---- the big cut ------------------------------------------------------------------------------------------------------------

func _heavy(aim: Vector2) -> FxSnips.Part:
	var def: WeaponDef = _sim.weapon_defs[&"gunting_heavy"]
	var snip: FxSnips.Snip = _fx.cones.snips.start(1, def, ORIGIN, aim)
	_fx.sync(_sim, DT)
	return snip.parts[0]


func test_the_big_cut_is_three_times_bigger() -> void:
	var heavy: WeaponDef = _sim.weapon_defs[&"gunting_heavy"]
	var part: FxSnips.Part = _heavy(Vector2(0.0, -1.0))
	var model: Node3D = part.rig.get_node("Model") as Node3D
	var length: float = model.scale.y * WeaponModel.size_of(ScissorsRig.MODEL).y
	assert_almost_eq(length, FxSnips.SNIP_LEN_HEAVY * 3.0, 0.001, "3x the old 2.2 m")
	assert_almost_eq(length, FxSnips.length_of(heavy), 0.001)
	assert_gt(length, 3.0 * FxSnips.SNIP_LEN, "and far bigger than a small scissors")


func test_the_caster_stands_at_the_screw_at_waist_height_whatever_the_aim() -> void:
	for aim: Vector2 in AIMS:
		_before_volley()
		var part: FxSnips.Part = _heavy(aim)
		var screw: Vector3 = (part.rig.blades()[0].get_parent() as Node3D).global_position
		assert_almost_eq(screw.x, ORIGIN.x, 0.001, "the screw is on the caster (x, aim %s)" % aim)
		assert_almost_eq(screw.z, ORIGIN.y, 0.001, "the screw is on the caster (z, aim %s)" % aim)
		assert_almost_eq(screw.y, FxSnips.HEAVY_WAIST * _tall(), 0.001, "at waist height (aim %s)" % aim)
		var blades: Vector3 = (part.rig.blades()[0].get_parent() as Node3D).global_basis * Vector3(0.0, 1.0, 0.0)
		assert_almost_eq(blades.normalized().dot(Vector3(aim.x, 0.0, aim.y)), 1.0, 0.001, "the blades reach out along the aim (%s)" % aim)


func test_the_big_cut_keeps_its_beats_and_the_tips_land_in_the_sim_range() -> void:
	var heavy: WeaponDef = _sim.weapon_defs[&"gunting_heavy"]
	var tips: float = ScissorsRig.tip_reach(FxSnips.length_of(heavy))
	assert_lte(tips + FxSnips.lunge_of(heavy), heavy.max_range + 0.001, "the arc radius (tips + lunge) is the sim's range at most")
	var part: FxSnips.Part = _heavy(Vector2(0.0, -1.0))
	var start: Vector3 = part.holder.position
	var pulled: float = 0.0
	for i: int in 8:
		_frame()
		pulled = maxf(pulled, start.distance_to(part.holder.position))
	assert_gt(pulled, 0.1, "the wind-up pulls back")
	assert_eq(_sim.weapon_defs[&"gunting_heavy"].delay, heavy.delay, "the sim's delay is the view's wind-up")
