extends GutTest
## The visual pass on the match and menus (no rule changes): a team ring under every kid, readable bases, chunky bars,
## impacts and banners, a bevelled tray and buttons that keep their size, the teammates while you are tagged, a grouped
## results card and the kid screen's stage and name.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const MAP_SCENE: PackedScene = preload("res://scenes/map/street_map.tscn")
const DT: float = 1.0 / 30.0

var _loadout: Array[StringName]
var _motion: bool
var _blind: bool


func before_each() -> void:
	_loadout = Session.saved_loadout.duplicate()
	_motion = Settings.reduced_motion
	_blind = Settings.colorblind
	Settings.set_reduced_motion(true)
	Settings.set_colorblind(false)


func after_each() -> void:
	Session.saved_loadout = _loadout
	Session.save()
	Settings.set_reduced_motion(_motion)
	Settings.set_colorblind(_blind)


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	return practice


# ---- the objective -------------------------------------------------------------------------------------------------

func test_the_bases_and_rescue_areas_are_a_clear_fill_and_a_thin_edge() -> void:
	assert_gte(StreetMap.ZONE_FILL_ALPHA, 0.3, "a fill you can see on the cement")
	assert_lt(StreetMap.ZONE_FILL_ALPHA, 0.5, "that still lets the street show")
	assert_gte(StreetMap.TAPE_WIDTH, 0.1, "an edge that still reads at phone size")
	assert_lte(StreetMap.TAPE_WIDTH, 0.15, "half the 0.3 m it was (owner: thinner)")
	var map: StreetMap = autofree(MAP_SCENE.instantiate()) as StreetMap
	add_child(map)
	var tape: Color = map.zone_colors(MapLayout.SIDE_ENEMY)[0]
	assert_gt(tape.s, 0.5, "their tape reads red, not grey")


func test_the_street_is_calm_and_shows_no_shadows() -> void:
	assert_lt(_uniform("crack_depth"), 0.12, "faint hairline cracks")
	assert_lt(_uniform("blotch_depth"), 0.15, "soft blotches")
	assert_gt(StreetMap.AMBIENT_ENERGY, 0.5, "the shadow side of a model is not muddy")
	var upward: float = StreetMap.SUN_ENERGY * sin(deg_to_rad(-StreetMap.SUN_ANGLES.x)) + StreetMap.AMBIENT_ENERGY
	assert_almost_eq(upward, 1.0, 0.1, "the lights put an upward face of a model at about its own colour (no over-exposure)")
	var sun: DirectionalLight3D = autofree(DirectionalLight3D.new()) as DirectionalLight3D
	StreetMap._light_scene(sun, sun)
	assert_eq(sun.shadow_opacity, 0.0, "no shadows on the street (owner): fully see-through")
	assert_true(sun.shadow_enabled, "but the shadow pass stays, or the whole street goes dark")
	assert_gt(StreetMap.SUN_COLOR.r, StreetMap.SUN_COLOR.b, "a warm sun like Clash Royale's (owner)")


## The default of a float uniform in the street's cement shader.
func _uniform(uniform: String) -> float:
	var found: RegExMatch = RegEx.create_from_string("uniform float %s = ([0-9.]+);" % uniform).search(StreetMap.STREET_SHADER.code)
	assert_not_null(found, "%s is a uniform" % uniform)
	return found.get_string(1).to_float() if found != null else INF


# ---- bars, impacts, banners ------------------------------------------------------------------------------------------

func test_the_health_bars_are_chunky_at_phone_size() -> void:
	var rules: CameraRules = load("res://data/rules/camera_rules.tres") as CameraRules
	assert_gte(OverheadHud.BAR_SIZE.y * rules.bar_scale, 9.0, "at least 9 px thick on the 720 px design")
	assert_lte(OverheadHud.BAR_OUTLINE, 2.0, "with a thin outline (owner: the bar outlines were too thick)")
	assert_gte(OverheadHud.BAR_OUTLINE, 1.0, "that still shows")


func test_a_hit_has_no_ground_ring_and_reduce_motion_keeps_it_still() -> void:
	var practice: PracticeMatch = _practice()
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	practice.sim.damage(PracticeMatch.ENEMY_STAND_ID, 9)
	practice.advance(DT)
	assert_eq(fx.context().ground.active_rings().size(), 0, "no ring on the ground (owner)")
	assert_eq(fx.particle_count(), 0, "no stars with Reduce motion")


func test_a_hit_throws_four_stars_and_no_ring() -> void:
	Settings.set_reduced_motion(false)
	var practice: PracticeMatch = _practice()
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	practice.sim.damage(PracticeMatch.ENEMY_STAND_ID, 9)
	practice.advance(DT)
	var stars: int = 0
	for emitter: CPUParticles3D in fx.context().particles.busy():
		if emitter.mesh == FxKit.star_quad():
			stars += emitter.amount
	assert_eq(stars, WeaponFxView.BURST_STARS, "four comic stars")
	assert_eq(fx.context().ground.active_rings().size(), 0, "no ring")
	for node: Node in fx.find_children("*", "MeshInstance3D", true, false):
		assert_false((node as MeshInstance3D).mesh is SphereMesh, "no flash over the kid: it hid the kid on every hit")


func test_banners_are_big_and_a_long_one_still_fits_a_narrow_phone() -> void:
	var practice: PracticeMatch = _practice()
	var hud: MatchHud = practice.hud
	assert_gte(MatchHud.BANNER_FONT, 64)
	hud.show_banner("GO!")
	assert_eq(hud.banner.get_theme_font_size("font_size"), MatchHud.BANNER_FONT)
	assert_eq(hud.banner.scale, Vector2.ONE, "no pop with Reduce motion")
	var long: String = "THEY SCORED  2-3"
	var font_size: int = hud.banner_font_size(long)
	var width: float = KalyeahTheme.DISPLAY_FONT.get_string_size(long, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size).x
	assert_lte(width + MatchHud.BANNER_OUTLINE * 2.0, hud.size.x - MatchHud.BANNER_MARGIN * 2.0 + 1.0, "kept inside the screen")


# ---- buttons keep their size -------------------------------------------------------------------------------------------

func test_buttons_have_a_deeper_lip_but_the_same_room_and_sink_when_pressed() -> void:
	assert_gte(UiSkin.BUTTON_LIP, 12)
	for variant: String in UiSkin.BUTTONS:
		var normal: StyleBoxTexture = UiSkin.button(variant, "normal")
		var pressed: StyleBoxTexture = UiSkin.button(variant, "pressed")
		# the old margins: 12 above and 12 + the lip below (9 for a live button, 3 for a disabled one)
		var old: float = 27.0 if variant == "disabled" else 33.0
		assert_almost_eq(normal.content_margin_top + normal.content_margin_bottom, old, 0.01, "%s: the old height" % variant)
		assert_almost_eq(pressed.content_margin_top + pressed.content_margin_bottom, old, 0.01, "%s pressed: the same" % variant)
		if variant != "disabled":
			assert_gt(pressed.content_margin_top, normal.content_margin_top, "%s: the label moves down when pressed" % variant)


# ---- the tagged tray ------------------------------------------------------------------------------------------------------

func test_while_tagged_you_see_your_teammates_and_when_the_rescue_area_is_ready() -> void:
	var practice: PracticeMatch = _practice()
	practice.advance(DT)
	TouchHelper.hurt(practice, 100)
	practice.advance(DT)
	var panel: TaggedPanel = practice.hud.tagged_panel
	assert_true(panel.visible)
	var mates: int = 0
	for id: int in practice.sim.players:
		if id != practice.local_id and practice.sim.players[id].team == practice.local_team:
			mates += 1
	assert_eq(panel.pips.pips.size(), mates, "one face per teammate")
	assert_true(panel.ready_ring.visible, "the rescue area is counting down: the ring shows it")
	assert_almost_eq(panel.ready_ring.seconds, practice.sim.rescue_ready_left[practice.local_team], 0.001)
	assert_eq(panel.find_children("*", "Button", true, false).size(), 1, "nothing to pick: only Change weapons")


func test_a_teammate_running_to_your_rescue_area_shows_as_coming_and_a_tagged_one_as_tagged() -> void:
	var practice: PracticeMatch = _practice()
	var sim: MatchSim = practice.sim
	var me: PlayerState = sim.players[practice.local_id]
	var mate: PlayerState = null
	for id: int in sim.players:
		if id != me.id and sim.players[id].team == me.team:
			mate = sim.players[id]
			break
	var pips: TeammatePips = autofree(TeammatePips.new()) as TeammatePips
	var area: Vector2 = sim.layout.rescue_rect(sim.side_for_team(me.team)).get_center()
	mate.position = area + Vector2(0.0, 10.0)
	pips.sync(sim, me)
	assert_eq(pips.count(TeammatePips.State.FREE), pips.pips.size(), "first look: free")
	mate.position = area + Vector2(0.0, 9.0)
	pips.sync(sim, me)
	assert_eq(pips.count(TeammatePips.State.COMING), 1, "nearer the rescue area: on the way")
	mate.tagged = true
	pips.sync(sim, me)
	assert_eq(pips.count(TeammatePips.State.TAGGED), 1)
	assert_false(pips.accessibility_description.is_empty(), "a screen reader hears who is free")


func test_no_ready_ring_once_the_rescue_area_is_ready() -> void:
	var practice: PracticeMatch = _practice()
	practice.sim.rescue_ready_left = [0.0, 0.0]
	TouchHelper.hurt(practice, 100)
	practice.advance(DT)
	assert_false(practice.hud.tagged_panel.ready_ring.visible)


# ---- results and the kid screen ---------------------------------------------------------------------------------------

func test_repeated_ways_of_scoring_are_one_chip_with_a_count() -> void:
	var log_: Array[Vector2i] = [Vector2i(0, MatchSim.CaptureKind.ALL_TAGGED), Vector2i(1, MatchSim.CaptureKind.BASE),
		Vector2i(0, MatchSim.CaptureKind.ALL_TAGGED), Vector2i(0, MatchSim.CaptureKind.ALL_TAGGED)]
	var groups: Array[Array] = MatchResults.how_groups(log_, 0)
	assert_eq(groups.size(), 2)
	assert_eq(groups[0], [true, MatchSim.CaptureKind.ALL_TAGGED, 3])
	assert_eq(MatchResults.group_text(true, MatchSim.CaptureKind.ALL_TAGGED, 3), "Tagged them all × 3")
	assert_eq(MatchResults.group_text(false, MatchSim.CaptureKind.BASE, 1), "Held your base", "once needs no count")
	assert_gte(MatchResults.CONFETTI_MIN, 14.0, "paper you can see")
	var result: MatchResults = autofree(MatchResults.new()) as MatchResults
	assert_null(result.find_child("Weapons", true, false), "the result has no loadout panel")


func test_the_kid_screen_names_the_chosen_kid_large_under_the_carousel() -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var screen: WeaponPickScreen = WeaponPickScreen.new()
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	phone.add_child(screen)
	screen.set_anchors_preset(Control.PRESET_TOP_LEFT)
	screen.size = phone.size
	screen.cards_mode = true
	var ids: Array[StringName] = []
	for weapon: WeaponDef in rules.weapons:
		ids.append(weapon.id)
	screen.open(WeaponPick.new(ids, 10.0), rules.weapons, "", RandomNumberGenerator.new(), rules.characters, &"toni", true)
	await wait_process_frames(2)
	var toni: CharacterDef = null
	for character: CharacterDef in rules.characters:
		if character.id == &"toni":
			toni = character
	assert_eq(screen._hero_name.text, toni.display_name)
	assert_true(screen._hero_name.is_visible_in_tree())
	assert_gte(screen._hero_name.get_theme_font_size("font_size"), 48, "large")
	assert_lte(screen._carousel.get_global_rect().end.y, screen._hero_name.get_global_rect().position.y + 1.0, "the name is under the cards")
	var other: CharacterDef = rules.characters[0] if rules.characters[0].id != &"toni" else rules.characters[1]
	screen.pick_character(other.id)
	assert_eq(screen._hero_name.text, other.display_name, "the name follows the pick")
	assert_lt(screen._hero_name.get_global_rect().end.y, screen._ready_button.get_global_rect().position.y, "above Play")


func test_no_kid_has_a_ring_under_its_feet() -> void:
	var practice: PracticeMatch = _practice()
	practice.advance(DT)
	for id: int in practice.sim.players:
		var view: KidModel = practice._views[id]
		assert_null(view.find_child("TeamRing", true, false), "kid %d has no ring: the bar says whose it is" % id)
