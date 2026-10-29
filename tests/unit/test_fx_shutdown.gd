extends GutTest
## When the last effect view leaves the tree (a match ends), every static cache of the effects is emptied: nothing keeps
## meshes, materials or model scenes alive in the menus, and the game exits without "resources still in use". A new view
## builds everything again and plays.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _motion: bool


func before_each() -> void:
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false


func after_each() -> void:
	Settings.reduced_motion = _motion


func _play(fx: WeaponFxView) -> MatchSim:
	var sim: MatchSim = MatchSim.new(load("res://data/rules/game_rules.tres") as GameRules, LAYOUT, 3)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.players[1].position = Vector2(0.0, 8.0)
	sim.players[2].position = Vector2(0.0, 5.0)
	sim.phase = MatchSim.Phase.PLAYING
	fx.watch(sim)
	for weapon: StringName in [&"bato_heavy", &"gunting_light", &"bola"]:
		assert_true(sim.weapons.fire(sim, sim.players[1], sim.weapon_defs[weapon], Vector2(0.0, -0.4), 0.0, 2))
		sim.weapon_cast.emit(1, weapon)
	for i: int in 6:
		sim.step(DT)
		fx.sync(sim, DT)
	return sim


func _caches_empty() -> bool:
	return FxKit.cached_count() == 0 and FxGround.mark_count() == 0 and FxParticles.spec_count() == 0 \
		and FxShadows.cached_count() == 0 and WeaponModel.look_count() == 0 and WeaponModel.cached_count() == 0 \
		and ScissorsRig.cached_count() == 0


func test_the_last_view_leaving_empties_every_cache() -> void:
	var fx: WeaponFxView = WeaponFxView.new()
	add_child(fx)
	_play(fx)
	assert_false(_caches_empty(), "while a match plays the caches are full")
	var context: WeakRef = weakref(fx.context())
	remove_child(fx)
	fx.free()
	assert_null(context.get_ref(), "the view's context (and the sim it reads) is freed with it: no reference loop")
	assert_true(_caches_empty(), "the last view gone: every cache is empty (FxKit %d, marks %d, specs %d, looks %d, models %d, scissors %d)" % [FxKit.cached_count(), FxGround.mark_count(), FxParticles.spec_count(), WeaponModel.look_count(), WeaponModel.cached_count(), ScissorsRig.cached_count()])
	var again: WeaponFxView = WeaponFxView.new()
	add_child_autofree(again)
	var sim: MatchSim = _play(again)
	assert_gt(again.live_count(), 0, "a new view builds everything again and draws the casts")
	assert_gt(again.cones.scissors().size(), 0)
	assert_true(sim != null)


func test_a_second_view_keeps_the_caches_while_the_first_goes() -> void:
	var first: WeaponFxView = WeaponFxView.new()
	var second: WeaponFxView = WeaponFxView.new()
	add_child(first)
	add_child_autofree(second)
	_play(first)
	remove_child(first)
	first.free()
	assert_false(_caches_empty(), "another view still plays: nothing is cleared under it")
	_play(second)
	assert_gt(second.live_count(), 0)
