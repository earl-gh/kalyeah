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


func _ring_color(view: KidModel) -> Color:
	var colors: PackedColorArray = (view.ring.mesh as ArrayMesh).surface_get_arrays(0)[Mesh.ARRAY_COLOR] as PackedColorArray
	return Color(colors[colors.size() - 1], 1.0)


## Vertex colours are stored in 8 bits: equal to one step.
func _near(a: Color, b: Color) -> bool:
	return absf(a.r - b.r) < 0.01 and absf(a.g - b.g) < 0.01 and absf(a.b - b.b) < 0.01


# ---- the team ring under every kid ---------------------------------------------------------------------------------

func test_every_kid_stands_on_a_ring_in_its_bar_colour() -> void:
	var practice: PracticeMatch = _practice()
	practice.advance(DT)
	var bars: OverheadHud = practice.overhead()
	var seen: Dictionary[Color, bool] = {}
	for id: int in practice.sim.players:
		var view: KidModel = practice._views[id]
		assert_not_null(view.ring, "kid %d has a team ring" % id)
		assert_eq(view.ring.cast_shadow, GeometryInstance3D.SHADOW_CASTING_SETTING_OFF)
		var color: Color = _ring_color(view)
		assert_true(_near(color, bars.bar_color(practice.sim.players[id])), "kid %d: the ring matches its bar" % id)
		seen[color] = true
	assert_eq(seen.size(), 3, "you, your team and theirs are three colours")


func test_the_rings_follow_the_colour_blind_setting_mid_match() -> void:
	var practice: PracticeMatch = _practice()
	practice.advance(DT)
	Settings.set_colorblind(true)
	var mine: KidModel = practice._views[practice.local_id]
	assert_true(_near(_ring_color(mine), TeamColors.bar_self()), "your ring turns near white with your bar")


func test_your_ring_is_bolder_and_the_ring_never_changes_the_hit_area() -> void:
	var plain: ArrayMesh = KidModel.ring_mesh(0.4, Color.RED, false)
	var mine: ArrayMesh = KidModel.ring_mesh(0.4, Color.GREEN, true)
	assert_eq(plain.surface_get_array_len(0), mine.surface_get_array_len(0))
	assert_gt(KidModel.RING_RIM_SELF, KidModel.RING_RIM)
	assert_gt(KidModel.RING_FILL_SELF_ALPHA, KidModel.RING_FILL_ALPHA)
	var practice: PracticeMatch = _practice()
	assert_eq(practice.sim.rules.player_radius, (load("res://data/rules/game_rules.tres") as GameRules).player_radius, "the hit area is the rules' own")


# ---- the objective -------------------------------------------------------------------------------------------------

func test_the_bases_and_rescue_areas_are_a_clear_fill_and_a_thick_edge() -> void:
	assert_gte(StreetMap.ZONE_FILL_ALPHA, 0.3, "a fill you can see on the cement")
	assert_lt(StreetMap.ZONE_FILL_ALPHA, 0.5, "that still lets the street show")
	assert_gte(StreetMap.TAPE_WIDTH, 0.25, "a thick chalk edge")
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


func test_a_hit_has_a_ground_ring_and_reduce_motion_keeps_it_still() -> void:
	var practice: PracticeMatch = _practice()
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	practice.sim.damage(PracticeMatch.ENEMY_STAND_ID, 1)
	practice.advance(DT)
	assert_eq(fx.burst_count(), 1)
	var burst: Node3D = fx.get_node("Burst") as Node3D
	var ring: Node3D = burst.get_node("ImpactRing") as Node3D
	var size: float = (WeaponFxView.BURST_MIN + 1.0 / WeaponFxView.BURST_DAMAGE_STEP) * fx.view_scale
	assert_almost_eq(ring.scale.x, size * WeaponFxView.BURST_RING_TO, 0.01, "a tick still reads near the smallest size, held at its end size")
	for child: Node in burst.get_children():
		assert_false(child is MeshInstance3D and (child as MeshInstance3D).mesh is BoxMesh, "no sparks with Reduce motion")


func test_impacts_grow_with_the_drawn_kid_and_sparks_fly_with_motion() -> void:
	Settings.set_reduced_motion(false)
	var practice: PracticeMatch = _practice()
	var fx: WeaponFxView = practice.get_node("%WeaponFx") as WeaponFxView
	practice.sim.damage(PracticeMatch.ENEMY_STAND_ID, 9)
	practice.advance(DT)
	var burst: Node3D = fx.get_node("Burst") as Node3D
	var sparks: int = 0
	for child: Node in burst.get_children():
		if child is MeshInstance3D and (child as MeshInstance3D).mesh is BoxMesh:
			sparks += 1
	assert_eq(sparks, WeaponFxView.BURST_SPARKS)
	var flash: Node3D = burst.get_child(0) as Node3D
	assert_almost_eq(flash.position.y, WeaponFxView.BURST_HEIGHT * fx.view_scale, 0.001, "the flash is on the drawn kid, not at its knees")


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

func test_while_tagged_you_see_your_teammates_and_the_way_out_alone() -> void:
	var practice: PracticeMatch = _practice()
	practice.advance(DT)
	practice.hurt_local(100)
	practice.advance(DT)
	var panel: TaggedPanel = practice.hud.tagged_panel
	assert_true(panel.visible)
	var mates: int = 0
	for id: int in practice.sim.players:
		if id != practice.local_id and practice.sim.players[id].team == practice.local_team:
			mates += 1
	assert_eq(panel.pips.pips.size(), mates, "one face per teammate")
	assert_true(panel.release_ring.visible, "Practice lets you walk out alone: the ring counts it down")
	assert_almost_eq(panel.release_ring.seconds, practice.sim.tag_release_time - practice.sim.players[practice.local_id].tag_time, 0.001)
	assert_eq(panel.find_children("*", "Button", true, false).size(), 1, "nothing to pick: only Change weapons")


func test_a_teammate_running_to_your_box_shows_as_coming_and_a_tagged_one_as_tagged() -> void:
	var practice: PracticeMatch = _practice()
	var sim: MatchSim = practice.sim
	var me: PlayerState = sim.players[practice.local_id]
	var mate: PlayerState = null
	for id: int in sim.players:
		if id != me.id and sim.players[id].team == me.team:
			mate = sim.players[id]
			break
	var pips: TeammatePips = autofree(TeammatePips.new()) as TeammatePips
	var box: Vector2 = sim.layout.jail_rect(sim.side_for_team(me.team)).get_center()
	mate.position = box + Vector2(0.0, 10.0)
	pips.sync(sim, me)
	assert_eq(pips.count(TeammatePips.State.FREE), pips.pips.size(), "first look: free")
	mate.position = box + Vector2(0.0, 9.0)
	pips.sync(sim, me)
	assert_eq(pips.count(TeammatePips.State.COMING), 1, "nearer the box: on the way")
	mate.tagged = true
	pips.sync(sim, me)
	assert_eq(pips.count(TeammatePips.State.TAGGED), 1)
	assert_false(pips.accessibility_description.is_empty(), "a screen reader hears who is free")


func test_no_release_ring_when_the_match_has_no_release_time() -> void:
	var practice: PracticeMatch = _practice()
	practice.sim.tag_release_time = 0.0
	practice.hurt_local(100)
	practice.advance(DT)
	assert_false(practice.hud.tagged_panel.release_ring.visible)


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
	assert_gt(MatchResults.WEAPON_ICON, 64.0)


func test_the_kid_screen_names_the_chosen_kid_large_on_a_stage() -> void:
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
	assert_eq(screen._hero.get_parent().name, &"Stage", "the kid stands on the stage")
	var other: CharacterDef = rules.characters[0] if rules.characters[0].id != &"toni" else rules.characters[1]
	screen.pick_character(other.id)
	assert_eq(screen._hero_name.text, other.display_name, "the name follows the pick")
	assert_lt(screen._hero_name.get_global_rect().end.y, screen._ready_button.get_global_rect().position.y, "above Play")
