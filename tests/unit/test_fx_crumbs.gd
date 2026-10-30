extends GutTest
## The tiny soil crumbs (owner: "tiny soil crumbs that fade out a delay after they appeared") and the heal wings' path (head to above
## the head, popping in then out). Crumbs are a pooled particle preset: a handful of earth-coloured specks that stay whole for the first
## part of their life and then fade; they fall off the soil block as it rises, widens and sinks, where the meteor lands, where the
## spinning top lands and spins, and at each bounce of the marble. Facts, not pixels.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules
var _motion: bool
var _sim: MatchSim
var _fx: WeaponFxView
var _seen: Dictionary[int, bool] = {}


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false
	_seen.clear()


func after_each() -> void:
	Settings.reduced_motion = _motion


func _setup() -> void:
	_sim = MatchSim.new(_rules, LAYOUT, 3)
	_sim.add_player(1, 0)
	_sim.add_player(2, 1)
	_sim.players[1].position = Vector2(0.0, 8.0)
	_sim.players[1].facing = Vector2(0.0, -1.0)
	_sim.players[2].position = Vector2(20.0, 0.0)
	_sim.phase = MatchSim.Phase.PLAYING
	_fx = WeaponFxView.new()
	add_child_autofree(_fx)
	_fx.view_scale = 2.0
	_fx.local_team = 0
	_fx.watch(_sim)


func _cast(weapon: StringName, aim: Vector2) -> void:
	assert_true(_sim.weapons.fire(_sim, _sim.players[1], _sim.weapon_defs[weapon], aim, 0.0, 2), "%s is cast" % weapon)
	_sim.weapon_cast.emit(1, weapon)


## One 30 Hz frame; returns the crumb bursts that began in it (emitters that became busy with crumbs).
func _frame() -> Array[CPUParticles3D]:
	_sim.step(DT)
	_fx.sync(_sim, DT)
	var started: Array[CPUParticles3D] = []
	var ramp: Gradient = FxParticles.spec(FxParticles.Preset.CRUMBS).color_ramp
	var now: Dictionary[int, bool] = {}
	for emitter: CPUParticles3D in _fx.context().particles.busy():
		if emitter.color_ramp == ramp:
			now[emitter.get_instance_id()] = true
			if not _seen.has(emitter.get_instance_id()):
				started.append(emitter)
	_seen = now
	return started


## Crumb bursts over `frames` frames: [count, the first burst's emitter, the last burst's emitter].
func _bursts(frames: int) -> Array:
	var total: int = 0
	var first: CPUParticles3D = null
	var last: CPUParticles3D = null
	for i: int in frames:
		var started: Array[CPUParticles3D] = _frame()
		total += started.size()
		if first == null and not started.is_empty():
			first = started[0]
		if not started.is_empty():
			last = started[started.size() - 1]
	return [total, first, last]


func test_the_crumbs_preset_is_tiny_earthy_and_fades_late() -> void:
	var spec: FxParticles.Spec = FxParticles.spec(FxParticles.Preset.CRUMBS)
	assert_gte(spec.size, 0.08, "specks of a few centimetres (asked 8 to 14 cm; 15 to 25 so they show as 3 to 5 px)")
	assert_lte(spec.size_max, 0.25, "none bigger than 25 cm")
	assert_gt(spec.size_max, spec.size, "of different sizes")
	assert_between(spec.lifetime, 0.8, 1.0, "alive about a second")
	assert_between(FxParticles.CRUMB_COUNT, 6, 10, "a handful a burst")
	assert_between(FxParticles.CRUMB_COUNT_SMALL, 6, 10)
	assert_between(FxParticles.CRUMB_FADE_START, 0.35, 0.4, "fully visible for the first 35 to 40 percent of the life")
	var fade: Gradient = spec.color_ramp
	assert_eq(fade.sample(0.0).a, 1.0, "visible when born")
	assert_eq(fade.sample(0.35).a, 1.0, "and still at 35 percent")
	assert_eq(fade.sample(FxParticles.CRUMB_FADE_START).a, 1.0, "and when the fade begins")
	assert_lt(fade.sample(0.7).a, 1.0, "fading after that")
	assert_gt(fade.sample(0.7).a, 0.0)
	assert_eq(fade.sample(1.0).a, 0.0, "gone at the end of its life")
	var tones: Gradient = spec.color_initial_ramp
	for i: int in FxParticles.CRUMB_TONES.size():
		var tone: Color = FxParticles.CRUMB_TONES[i]
		assert_true(tone.r > tone.g and tone.g > tone.b, "tone %d is a brown" % i)
		assert_lt(tone.v, 0.6, "and an earth-dark one")
	assert_true(tones.get_point_count() >= 3, "several browns, a few tones per burst")
	assert_true(spec.spread < 60.0 and spec.speed_min > 0.0 and spec.gravity > 0.0, "tossed out and up, then pulled down")
	assert_eq(spec.color, Color.WHITE, "no team colour: the earth tones are the whole look")
	assert_lte(FxParticles.apex(spec), spec.apex_bound + 0.001)


func test_a_crumb_burst_is_set_up_from_the_preset_and_pooled() -> void:
	_setup()
	var ctx: FxContext = _fx.context()
	var materials: int = FxKit.material_count()
	var children: int = _fx.find_children("*", "Node", true, false).size()
	assert_true(ctx.particles.puff(FxParticles.Preset.CRUMBS, Vector3(1.0, 0.0, 2.0), FxParticles.CRUMB_COUNT))
	var emitter: CPUParticles3D = ctx.particles.busy()[0]
	var spec: FxParticles.Spec = FxParticles.spec(FxParticles.Preset.CRUMBS)
	assert_eq(emitter.amount, FxParticles.CRUMB_COUNT)
	assert_eq(emitter.lifetime, spec.lifetime)
	assert_almost_eq(emitter.scale_amount_min, spec.size, 0.0001)
	assert_almost_eq(emitter.scale_amount_max, spec.size_max, 0.0001)
	assert_eq(emitter.color_ramp, spec.color_ramp, "the fade is the shared ramp")
	assert_eq(emitter.color_initial_ramp, spec.color_initial_ramp)
	assert_true(emitter.one_shot)
	for i: int in 200:
		ctx.time += DT
		ctx.particles.step()
		ctx.particles.puff(FxParticles.Preset.CRUMBS, Vector3(float(i), 0.0, 0.0), FxParticles.CRUMB_COUNT_SMALL)
	assert_eq(FxKit.material_count(), materials, "no material made")
	assert_eq(_fx.find_children("*", "Node", true, false).size(), children, "no node made")
	assert_lte(ctx.particles.live_particles(), FxParticles.LIVE_CAP, "and the cap holds")
	# another puff reuses the emitter and puts the plain presets' look back
	ctx.time += 5.0
	ctx.particles.step()
	assert_true(ctx.particles.puff(FxParticles.Preset.DUST, Vector3.ZERO, FxParticles.CRUMB_COUNT))
	var dust: CPUParticles3D = ctx.particles.busy()[0]
	assert_null(dust.color_ramp, "dust has no crumb fade")
	assert_null(dust.color_initial_ramp)


func test_crumbs_obey_reduced_motion_and_the_cap() -> void:
	_setup()
	var particles: FxParticles = _fx.context().particles
	_fx.context().reduced = true
	assert_false(particles.puff(FxParticles.Preset.CRUMBS, Vector3.ZERO, FxParticles.CRUMB_COUNT), "none with Reduce motion")
	assert_eq(particles.live_particles(), 0)
	_fx.context().reduced = false
	var skipped: int = 0
	for i: int in 40:
		if not particles.puff(FxParticles.Preset.CRUMBS, Vector3(float(i), 0.0, 0.0), FxParticles.CRUMB_COUNT):
			skipped += 1
		assert_lte(particles.live_particles(), FxParticles.LIVE_CAP)
	assert_gt(skipped, 0, "past the pool or the cap a burst is skipped, like any decoration")


func test_a_burst_can_be_spread_along_a_wall_and_turned() -> void:
	_setup()
	var particles: FxParticles = _fx.context().particles
	assert_true(particles.puff(FxParticles.Preset.CRUMBS, Vector3.ZERO, FxParticles.CRUMB_COUNT, Color.WHITE, Vector2(3.6, 0.5), 0.5))
	var emitter: CPUParticles3D = particles.busy()[0]
	assert_almost_eq(emitter.emission_box_extents.x, 1.8, 0.001, "over the block's width")
	assert_almost_eq(emitter.emission_box_extents.z, 0.25, 0.001)
	assert_almost_eq(emitter.rotation.y, 0.5, 0.001, "turned along the wall")


func test_the_soil_block_drops_crumbs_as_it_rises_widens_and_sinks() -> void:
	_setup()
	_cast(&"langit_lupa", Vector2(0.0, -1.0))
	var shield: WeaponSystem.Shield = _sim.weapons.shields[0]
	var rise: Array = _bursts(2)
	assert_eq(rise[0], 1, "a burst as it rises")
	var emitter: CPUParticles3D = rise[1]
	assert_almost_eq(emitter.emission_box_extents.x * 2.0, shield.half_width * 2.0, 0.2, "over its width at that moment")
	var widen: Array = _bursts(int((shield.def.expand_delay + shield.def.expand_time) / DT))
	assert_eq(widen[0], 2, "two while it widens (as it starts and half way)")
	assert_gt((widen[2] as CPUParticles3D).emission_box_extents.x, (widen[1] as CPUParticles3D).emission_box_extents.x * 1.5, "each spread over a wider block than the one before")
	var held: Array = _bursts(int((shield.time_left - FxWalls.SOIL_COLLAPSE_TIME) / DT) - 3)
	assert_eq(held[0], 0, "none while it stands")
	var sink: Array = _bursts(int(FxWalls.SOIL_COLLAPSE_TIME / DT) + 6)
	assert_eq(sink[0], 2, "two as it sinks (when it starts and half way)")
	assert_almost_eq((sink[2] as CPUParticles3D).emission_box_extents.x * 2.0, 3.6, 0.2, "over the full 3.6 m")


func test_the_paper_wall_drops_no_crumbs() -> void:
	_setup()
	_cast(&"papel_shield", Vector2(0.0, -1.0))
	assert_eq(_bursts(80)[0], 0, "paper is not earth")


func test_the_meteor_lands_in_crumbs() -> void:
	_setup()
	_sim.players[2].position = Vector2(0.0, 3.0)
	_cast(&"bato_heavy", Vector2(0.0, -0.6))
	var before: int = 0
	var landed: Array = [0, null]
	for i: int in 100:
		var now: Array = _bursts(1)
		if _sim.weapons.zones.is_empty():
			landed[0] += now[0]
			if landed[1] == null:
				landed[1] = now[1]
		else:
			before += now[0]
	assert_eq(before, 0, "none while it falls")
	assert_gte(landed[0], 1 + FxLandings.METEOR_RIM_SPOTS, "where it lands: the impact point and round the rim")


func test_the_top_drops_crumbs_where_it_lands_and_while_it_spins() -> void:
	_setup()
	_cast(&"trumpo", Vector2(0.0, -1.0))
	var landing: Array = _bursts(4)
	assert_gte(landing[0], 1, "a burst where it lands")
	var trickle: Array = _bursts(30)
	assert_gte(trickle[0], 2, "and a trickle while it spins")
	assert_lt(trickle[0], 8, "kept small")


func test_the_marble_drops_crumbs_at_each_bounce() -> void:
	_setup()
	var def: WeaponDef = _sim.weapon_defs[&"bola"]
	_cast(&"bola", Vector2(0.0, -1.0))
	var bursts: int = 0
	for i: int in 120:
		bursts += _bursts(1)[0]
		if _sim.weapons.projectiles.is_empty() and i > 10:
			break
	assert_gte(bursts, def.count, "a burst for each of its %d bounces" % def.count)


# ---- the heal wings ---------------------------------------------------------------------------------------------------

func test_the_heal_wings_pop_in_hold_and_pop_out_to_exactly_nothing() -> void:
	assert_almost_eq(FxHeals.cross_size(0.0), 0.0, 0.000001, "born at nothing")
	var peak: float = 0.0
	for i: int in 20:
		peak = maxf(peak, FxHeals.cross_size(FxHeals.CROSS_POP_IN * float(i) / 19.0))
	assert_gt(peak, 1.0, "the pop-in grows a little past full size")
	assert_almost_eq(FxHeals.cross_size(FxHeals.CROSS_POP_IN), 1.0, 0.0001, "then settles at full size")
	assert_eq(FxHeals.cross_size((FxHeals.CROSS_POP_IN + FxHeals.CROSS_POP_OUT_AT) / 2.0), 1.0, "and holds a moment")
	var wound: float = 0.0
	for i: int in 20:
		wound = maxf(wound, FxHeals.cross_size(FxHeals.CROSS_POP_OUT_AT + (FxHeals.CROSS_END - FxHeals.CROSS_POP_OUT_AT) * float(i) / 19.0))
	assert_gt(wound, 1.0, "the pop-out winds up first")
	assert_lt(FxHeals.cross_size(FxHeals.CROSS_END - DT), 0.5, "then shrinks away")
	assert_almost_eq(FxHeals.cross_size(FxHeals.CROSS_END), 0.0, 0.000001, "to nothing as it ends")
	assert_between(FxHeals.CROSS_END, 0.9, 1.1, "about a second in all")


func test_the_heal_wings_rise_from_the_head_to_above_it() -> void:
	assert_eq(FxHeals.cross_share(0.0), FxHeals.CROSS_SCREEN_FROM, "they start at the head")
	assert_lt(FxHeals.CROSS_SCREEN_FROM, 1.0, "which is under the crown")
	assert_eq(FxHeals.cross_share(FxHeals.CROSS_RISE_TIME), FxHeals.CROSS_SCREEN_TO, "and rise to above the head")
	assert_gt(FxHeals.CROSS_SCREEN_TO, 1.4, "clear of the crown and the name bar over it")
	assert_gt(FxHeals.cross_share(FxHeals.CROSS_RISE_TIME / 2.0), (FxHeals.CROSS_SCREEN_FROM + FxHeals.CROSS_SCREEN_TO) / 2.0, "fast first (ease-out)")
	var last: float = 0.0
	for i: int in 40:
		var share: float = FxHeals.cross_share(float(i) * DT)
		assert_gte(share, last)
		last = share
	assert_eq(FxHeals.cross_share(5.0), FxHeals.CROSS_SCREEN_TO, "it stays up there")


func test_the_heal_wings_with_reduced_motion_stand_above_the_head_and_go() -> void:
	Settings.reduced_motion = true
	_setup()
	_sim.damage(1, 40)
	_cast(&"heal", Vector2.ZERO)
	_fx.sync(_sim, DT)
	var crosses: Array[Node3D] = _fx.heals.crosses()
	assert_eq(crosses.size(), 1)
	var at: float = crosses[0].position.y
	var size: Vector3 = crosses[0].scale
	for i: int in 10:
		_frame()
		assert_eq(crosses[0].position.y, at, "no rise")
		assert_eq(crosses[0].scale, size, "no pop")
	for i: int in 12:
		_frame()
	assert_eq(_fx.heals.crosses().size(), 0, "gone after its hold")
	Settings.reduced_motion = false
	_setup()
	_sim.damage(1, 40)
	_cast(&"heal", Vector2.ZERO)
	_fx.sync(_sim, DT)
	var start: float = _fx.heals.crosses()[0].position.y
	for i: int in 14:
		_frame()
	assert_gt(_fx.heals.crosses()[0].position.y, start + 0.5, "with motion it rises (reduced stands at the top)")
	assert_almost_eq(at, _fx.heals.crosses()[0].position.y, 0.05, "to where the reduced one stands")
	for i: int in 25:
		_frame()
	assert_eq(_fx.heals.crosses().size(), 0, "and it is gone by its end")
