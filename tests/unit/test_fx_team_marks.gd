extends GutTest
## An effect says who threw it from its first frame (docs/ART.md): landing circles and the cone flash are drawn in the thrower's team
## colour with an opaque rim, both sides visible. Colour-blind colours keep you, your team and theirs apart.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
## Vertex colours are stored with 8 bits a channel.
const QUANTUM: float = 1.5 / 255.0

var _rules: GameRules
var _saved: bool


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_saved = Settings.colorblind
	Settings.set_colorblind(false)


func after_each() -> void:
	Settings.set_colorblind(_saved)


func _fx() -> WeaponFxView:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 3)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	var fx: WeaponFxView = WeaponFxView.new()
	add_child_autofree(fx)
	fx.local_team = 0
	fx.watch(sim)
	return fx


## The colours the effect's mesh is drawn with (vertex colours, alpha dropped).
func _colors(node: MeshInstance3D) -> Array[Color]:
	var arrays: Array = node.mesh.surface_get_arrays(0)
	var found: Array[Color] = []
	for color: Color in arrays[Mesh.ARRAY_COLOR] as PackedColorArray:
		var opaque: Color = Color(color, 1.0)
		if not found.has(opaque):
			found.append(opaque)
	return found


## Every colour of `node` is `expected` (within the 8-bit rounding).
func _all_near(node: MeshInstance3D, expected: Color) -> bool:
	var colors: Array[Color] = _colors(node)
	if colors.is_empty():
		return false
	for color: Color in colors:
		if absf(color.r - expected.r) > QUANTUM or absf(color.g - expected.g) > QUANTUM or absf(color.b - expected.b) > QUANTUM:
			return false
	return true


func _landing(fx: WeaponFxView, team: int) -> MeshInstance3D:
	var zone: WeaponSystem.Zone = WeaponSystem.Zone.new()
	zone.team = team
	zone.kind = WeaponSystem.ZoneKind.EXPLOSION
	zone.def = _rules.weapons.filter(func(d: WeaponDef) -> bool: return d.id == &"lata")[0] as WeaponDef
	zone.radius = 2.0
	return fx._zone_node(zone) as MeshInstance3D


func _rim_alpha(node: MeshInstance3D) -> float:
	var alpha: float = 0.0
	for color: Color in node.mesh.surface_get_arrays(0)[Mesh.ARRAY_COLOR] as PackedColorArray:
		alpha = maxf(alpha, color.a)
	return alpha


func test_a_landing_circle_is_in_the_throwers_team_colour() -> void:
	var fx: WeaponFxView = _fx()
	var mine: MeshInstance3D = autofree(_landing(fx, 0)) as MeshInstance3D
	var theirs: MeshInstance3D = autofree(_landing(fx, 1)) as MeshInstance3D
	assert_true(_all_near(mine, TeamColors.world_own()), "yours: your colour %s" % str(_colors(mine)))
	assert_true(_all_near(theirs, TeamColors.world_enemy()), "theirs: their colour %s" % str(_colors(theirs)))
	assert_almost_eq(_rim_alpha(theirs), WeaponFxView.FX_RIM_ALPHA, QUANTUM, "an opaque rim reads on the grey street at once")


func test_the_cone_flash_is_whole_from_its_first_frame_and_says_who_threw_it() -> void:
	var fx: WeaponFxView = _fx()
	var snip: WeaponDef = _rules.weapons.filter(func(d: WeaponDef) -> bool: return d.id == &"gunting_light")[0] as WeaponDef
	fx._on_cone_struck(2, snip, Vector2.ZERO, Vector2.UP)
	var flash: MeshInstance3D = fx._flashes.back() as MeshInstance3D
	assert_true(_all_near(flash, TeamColors.world_enemy()), "an enemy's snip is in their colour %s" % str(_colors(flash)))
	var material: BaseMaterial3D = flash.material_override as BaseMaterial3D
	assert_eq(material.cull_mode, BaseMaterial3D.CULL_DISABLED, "seen from above, not culled away")
	assert_almost_eq(_rim_alpha(flash), WeaponFxView.FX_RIM_ALPHA, QUANTUM)
	assert_almost_eq(fx._flash_time[flash], WeaponFxView.CONE_FLASH_TIME, 0.001, "it holds its full look from frame 0")


func test_colour_blind_you_are_not_the_enemys_orange() -> void:
	Settings.set_colorblind(true)
	var mine: Color = TeamColors.bar_self()
	assert_lt(mine.s, 0.1, "your bar has no hue of its own")
	assert_gt(mine.get_luminance(), TeamColors.bar_enemy().get_luminance() + 0.3, "and is far brighter than theirs")


func test_colour_blind_base_tints_keep_their_hue() -> void:
	Settings.set_colorblind(true)
	var tinted: Color = TeamColors.world_enemy().lerp(StreetMap.SOFT_GREY, TeamColors.base_soften())
	assert_almost_eq(tinted.h, TeamColors.BLIND_ENEMY.h, 0.02, "still orange, not khaki")
	assert_gt(tinted.s, TeamColors.BLIND_ENEMY.s * 0.8)
	Settings.set_colorblind(false)
	assert_eq(TeamColors.base_soften(), TeamColors.BASE_SOFTEN, "the default street keeps its calm tints")
