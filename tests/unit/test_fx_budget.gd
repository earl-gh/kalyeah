extends GutTest
## The restraint budget of the skill effects (docs/ART.md "Skill effects"): a six-kid brawl casting every weapon stays inside
## the caps (particles, emitters, bursts, decals, shadows, visible effect nodes), makes no material or model look after its
## warm-up, and every effect is over shortly after its sim object.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0
const ALL: Array[StringName] = [&"bato_light", &"bato_heavy", &"gunting_light", &"gunting_heavy", &"papel_trap", &"papel_shield", &"tsinelas_light", &"tsinelas_heavy", &"lata", &"jacks", &"bola", &"trumpo", &"heal", &"langit_lupa"]
## Visible effect nodes (every mesh and particle emitter under the view, the weapon models included) at most in a deliberately
## overloaded brawl: six kids casting a card every half second with no cooldown or elixir (about ten times what elixir
## allows). A headless proxy for draw calls; the phone target of 40 effect draws in a normal brawl is checked on a device.
## 100, was 96 (a jacks field is six clusters now, three more weapon-model nodes at the overloaded peak; the 64 non-model nodes are unchanged); 96, was 92 (4 more soil-crumb emitters, each a visible node while its crumbs live ~1 s); 92, was 90: dust, impact puffs and hit stars now live 0.5 / 0.5 / 0.4 s (were 0.4 / 0.45 / 0.3) so the bigger puffs are seen long
## enough to read, which keeps one or two more emitters visible at the overloaded peak (the 60 non-model nodes are unchanged in kind).
const VISIBLE_CAP: int = 100
## The audit's budget per live effect (a sim object, an effect playing its end, a hit burst): at most this many effect
## draws on average (its ground mark, its decal or shadow, its particles; the object itself is the weapon model).
const PER_EFFECT: float = 3.0

var _rules: GameRules
var _motion: bool
var _sim: MatchSim
var _fx: WeaponFxView


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false


func after_each() -> void:
	Settings.reduced_motion = _motion


func _brawl() -> void:
	_sim = MatchSim.new(_rules, LAYOUT, 7)
	for i: int in 6:
		var id: int = i + 1
		_sim.add_player(id, i % 2)
		_sim.players[id].position = Vector2(float(i / 2) * 2.5 - 2.5, 7.0 if i % 2 == 0 else 3.0)
		_sim.players[id].facing = Vector2(0.0, -1.0) if i % 2 == 0 else Vector2(0.0, 1.0)
	_sim.phase = MatchSim.Phase.PLAYING
	_fx = WeaponFxView.new()
	add_child_autofree(_fx)
	_fx.view_scale = 2.0
	_fx.watch(_sim)


## Kid `id` casts `weapon` at the nearest enemy (or a friend for the heal), like a player would.
func _cast(id: int, weapon: StringName) -> void:
	var caster: PlayerState = _sim.players[id]
	if caster.tagged:
		return
	var def: WeaponDef = _sim.weapon_defs[weapon]
	var target: PlayerState = _sim.nearest_enemy(caster, 30.0)
	var aim: Vector2 = caster.facing
	if target != null:
		aim = ((target.position - caster.position) / maxf(def.max_range, 0.01)).limit_length(1.0)
	if def.shape == WeaponDef.Shape.HEAL:
		aim = Vector2.ZERO
	if _sim.weapons.fire(_sim, caster, def, aim, 1.0, target.id if target != null else -1):
		_sim.weapon_cast.emit(id, weapon)


## Effects playing right now: sim objects, ends playing, hit bursts.
func _live_effects() -> int:
	return _fx.live_count() + _fx.fading_count() + _fx.burst_count() + _fx.windup_count()


## Visible effect nodes under the view: [without the weapon models, with them].
func _visible_nodes() -> Vector2i:
	var effects: int = 0
	var models: int = 0
	for node: Node in _fx.find_children("*", "GeometryInstance3D", true, false):
		if (node as GeometryInstance3D).is_visible_in_tree() and not _gone(node):
			if node.is_in_group(WeaponModel.GROUP):
				models += 1
			else:
				effects += 1
	return Vector2i(effects, effects + models)


## True when `node` or a parent was freed this frame (this test runs many sim frames inside one engine frame, so freed nodes
## are still in the tree until it ends).
func _gone(node: Node) -> bool:
	var at: Node = node
	while at != null:
		if at.is_queued_for_deletion():
			return true
		at = at.get_parent()
	return false


func test_a_six_kid_brawl_stays_in_budget() -> void:
	_brawl()
	var ctx: FxContext = _fx.context()
	var materials: int = -1
	var looks: int = -1
	var most_visible: Vector2i = Vector2i.ZERO
	for frame: int in 120:
		if frame % 15 == 0:
			for i: int in 6:
				_cast(i + 1, ALL[(frame / 15 * 6 + i) % ALL.size()])
				_sim.players[i + 1].hp = _sim.rules.player_max_hp
		_sim.step(DT)
		_fx.sync(_sim, DT)
		assert_lte(ctx.particles.live_particles(), FxParticles.LIVE_CAP, "particles at frame %d" % frame)
		assert_lte(ctx.particles.busy_emitters(), 40)
		assert_lte(_fx.burst_count(), FxBursts.MAX_LIVE, "hit bursts")
		assert_lte(ctx.ground.live_rings(), FxGround.RINGS)
		assert_lte(ctx.ground.live_smears(), FxGround.SMEARS)
		assert_lte(ctx.ground.live_flashes(), FxGround.FLASHES)
		assert_lte(ctx.ground.live_cracks(), FxGround.CRACKS)
		assert_lte(ctx.ground.live_fades(), FxGround.FADES)
		assert_lte(ctx.shadows.live(), FxShadows.SLOTS)
		var visible: Vector2i = _visible_nodes()
		most_visible = Vector2i(maxi(most_visible.x, visible.x), maxi(most_visible.y, visible.y))
		if frame == 45:
			materials = FxKit.material_count()
			looks = WeaponModel.look_count()
	assert_lte(most_visible.y, VISIBLE_CAP, "visible effect nodes in an overloaded six-kid brawl (most %d with the weapon models, %d without)" % [most_visible.y, most_visible.x])
	gut.p("overloaded six-kid brawl: at most %d visible effect nodes, %d with the weapon models" % [most_visible.x, most_visible.y])
	assert_eq(FxKit.material_count(), materials, "no effect material made after the warm-up")
	assert_eq(WeaponModel.look_count(), looks, "no model look made after the warm-up")
	assert_lte(WeaponModel.look_count(), 20)


func test_an_opening_brawl_keeps_three_draws_per_effect() -> void:
	_brawl()
	var most: Vector2i = Vector2i.ZERO
	var draws: int = 0
	var effects: int = 0
	for frame: int in 120:
		# every kid's opening: three cards half a second apart (then elixir runs dry)
		if frame % 15 == 0 and frame <= 30:
			for i: int in 6:
				_cast(i + 1, ALL[(frame / 15 * 6 + i * 2) % ALL.size()])
		_sim.step(DT)
		_fx.sync(_sim, DT)
		var visible: Vector2i = _visible_nodes()
		most = Vector2i(maxi(most.x, visible.x), maxi(most.y, visible.y))
		draws += visible.x
		effects += _live_effects()
	var average: float = float(draws) / float(maxi(effects, 1))
	assert_lte(average, PER_EFFECT, "about three effect draws per live effect at most (%.2f on average)" % average)
	gut.p("opening six-kid brawl (18 cards in 1 s): at most %d visible effect nodes, %d with the weapon models; %.2f per live effect" % [most.x, most.y, average])


func test_the_pools_are_built_within_the_material_budget() -> void:
	var before: int = FxKit.material_count()
	var fx: WeaponFxView = WeaponFxView.new()
	add_child_autofree(fx)
	assert_lte(FxKit.material_count() - before, 96, "one view's pools make at most 96 materials, once")


func test_every_effect_ends() -> void:
	_brawl()
	for frame: int in 45:
		if frame % 15 == 0:
			for i: int in 6:
				_cast(i + 1, ALL[(frame / 15 * 6 + i) % ALL.size()])
		_sim.step(DT)
		_fx.sync(_sim, DT)
	# everything leaves the sim at once (a point ends)
	_sim.weapons.reset()
	for frame: int in 18:
		_sim.step(DT)
		_fx.sync(_sim, DT)
	assert_eq(_fx.live_count(), 0, "nothing drawn for a sim object that is gone")
	assert_eq(_fx.fading_count(), 0, "every end is over within 0.6 s")
	# the soil crumbs live 0.9 s (they stay whole, then fade), so the last puffs are over a second after the end
	for frame: int in 14:
		_sim.step(DT)
		_fx.sync(_sim, DT)
	assert_eq(_fx.particle_count(), 0, "and every puff within a second")
	assert_eq(_fx.windup_count(), 0)
