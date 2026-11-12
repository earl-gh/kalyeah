extends GutTest
## Comprehensive 5v5 Barangay Arena, 10-Kid Lineup, Hero-Action Combat,
## Deterministic BarangayNav A*, and 7-Tier Tagalog RankedSystem acceptance suite.

const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const NET_RULES: NetRules = preload("res://data/rules/net_rules.tres")
const DT: float = 1.0 / 30.0
const TEMP_RANKED_FILE: String = "test_barangay_5v5_ranked.cfg"
const TEMP_HISTORY_FILE: String = "test_barangay_5v5_history.cfg"
const FALLBACK_SCRATCH_DIR: String = "C:/Users/Den/.gemini/antigravity/brain/15d343b2-9c0c-402b-af26-44fc50aeed25/scratch"

const EXPECTED_KIDS: Array[String] = [
	"junjun",
	"ligaya",
	"migo",
	"toni",
	"popoy",
	"inday",
	"kiko",
	"nene",
	"buknoy",
	"tala",
]

const EXPECTED_TIERS: Array[String] = [
	"Kahoy",
	"Bakal",
	"Tanso",
	"Pilak",
	"Ginto",
	"Brilyante",
	"Batang Kalyeah",
]

var _saved_ranked_path: String = ""
var _saved_history_path: String = ""
var _temp_ranked_path: String = ""
var _temp_history_path: String = ""


func before_each() -> void:
	_saved_ranked_path = RankedSystem.path
	_saved_history_path = MatchHistory.path
	_temp_ranked_path = _resolve_writable_temp_path(TEMP_RANKED_FILE)
	_temp_history_path = _resolve_writable_temp_path(TEMP_HISTORY_FILE)
	RankedSystem.path = _temp_ranked_path
	MatchHistory.path = _temp_history_path
	_cleanup_temp_files()


func after_each() -> void:
	_cleanup_temp_files()
	if not _saved_ranked_path.is_empty():
		RankedSystem.path = _saved_ranked_path
	if not _saved_history_path.is_empty():
		MatchHistory.path = _saved_history_path


func _resolve_writable_temp_path(filename: String) -> String:
	var candidates: Array[String] = []
	if not OS.get_user_data_dir().is_empty():
		candidates.append("user://%s" % filename)
	var cache_dir: String = OS.get_cache_dir()
	if not cache_dir.is_empty():
		candidates.append(cache_dir.path_join(filename))
	var godot_dir: String = ProjectSettings.globalize_path("res://.godot")
	if DirAccess.dir_exists_absolute(godot_dir):
		candidates.append(godot_dir.path_join(filename))
	if DirAccess.dir_exists_absolute(FALLBACK_SCRATCH_DIR):
		candidates.append("%s/%s" % [FALLBACK_SCRATCH_DIR, filename])
	for cand: String in candidates:
		var probe: ConfigFile = ConfigFile.new()
		probe.set_value("probe", "ok", 1)
		if probe.save(cand) == OK:
			_remove_path(cand)
			return cand
	return godot_dir.path_join(filename)


func _cleanup_temp_files() -> void:
	_remove_path(_temp_ranked_path)
	_remove_path(_temp_history_path)


func _remove_path(cfg_path: String) -> void:
	if cfg_path.is_empty():
		return
	var abs_p: String = ProjectSettings.globalize_path(cfg_path) if (cfg_path.begins_with("user://") or cfg_path.begins_with("res://")) else cfg_path
	if FileAccess.file_exists(cfg_path) or FileAccess.file_exists(abs_p):
		DirAccess.remove_absolute(abs_p)


func _no_trip_rules() -> GameRules:
	var copy: GameRules = RULES.duplicate() as GameRules
	copy.trip_chance = 0.0
	return copy


func _open_circular_layout() -> MapLayout:
	var custom: MapLayout = MapLayout.new()
	custom.tiles = Vector2i(58, 58)
	custom.tile_size = 3.0
	custom.arena_radius = 84.0
	custom.base_inset = 16.0
	custom.base_radius = 12.5
	custom.base_corner = 3.0
	custom.spawn_offset = 6.0
	custom.rescue_size = Vector2(8.0, 14.0)
	custom.tagged_places = 5
	custom.boundary_thickness = 2.0
	custom.powerup_spots = [
		Vector2(-34.0, 0.0),
		Vector2(0.0, 0.0),
		Vector2(34.0, 0.0),
	]
	return custom


# ---------------------------------------------------------------------------
# 1. Circular Barangay MapLayout & Creek Projectile-Passable / Walk-Blocking
# ---------------------------------------------------------------------------

func test_circular_barangay_arena_radius_in_arena_and_clamp() -> void:
	assert_almost_eq(LAYOUT.arena_radius, 84.0, 0.001, "Barangay compound is a circular 84 m radius arena")
	assert_gte(LAYOUT.tagged_places, 5, "Rescue sidewalk holds all 5 teammates in 5v5")

	assert_true(LAYOUT.in_arena(Vector2.ZERO), "Plaza center is inside the arena")
	assert_true(LAYOUT.in_arena(Vector2(83.5, 0.0)), "Just inside 84 m East rim")
	assert_true(LAYOUT.in_arena(Vector2(0.0, -83.5)), "Just inside 84 m North rim")
	assert_true(LAYOUT.in_arena(Vector2(58.0, 58.0)), "Diagonal inside 84 m radius (~82.02 m)")
	assert_false(LAYOUT.in_arena(Vector2(84.5, 0.0)), "Outside 84 m East rim")
	assert_false(LAYOUT.in_arena(Vector2(70.0, 70.0)), "Outside circular rim even though inside bounding square")

	var pad: float = RULES.player_radius
	var inside_pt: Vector2 = Vector2(24.0, -18.0)
	assert_true(LAYOUT.clamp_to_arena(inside_pt, pad).is_equal_approx(inside_pt), "Interior point stays untouched")

	var clamped_east: Vector2 = LAYOUT.clamp_to_arena(Vector2(120.0, 0.0), pad)
	assert_almost_eq(clamped_east.length(), 84.0 - pad, 0.01, "Radial clamp pulls point to arena_radius - pad")
	assert_almost_eq(clamped_east.y, 0.0, 0.001)

	var clamped_diag: Vector2 = LAYOUT.clamp_to_arena(Vector2(80.0, 80.0), pad)
	assert_almost_eq(clamped_diag.length(), 84.0 - pad, 0.01, "Diagonal corner clamps onto circular perimeter")

	var sim: MatchSim = MatchSim.new(RULES, _open_circular_layout(), 101)
	var runner: PlayerState = sim.add_player(1, 0)
	runner.position = Vector2(82.0, 0.0)
	for _tick: int in 45:
		sim.set_input(1, PlayerInput.create(Vector2(1.0, 0.0), Vector2.ZERO, 0, sim.tick))
		sim.step(DT)
	assert_lte(runner.position.length(), 84.0 - runner.radius + 0.02, "MatchSim movement enforces circular Barangay wall")


func test_legacy_zero_arena_radius_preserves_rectangular_bounds() -> void:
	var rect_layout: MapLayout = MapLayout.new()
	rect_layout.tiles = Vector2i(16, 32)
	rect_layout.tile_size = 3.0
	rect_layout.arena_radius = 0.0
	rect_layout.base_corner = 2.0
	assert_true(rect_layout.in_arena(Vector2(20.0, 42.0)), "arena_radius == 0 keeps rectangular corner walkable")


func test_symmetric_tabi_tabi_obstacles_and_water_rects_block_walk_not_projectiles() -> void:
	var obs: Array[Rect2] = LAYOUT.obstacle_rects()
	var half_obs: int = LAYOUT.obstacles.size()
	assert_eq(half_obs, 14, "Overhaul v2 Barangay layout has 14 half-map building/landmark footprints")
	assert_eq(obs.size(), half_obs * 2, "Obstacles mirror point-symmetrically for both teams")
	for i: int in half_obs:
		var expected_mirror: Rect2 = Rect2(-obs[i].end, obs[i].size)
		assert_true(obs[i + half_obs].is_equal_approx(expected_mirror), "Obstacle %d mirrors around (0, 0)" % i)

	var water: Array[Rect2] = LAYOUT.water_rects()
	assert_eq(LAYOUT.water_obstacles.size(), 0, "Overhaul v2 shallow mid canal is walkable across all 3 N-S pathways")
	assert_eq(water.size(), 0, "Walkable shallow canal adds zero blocking water_rects in map_layout.tres")
	assert_eq(
		LAYOUT.powerup_spots,
		[Vector2(-34.0, 0.0), Vector2(0.0, 0.0), Vector2(34.0, 0.0)],
		"Barangay layout places 3 merienda power-up stands along West, Center, and East mid canal crossings"
	)

	var walk_all: Array[Rect2] = LAYOUT.walk_obstacle_rects()
	assert_eq(walk_all.size(), obs.size(), "walk_obstacle_rects equals solid cover when mid canal is shallow-walkable")

	# Verify physics rule in MatchSim: when a custom layout defines water_obstacles, it blocks walking while Tirador shots fly over it.
	var creek_layout: MapLayout = _open_circular_layout()
	creek_layout.water_obstacles = [Rect2(-12.0, 1.0, 24.0, 2.0)]
	var sim: MatchSim = MatchSim.new(_no_trip_rules(), creek_layout, 202)
	var shooter: PlayerState = sim.add_player(1, 0)
	var target: PlayerState = sim.add_player(2, 1)
	shooter.position = Vector2(0.0, 5.5)
	target.position = Vector2(0.0, -5.5)

	for _tick: int in 35:
		sim.set_input(1, PlayerInput.create(Vector2(0.0, -1.0), Vector2.ZERO, 0, sim.tick))
		sim.step(DT)
	assert_gte(shooter.position.y, 3.0 + shooter.radius - 0.05, "Creek channel blocks player from walking across water")

	var hp_before: int = target.hp
	var fire_input: PlayerInput = PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_PRIMARY, sim.tick)
	fire_input.with_aim_point(target.position)
	sim.set_input(1, fire_input)
	sim.step(DT)
	assert_gt(sim.weapons.projectiles.size(), 0, "Tirador spawns projectile at creek bank")

	for _tick: int in 30:
		sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
		sim.step(DT)
	assert_lt(target.hp, hp_before, "Projectile crosses water_rects unimpeded and damages enemy on far bank")


# ---------------------------------------------------------------------------
# 2. BarangayNav Deterministic A* Routing Across 3 N-S Pathways & Walkable Canal
# ---------------------------------------------------------------------------

func test_barangay_nav_deterministic_routing_through_eskinitas_and_bridges() -> void:
	var nav_a: BarangayNav = BarangayNav.new(LAYOUT, RULES.player_radius)
	var nav_b: BarangayNav = BarangayNav.new(LAYOUT, RULES.player_radius)
	assert_gt(nav_a.waypoints.size(), 8, "BarangayNav builds intersection, canal-ford, and corner waypoints")
	assert_eq(nav_a.waypoints, nav_b.waypoints, "Waypoint graph construction is 100% deterministic")

	var walk_rects: Array[Rect2] = LAYOUT.walk_obstacle_rects()
	for wp: Vector2 in nav_a.waypoints:
		assert_true(LAYOUT.in_arena(wp), "Waypoint %s lies inside circular Barangay arena" % str(wp))
		for rect: Rect2 in walk_rects:
			assert_false(rect.has_point(wp), "Waypoint %s stays clear of walk-blocking rect %s" % [str(wp), str(rect)])

	# All 3 wide North-South pathways (West X=-34, Center X=0, East X=+34) ford directly across the shallow mid canal.
	for lane_x: float in [-34.0, 0.0, 34.0]:
		assert_true(
			nav_a.is_clear(Vector2(lane_x, 10.0), Vector2(lane_x, -10.0)),
			"N-S pathway X=%.1f wades directly across shallow mid canal without choke obstacles" % lane_x
		)

	# Full cross-barangay navigation from Team 0 spawn (+Z) to Team 1 spawn (-Z) across the 3-pathway barangay grid.
	var start: Vector2 = LAYOUT.spawn_center(MapLayout.SIDE_OWN)
	var goal: Vector2 = LAYOUT.spawn_center(MapLayout.SIDE_ENEMY)
	var pos: Vector2 = start
	var step_dist: float = 2.0
	var reached: bool = false
	for _step_idx: int in 220:
		var wp_a: Vector2 = nav_a.next_waypoint(pos, goal)
		var wp_b: Vector2 = nav_b.next_waypoint(pos, goal)
		assert_true(wp_a.is_equal_approx(wp_b), "A* next_waypoint query is deterministic at %s" % str(pos))
		var dir: Vector2 = wp_a - pos
		if dir.length() <= step_dist:
			pos = wp_a
		else:
			pos += dir.normalized() * step_dist
		if pos.distance_to(goal) <= 2.5:
			reached = true
			break
	assert_true(reached, "Agent routes from South base to North base across Barangay pathways")

	# Custom deep-creek layout check: BarangayNav detours around water_obstacles when present.
	var creek_layout: MapLayout = _open_circular_layout()
	creek_layout.water_obstacles = [Rect2(-18.0, -2.0, 36.0, 4.0)]
	var creek_nav: BarangayNav = BarangayNav.new(creek_layout, RULES.player_radius)
	var south_bank: Vector2 = Vector2(0.0, 5.0)
	var north_bank: Vector2 = Vector2(0.0, -5.0)
	assert_false(creek_nav.is_clear(south_bank, north_bank), "Straight segment across deep creek channel is blocked for walking")
	var detour_wp: Vector2 = creek_nav.next_waypoint(south_bank, north_bank)
	assert_false(detour_wp.is_equal_approx(north_bank), "BarangayNav selects a corner detour around deep water_obstacles")


# ---------------------------------------------------------------------------
# 3. All 10 Filipino Kids Lineup, Models, Portraits & 5v5 Team Uniqueness
# ---------------------------------------------------------------------------

func test_all_ten_filipino_kids_have_models_portraits_and_fair_5v5_assignment() -> void:
	assert_eq(RULES.characters.size(), EXPECTED_KIDS.size(), "game_rules.tres registers all 10 Filipino kids")
	var found_ids: Array[String] = []
	for i: int in RULES.characters.size():
		var def: Resource = RULES.characters[i]
		assert_not_null(def)
		var id_str: String = String(def.get(&"id"))
		var disp_name: String = String(def.get(&"display_name"))
		var stand_rel: float = float(def.get(&"cast_release_standing"))
		var move_rel: float = float(def.get(&"cast_release_moving"))
		found_ids.append(id_str)
		assert_false(disp_name.strip_edges().is_empty(), "%s has a display_name" % id_str)
		assert_gt(stand_rel, 0.0, "%s defines standing cast release timing" % id_str)
		assert_gt(move_rel, 0.0, "%s defines moving cast release timing" % id_str)
		assert_true(
			ResourceLoader.exists("res://assets/models/characters/%s.glb" % id_str),
			"Missing rigged 3D .glb model for %s" % id_str
		)
		assert_true(
			ResourceLoader.exists("res://assets/portraits/%s.png" % id_str),
			"Missing portrait PNG for %s" % id_str
		)
		assert_true(
			ResourceLoader.exists("res://assets/portraits/faces/%s.png" % id_str),
			"Missing minimap face PNG for %s" % id_str
		)
		assert_true(
			ResourceLoader.exists("res://assets/portraits/cards/%s.webp" % id_str),
			"Missing 90s komiks card WebP for %s" % id_str
		)
	assert_eq(found_ids, EXPECTED_KIDS, "Exact 10-kid roster order matches specification")

	# Full 10-player 5v5 match: 5 unique kids per team, identical competitive hitbox & HP.
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, 505)
	for i: int in 5:
		sim.add_player(i + 1, 0)
		sim.add_player(i + 6, 1)
	sim.assign_characters()

	var team0_kids: Dictionary[StringName, bool] = {}
	var team1_kids: Dictionary[StringName, bool] = {}
	for pid: int in sim.players:
		var p: PlayerState = sim.players[pid]
		assert_ne(p.character_id, &"", "Player %d assigned a Filipino kid" % pid)
		assert_eq(p.hp, RULES.player_max_hp, "Cosmetic fairness: identical max HP")
		assert_almost_eq(p.radius, RULES.player_radius, 0.0001, "Cosmetic fairness: identical hitbox radius")
		if p.team == 0:
			team0_kids[p.character_id] = true
		else:
			team1_kids[p.character_id] = true
	assert_eq(team0_kids.size(), 5, "All 5 allies get distinct kids")
	assert_eq(team1_kids.size(), 5, "All 5 enemies get distinct kids")


# ---------------------------------------------------------------------------
# 4. 5v5 RoomService, 3-Superpower Loadout & Hero-Action Combat Kit
# ---------------------------------------------------------------------------

func test_room_service_5v5_ten_player_capacity_and_three_superpower_loadouts() -> void:
	assert_eq(RoomService.MAX_TEAM_SIZE, 5, "Online/Hotspot rooms are 5v5 (5 per team)")
	var service: RoomService = RoomService.new(NET_RULES, 42)
	var room: RoomService.Room = service.create_room(1, "HostBida")
	assert_not_null(room)
	for peer_id: int in range(2, 11):
		var res: RoomService.Result = service.join(room.code, peer_id, "Bida%d" % peer_id)
		assert_eq(res, RoomService.Result.OK, "Peer %d joins 10-player 5v5 room" % peer_id)
	assert_eq(room.members.size(), 10, "Room holds 10 players total")
	assert_eq(room.team_count(0), 5, "Team 0 has 5 players")
	assert_eq(room.team_count(1), 5, "Team 1 has 5 players")
	assert_eq(service.join(room.code, 11, "Extra"), RoomService.Result.FULL, "11th player rejected as FULL")
	var desc: Dictionary = service.describe(room)
	assert_eq(int(desc.get("team_size", 0)), 5, "Room descriptor advertises team_size == 5")

	assert_eq(RULES.loadout_size, 3, "Hero-action rules equip 3 superpowers")
	assert_false(RULES.card_cycle, "Fixed 3-superpower cooldown kit replaces 4-card deck cycle")
	var sim: MatchSim = MatchSim.new(RULES, _open_circular_layout(), 77)
	var p1: PlayerState = sim.add_player(1, 0)
	var p2: PlayerState = sim.add_player(2, 1)
	assert_true(sim.set_loadout(1, &"bato_light", &"papel_shield", &"lata"), "Equips 3 distinct superpowers")
	assert_eq(p1.weapons.size(), 3)
	assert_false(sim.set_loadout(1, &"bato_light", &"bato_light", &"lata"), "Rejects duplicate superpower in 3-slot kit")
	sim.equip(2, true)
	assert_eq(p2.weapons.size(), 3, "Bot auto-equip fills all 3 superpower slots")


func test_tirador_primary_luksong_tinik_dash_bagsak_ultimate_and_v2_locomotion() -> void:
	assert_false(RULES.sprint_enabled, "Overhaul v2 replaces auto-sprint with analog walk/run + slide/vault")
	assert_almost_eq(RULES.move_speed, 6.4, 0.001, "Overhaul v2 base street run speed is 6.4 m/s")
	assert_almost_eq(RULES.walk_run_threshold, 0.62, 0.001, "Analog stick threshold splits walk (<0.62) and run (>=0.62)")

	var sim: MatchSim = MatchSim.new(_no_trip_rules(), _open_circular_layout(), 808)
	var hero: PlayerState = sim.add_player(1, 0)
	var rival: PlayerState = sim.add_player(2, 1)
	hero.position = Vector2(-12.0, 12.0)
	rival.position = Vector2(0.0, 10.0)
	sim.set_loadout(1, &"bato_light", &"papel_shield", &"lata")
	sim.set_loadout(2, &"bato_light", &"papel_shield", &"lata")

	var cast_ids: Array[StringName] = []
	var dashed_players: Array[int] = []
	var ult_players: Array[int] = []
	var picked_powerups: Array[int] = []
	sim.weapon_cast.connect(func(_pid: int, wid: StringName) -> void: cast_ids.append(wid))
	sim.player_dashed.connect(func(pid: int) -> void: dashed_players.append(pid))
	sim.ultimate_cast.connect(func(pid: int) -> void: ult_players.append(pid))
	sim.powerup_taken.connect(func(pid: int, _pu_id: int, kind: int) -> void:
		if pid == 1:
			picked_powerups.append(kind)
	)

	# 1) Overhaul v2 Locomotion: Analog Walk vs Run, Jump, Crouch & Ground Slide.
	sim.set_input(1, PlayerInput.create(Vector2(-0.5, 0.0), Vector2.ZERO, 0, sim.tick))
	sim.step(DT)
	assert_true(hero.walking, "Analog stick < 0.62 enters steady Walk state")
	assert_almost_eq(hero.run_time, 0.0, 0.0001, "Walking never accumulates trip run_time")

	sim.set_input(1, PlayerInput.create(Vector2(-1.0, 0.0), Vector2.ZERO, 0, sim.tick))
	sim.step(DT)
	assert_false(hero.walking, "Full stick deflection >= 0.62 enters full Run state")

	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CROUCH, sim.tick))
	sim.step(DT)
	assert_true(hero.crouching, "BTN_CROUCH from rest enters stealth Crouch")
	var crouch_origin: Vector2 = hero.position
	sim.set_input(1, PlayerInput.create(Vector2(-1.0, 0.0), Vector2.ZERO, PlayerInput.BTN_CROUCH, sim.tick))
	sim.step(DT)
	assert_true(hero.crouching, "Holding BTN_CROUCH maintains stealth Crouch while moving")
	var crouch_step: float = hero.position.distance_to(crouch_origin)
	assert_almost_eq(crouch_step, RULES.move_speed * RULES.crouch_speed_share * DT, 0.03, "Crouch scales speed by crouch_speed_share")

	# Release crouch, build run speed, then trigger Ground Slide (BTN_SLIDE) and Jump (BTN_JUMP).
	for _t: int in 4:
		sim.set_input(1, PlayerInput.create(Vector2(1.0, 0.0), Vector2.ZERO, 0, sim.tick))
		sim.step(DT)
	var slide_origin: Vector2 = hero.position
	sim.set_input(1, PlayerInput.create(Vector2(1.0, 0.0), Vector2.ZERO, PlayerInput.BTN_SLIDE, sim.tick))
	sim.step(DT)
	assert_gt(hero.slide_left, 0.0, "BTN_SLIDE starts kinetic street ground slide")
	var slide_step: float = hero.position.distance_to(slide_origin)
	assert_gt(slide_step, RULES.move_speed * DT * 1.2, "Ground slide bursts faster than normal run")
	for _t: int in ceili((RULES.slide_duration + 0.05) / DT):
		sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
		sim.step(DT)

	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_JUMP, sim.tick))
	sim.step(DT)
	assert_gt(hero.air_y, 0.0, "BTN_JUMP launches vertical jump parabola")
	for _t: int in 30:
		sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
		sim.step(DT)
	assert_almost_eq(hero.air_y, 0.0, 0.001, "Jump lands cleanly back on street plane")

	# Merienda Power-Up pickup & Snapshot wire sync round-trip.
	assert_eq(sim.powerups.size(), 3, "MatchSim spawns 3 Barangay merienda power-up stands")
	var first_pu: Dictionary = sim.powerups[0]
	var pu_pos: Vector2 = first_pu["pos"] as Vector2
	hero.hp = RULES.player_max_hp - 20
	hero.position = pu_pos
	sim.set_input(1, PlayerInput.create(Vector2(0.45, 0.0), Vector2.ZERO, PlayerInput.BTN_JUMP, sim.tick))
	sim.step(DT)
	assert_eq(picked_powerups.size(), 1, "Stepping onto active merienda stand emits powerup_taken")
	assert_gt(hero.buff_type, 0, "Hero receives active merienda buff_type")
	assert_gt(hero.buff_left, 0.0, "Hero receives positive merienda buff duration")
	assert_false(bool(sim.powerups[0]["active"]), "Picked merienda stand enters respawn cooldown")

	var empty_acks: Dictionary[int, int] = {}
	var snap: Dictionary = Snapshot.capture(sim, empty_acks)
	var mirror: MatchSim = MatchSim.new(_no_trip_rules(), _open_circular_layout(), 808)
	mirror.add_player(1, 0)
	mirror.add_player(2, 1)
	Snapshot.apply(mirror, snap, -1)
	var mirror_hero: PlayerState = mirror.players[1]
	assert_eq(mirror_hero.walking, hero.walking, "Snapshot syncs walking flag")
	assert_eq(mirror_hero.buff_type, hero.buff_type, "Snapshot syncs active merienda buff_type")
	assert_almost_eq(mirror_hero.buff_left, hero.buff_left, 0.02, "Snapshot syncs merienda buff_left")
	assert_gt(mirror_hero.air_y, 0.0, "Snapshot syncs mid-air jump height")
	assert_false(bool(mirror.powerups[0]["active"]), "Snapshot syncs inactive power-up stand state")

	# Clear active buff & reset positions for clean Tirador / Dash / Ultimate combat checks.
	hero.buff_type = PlayerState.BUFF_NONE
	hero.buff_left = 0.0
	hero.air_y = 0.0
	hero.vel_y = 0.0
	hero.position = Vector2(0.0, 10.0)
	rival.position = Vector2(0.0, 2.0)
	hero.hp = RULES.player_max_hp

	# 2) Tirador (BTN_PRIMARY): rapid slingshot shot, cooldown, damage & ult_charge gain.
	assert_not_null(RULES.primary_weapon, "GameRules defines Tirador primary_weapon")
	var rival_hp_before: int = rival.hp
	var primary_in: PlayerInput = PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_PRIMARY, sim.tick)
	primary_in.with_aim_point(rival.position)
	sim.set_input(1, primary_in)
	sim.step(DT)
	assert_true(cast_ids.has(RULES.primary_weapon.id), "BTN_PRIMARY casts Tirador")
	assert_gt(hero.primary_cd, 0.0, "Tirador enters short fire cooldown")
	for _t: int in 20:
		sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
		sim.step(DT)
	assert_lt(rival.hp, rival_hp_before, "Tirador projectile damages rival")
	assert_gt(hero.ult_charge, 0.0, "Dealing damage charges Bida Ultimate meter")

	# 3) Luksong-Tinik (BTN_DASH): cleanses SLOW and bursts forward faster than walk speed.
	sim.apply_effect(1, StatusEffects.Type.SLOW, 3.0, 0.55)
	assert_true(hero.effects.has(StatusEffects.Type.SLOW), "SLOW applied before dash")
	var dash_origin: Vector2 = hero.position
	var dash_in: PlayerInput = PlayerInput.create(Vector2(1.0, 0.0), Vector2.ZERO, PlayerInput.BTN_DASH, sim.tick)
	sim.set_input(1, dash_in)
	sim.step(DT)
	assert_false(hero.effects.has(StatusEffects.Type.SLOW), "Luksong-Tinik vault-dash cleanses SLOW")
	assert_true(dashed_players.has(1), "player_dashed signal emitted")
	assert_gt(hero.dash_cd, 0.0, "Dash cooldown started")
	var remaining_dash_ticks: int = ceili(RULES.dash_duration / DT)
	for _t: int in remaining_dash_ticks:
		sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
		sim.step(DT)
	var dash_travel: float = hero.position.distance_to(dash_origin)
	assert_gt(dash_travel, RULES.move_speed * RULES.dash_duration * 1.4, "Luksong-Tinik bursts substantially faster than normal run")

	# 4) Bagsak ng Kalye Ultimate (BTN_ULTIMATE): gated below 100%, unleashes & resets at 100%.
	assert_not_null(RULES.ultimate_weapon, "GameRules defines Bagsak ultimate_weapon")
	hero.ult_charge = 85.0
	var early_ult: PlayerInput = PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_ULTIMATE, sim.tick)
	early_ult.with_aim_point(rival.position)
	sim.set_input(1, early_ult)
	sim.step(DT)
	assert_eq(ult_players.size(), 0, "BTN_ULTIMATE does not fire below 100% ult_charge")

	# Release button for 1 tick so next press is fresh, then fire at 100% charge.
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
	sim.step(DT)
	hero.ult_charge = 100.0
	var ready_ult: PlayerInput = PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_ULTIMATE, sim.tick)
	ready_ult.with_aim_point(rival.position)
	sim.set_input(1, ready_ult)
	sim.step(DT)
	assert_true(ult_players.has(1), "BTN_ULTIMATE emits ultimate_cast at 100% charge")
	assert_true(cast_ids.has(RULES.ultimate_weapon.id), "BTN_ULTIMATE fires ultimate_weapon")
	assert_almost_eq(hero.ult_charge, 0.0, 0.001, "Unleashing Ultimate consumes full ult_charge")


# ---------------------------------------------------------------------------
# 5. Tagalog RankedSystem (7 Tiers, LP Math, Floor Protection & Persistence)
# ---------------------------------------------------------------------------

func test_ranked_system_seven_tagalog_tiers_lp_math_floor_and_persistence() -> void:
	assert_eq(RankedSystem.TIERS.size(), EXPECTED_TIERS.size(), "RankedSystem defines all 7 Tagalog tiers")

	assert_eq(RankedSystem.tier_name(0), "Kahoy")
	assert_eq(RankedSystem.tier_name(199), "Kahoy")
	assert_eq(RankedSystem.tier_name(200), "Bakal")
	assert_eq(RankedSystem.tier_name(399), "Bakal")
	assert_eq(RankedSystem.tier_name(400), "Tanso")
	assert_eq(RankedSystem.tier_name(599), "Tanso")
	assert_eq(RankedSystem.tier_name(600), "Pilak")
	assert_eq(RankedSystem.tier_name(849), "Pilak")
	assert_eq(RankedSystem.tier_name(850), "Ginto")
	assert_eq(RankedSystem.tier_name(1149), "Ginto")
	assert_eq(RankedSystem.tier_name(1150), "Brilyante")
	assert_eq(RankedSystem.tier_name(1499), "Brilyante")
	assert_eq(RankedSystem.tier_name(1500), "Batang Kalyeah")
	assert_eq(RankedSystem.tier_name(2500), "Batang Kalyeah")

	var ginto_info: Dictionary = RankedSystem.tier_for_lp(900)
	assert_false(ginto_info.is_empty(), "tier_for_lp returns tier metadata dictionary")

	# Fresh temp config starts at 0 LP (Kahoy) and protects floor at 0 LP on loss.
	assert_eq(RankedSystem.current_lp(), 0, "Fresh profile starts at 0 LP")
	var floor_loss: Dictionary = RankedSystem.record_match("online", false, 0, 3)
	assert_eq(int(floor_loss.get("old_lp", -1)), 0)
	assert_eq(int(floor_loss.get("new_lp", -1)), 0, "Floor protection prevents negative LP")
	assert_false(bool(floor_loss.get("promoted", true)))
	assert_eq(RankedSystem.current_lp(), 0)

	# Online win grants positive LP and persists to RankedSystem.path.
	var first_win: Dictionary = RankedSystem.record_match("online", true, 3, 1)
	var gained: int = int(first_win.get("delta", 0))
	var after_win_lp: int = int(first_win.get("new_lp", 0))
	assert_gt(gained, 0, "Online victory grants positive LP")
	assert_eq(after_win_lp, gained)
	assert_eq(RankedSystem.current_lp(), after_win_lp, "LP persists in temp config file")
	assert_true(FileAccess.file_exists(_temp_ranked_path), "Temp ranked config written to disk")

	# Climb past 200 LP to verify Kahoy -> Bakal promotion detection, then verify LP loss math.
	var promoted_seen: bool = bool(first_win.get("promoted", false))
	for _match_idx: int in 15:
		if RankedSystem.current_lp() >= 220:
			break
		var res: Dictionary = RankedSystem.record_match("online", true, 3, 0)
		if bool(res.get("promoted", false)):
			promoted_seen = true
	assert_gte(RankedSystem.current_lp(), 200, "Repeated online wins climb into Bakal tier")
	assert_true(promoted_seen, "Crossing 200 LP reports promoted == true")
	assert_eq(RankedSystem.tier_name(RankedSystem.current_lp()), "Bakal")

	var pre_loss_lp: int = RankedSystem.current_lp()
	var loss_res: Dictionary = RankedSystem.record_match("online", false, 1, 3)
	var loss_delta: int = int(loss_res.get("delta", 0))
	var post_loss_lp: int = int(loss_res.get("new_lp", -1))
	assert_lt(loss_delta, 0, "Online defeat deducts LP above 0")
	assert_eq(post_loss_lp, maxi(0, pre_loss_lp + loss_delta))
	assert_eq(RankedSystem.current_lp(), post_loss_lp, "Updated LP after loss persists cleanly")
