class_name PracticeMatch
extends Node3D
## The match screen. Offline practice runs a local MatchSim with training dummies or a full 5v5 bot squad;
## online (`online = true`, scenes/match/online.tscn) it draws the GameClient's
## mirror of the server's match and sends inputs instead of stepping a sim.
## It wires the pieces together: MatchInput + PcCombatInput (devices -> PlayerInput), MatchHud
## (all HUD controls), the 3D views, audio and the camera. Reads sim state only;
## all rules live in scripts/sim/.

const LOCAL_ID: int = 1
const LOCAL_TEAM: int = 0
const ENEMY_STAND_ID: int = 2
const ENEMY_PATROL_ID: int = 3
const ALLY_ID: int = 4
## A free enemy standing in its own rescue area (bottom right), a third target.
const ENEMY_RESCUE_ID: int = 5
## Practice match vs bots (Agawang Base 5v5): your four allies and five enemies (10 unique kids, driven by TeamCoach).
const BOT_ALLY_IDS: Array[int] = [2, 3, 4, 5]
const BOT_ENEMY_IDS: Array[int] = [6, 7, 8, 9, 10]
## Street games (Patintero, Tumbang Preso, Langit Lupa) keep their 6-player street roster.
const STREET_ALLY_IDS: Array[int] = [2, 3]
const STREET_ENEMY_IDS: Array[int] = [4, 5, 6]
const MAX_STEPS_PER_FRAME: int = 8
const RECONNECT_INTERVAL: float = 2.0
const TITLE_SCENE: String = "res://scenes/ui/title_screen.tscn"
## Tilt to run: how far ahead of the kid (screen pixels) the leaning phone puts the move target, like a finger dragged ahead of it.
const TILT_AHEAD: float = 140.0
## Online: the server closed the room (a hotspot host left), so there is no battle to go back to.
const ROOM_CLOSED_TEXT: String = "The host left.\nThe battle is over."

@export var rules: GameRules
@export var layout: MapLayout
## Online match: driven by Net.client (set `client` before adding to the tree to
## use another one, e.g. in tests).
## A Practice match left after this many seconds counts as a loss in History.
const ABANDON_AFTER: float = 15.0

@export var online: bool = false

var sim: MatchSim
var client: GameClient
var local_id: int = LOCAL_ID
var local_team: int = LOCAL_TEAM
var input: MatchInput = MatchInput.new()
var _pc_input: PcCombatInput = PcCombatInput.new()
## Tilt to run (Settings): the phone's lean, levelled each time the controls come back.
var tilt: TiltInput = TiltInput.new()
## The move target is being steered by the tilt (not by a finger).
var tilt_steering: bool = false
var _accumulator: float = 0.0
## What the screen draws between two sim ticks (kids and flying things glide instead of stepping at 30 Hz).
var blend: TickBlend = TickBlend.new()
var _views: Dictionary[int, KidModel] = {}
var _characters: Dictionary[StringName, CharacterDef] = {}
## The opening pick pauses the match; the box swap does not.
var _opening_pick: bool = false
## Online: our own input counter (the server acks it for reconciliation).
var _input_tick: int = 0
## Online: seconds until the next reconnect attempt after the connection dropped.
var _reconnect_left: float = -1.0
## A new socket is not a restored match until WELCOME confirms the old slot.
var _awaiting_reconnect_welcome: bool = false
var _resume_rejected: bool = false
## Online: who in the room was connected at the last room update (player id -> connected), to tell when it changes.
var _connected: Dictionary[int, bool] = {}
## Online: when the whole other team is away, the server ends the match for us; this says why ("BEN is gone for a while")
## in place of VICTORY!. Empty otherwise.
var gone_note: String = ""
var _application_focused: bool = true
var _application_paused: bool = false
var _presentation: PracticeMatchPresentation
## The bot teams of a Practice match vs bots (empty in training and online).
var _coaches: Array[TeamCoach] = []
## The bots of a street game (Tumbang Preso, Patintero), both teams; null in Agawang Base.
var _mode_coach: StreetModeCoach
## The street game's lines, can and slippers (empty in Agawang Base).
var mode_view: StreetModeView
var _hold_ticked: bool = false
## Tumbang Preso: where the held Throw button aims (radians, see `_angle_of`).
var _throw_angle: float = 0.0
## Cached screen-space MoveStick deflection so camera turns keep steering a steady thumb.
var _stick_screen_way: Vector2 = Vector2.ZERO
var _suppress_idle_behind: bool = false

@onready var hud: MatchHud = %Hud
@onready var _camera: ArenaCamera = %ArenaCamera
@onready var _actors: Node3D = %Actors
@onready var _fx: WeaponFxView = %WeaponFx
var _sfx: SfxView
@onready var _aim_indicator: AimIndicator = %AimIndicator
@onready var _pick_screen: WeaponPickScreen = %PickScreen
@onready var _map: StreetMap = %Map


## A match is played sideways with the follow or shoulder camera (`CameraRules.follow` / `third_person`): set before the HUD lays itself out.
func _enter_tree() -> void:
	var camera: ArenaCamera = get_node_or_null("ArenaCamera") as ArenaCamera
	Settings.set_landscape(camera == null or camera.rules == null or camera.rules.follow or camera.rules.third_person)


func _ready() -> void:
	if online:
		_setup_online()
	else:
		_setup_practice()
	hud.set_street_game(sim.mode != null)
	_presentation = PracticeMatchPresentation.new(self)
	for character: CharacterDef in rules.characters:
		_characters[character.id] = character
	for id: int in sim.players:
		_presentation.make_actor_view(sim.players[id])
	_fx.watch(sim)
	_sfx = SfxView.new()
	_sfx.name = "Sfx"
	add_child(_sfx)
	_sfx.watch(sim, local_id)
	if _camera == null or _camera.rules == null or not _camera.rules.third_person:
		var fog: MatchFog = MatchFog.new()
		add_child(fog)
		fog.build(layout.lane_length, _map.fog_distance(), _map.fog_far_start())  # legacy fixed-camera modes only
	var music: MatchMusic = MatchMusic.new()
	add_child(music)
	music.start(rules.bgm_volume)  # the match's background music (silent until assets/audio/bgm.mp3 is added)
	_fx.context().sound = _sfx.play
	mode_view = StreetModeView.new()
	mode_view.name = "StreetModeView"
	add_child(mode_view)
	mode_view.setup(sim, _fx.context(), local_id)

	_fx.use_blend(blend)
	_map.set_own_side(own_side())
	_map.show_bases(sim.mode == null)
	_map.show_back_button(false)
	_presentation.setup_hud()
	if not online:
		hud.overhead.names[local_id] = Session.player_name  # the others are named after their kid (OverheadHud.name_of)
	_presentation.connect_hud()
	_ensure_locomotion_hud_signals()
	_presentation.connect_sim()
	_pick_screen.exit_requested.connect(_on_exit_pressed)
	_pick_screen.confirmed.connect(_on_pick_confirmed)
	_pick_screen.swapped.connect(_on_swapped)
	_pick_screen.closed.connect(_set_controls_active.bind(true))
	# set before opening: with cards the pick finishes at once (Practice's kid is the main screen's) and that clears it again; set after,
	# it stayed set and the match waited for a pick that was already over (nothing moved, nothing could be tapped)
	_opening_pick = true
	_open_pick("PICK A KID" if sim.mode != null else "PILI NA NG BIDA + 3 SUPERPOWERS", [])  # no skills in the street games
	if online:
		client.stage_changed.connect(_on_stage_changed)
		client.picks_changed.connect(func() -> void: _pick_screen.set_taken(client.taken_kids()))
		client.connection_changed.connect(_on_connection_changed)
		client.welcomed.connect(_on_welcomed)
		client.room_changed.connect(_on_room_changed)
		client.room_closed.connect(_on_room_closed)
		client.said.connect(_on_said)
		_on_room_changed(client.room)
		client.loaded()
		if client.stage == "play":
			_on_stage_changed("play")
	_presentation.sync(0.0)


func _process(delta: float) -> void:
	advance(delta)


## A tap anywhere skips the opening show (offline: online the server runs it for everyone), except Tumbang Preso's: its Mataya-taya! roll always
## plays through (owner: it must show in Practice, Local and Online, and online nobody can skip it).
func _input(event: InputEvent) -> void:
	if online or sim == null or sim.phase != MatchSim.Phase.INTRO or is_picking() or hud.briefing.visible:
		return
	if sim.mode is TumbangPreso and (sim.mode as TumbangPreso).rules.roll_seconds > 0.0:
		return
	if event is InputEventScreenTouch and (event as InputEventScreenTouch).pressed:
		skip_intro()


func _unhandled_input(event: InputEvent) -> void:
	if sim == null or _camera == null or _camera.rules == null:
		return
	var active: bool = _pc_combat_active()
	var aim_pt: Vector2 = crosshair_point() if active else Vector2.ZERO
	var drag: Vector2 = _pc_input.handle_event(event, input, aim_pt, active)
	if drag != Vector2.ZERO:
		_camera.look(drag)


func _pc_combat_active() -> bool:
	return _application_focused and not _application_paused and controls_active() and not is_picking() \
		and not hud.menu.visible and not hud.briefing.visible and sim != null and sim.mode == null \
		and sim.phase == MatchSim.Phase.PLAYING and _camera != null and _camera.rules != null and _camera.rules.third_person


func _notification(what: int) -> void:
	match what:
		NOTIFICATION_APPLICATION_FOCUS_IN:
			_application_focused = true
			return
		NOTIFICATION_APPLICATION_RESUMED:
			_application_paused = false
			return
		NOTIFICATION_APPLICATION_FOCUS_OUT:
			_application_focused = false
		NOTIFICATION_APPLICATION_PAUSED:
			_application_paused = true
		NOTIFICATION_WM_GO_BACK_REQUEST:
			_back_pressed.call_deferred()
			return
		_:
			return
	_cancel_local_input()
	# Android can stop rendering now; send cancellation before the next frame.
	if is_node_ready() and online and client.is_online():
		_step_once(0.0)


## The phone's back button opens the match settings (Resume, Leave). The pick screen, the briefing and the settings take the
## press themselves; the results card has its own buttons. Deferred, so they still read as open here (see MenuDialog).
## The screen changed shape (the phone turned): the safe area's insets are read again.
func _on_screen_changed() -> void:
	_presentation.apply_safe_insets()


func _back_pressed() -> void:
	if is_picking() or hud.menu.visible or hud.briefing.visible or sim.phase == MatchSim.Phase.MATCH_OVER:
		return
	hud.toggle_menu()


func _exit_tree() -> void:
	_pc_input.reset(input)
	Settings.set_landscape(false)
	if online and client != null and client.in_match():
		client.leave_room()
		Session.reconnect_token = ""
		Session.save()


# ---- public API (used by tests and the HUD) ---------------------------------------

## The street point (sim XZ) under screen point `screen` (viewport pixels), or null when that ray misses the ground.
func street_point(screen: Vector2) -> Variant:
	var flat: Vector2 = _camera.flat_position(screen)
	var hit: Variant = Plane(Vector3.UP, 0.0).intersects_ray(_camera.project_ray_origin(flat), _camera.project_ray_normal(flat))
	if hit == null:
		return null
	return Vector2((hit as Vector3).x, (hit as Vector3).z)


## A tap on the street at screen point `at`: walk there.
func move_pressed(at: Vector2) -> void:
	var point: Variant = street_point(at)
	if point is Vector2:
		input.move_press(point as Vector2)
		hud.marker.ping(point as Vector2)


## The finger held on the street moved to `at`: the target follows it.
func move_dragged(at: Vector2) -> void:
	var point: Variant = street_point(at)
	if point is Vector2 and input.move_held and input.follow_hold:
		input.move_drag(point as Vector2)
		hud.marker.ping(point as Vector2, false)


func move_released() -> void:
	input.move_release()


## A card in `slot` was picked up from the hand.
func card_pressed(slot: int) -> void:
	input.card_press(slot, Time.get_ticks_msec())
	_sfx.play("card_pick")


## The held card's finger is at screen point `at` (on its own card, over the hand).
func card_moved(slot: int, at: Vector2, on_card: bool, over_hand: bool) -> void:
	if hud.hand is AbilityArc and _camera.rules.third_person:
		# the shoulder view: the card goes where the crosshair is (the finger turns the view), a tap too; the X puts it back. A heal tapped
		# without turning heals yourself, as everywhere (docs/GDD.md "Heal: a quick tap heals yourself"): it is a tap, not a cast at the crosshair
		var cancel: bool = over_hand and not on_card
		if on_card and not cancel and _is_heal(slot):
			input.card_drag(true, null)
			return
		input.card_drag(false, null if cancel else crosshair_point(), true)
		return
	if hud.hand is AbilityArc:
		# Wild Rift aiming: the drag from the button is the way and share of the card's range from the kid; a tap or the cancel button aims nowhere
		var aim: Variant = null if over_hand or on_card else arc_aim_point(slot, (hud.hand as AbilityArc).aim_way)
		input.card_drag(on_card, aim, true)
		return
	var point: Variant = null if over_hand else street_point(at)
	var inside: bool = false
	if point is Vector2:
		var pt: Vector2 = point as Vector2
		inside = layout.in_arena(pt, 0.0) if layout.arena_radius > 0.0 else layout.lane_rect().has_point(pt)
	input.card_drag(on_card, point, inside)


## True when the card in `slot` of your hand heals.
func _is_heal(slot: int) -> bool:
	var state: PlayerState = sim.players[local_id] if sim.players.has(local_id) else null
	if state == null or slot < 0 or slot >= state.weapons.size():
		return false
	var wid: StringName = state.weapons[slot]
	var def: WeaponDef = sim.weapon_defs[wid] if sim.weapon_defs.has(wid) else null
	return def != null and def.kind == WeaponDef.Kind.HEAL


## The shoulder view: the street point under the crosshair, kept inside the arena. Looking over the horizon (or so far the point is past the
## arena) the card goes `CameraRules.aim_reach` metres along the view from the kid instead.
func crosshair_point() -> Vector2:
	var state: PlayerState = sim.players[local_id] if sim.players.has(local_id) else null
	var from: Vector2 = state.position if state != null else Vector2.ZERO
	var hit: Variant = street_point(_camera.crosshair())
	var lane: Rect2 = layout.lane_rect()
	var point: Vector2
	var hit_valid: bool = false
	if hit is Vector2:
		var candidate: Vector2 = hit as Vector2
		if layout.arena_radius > 0.0:
			hit_valid = candidate.length() <= layout.arena_radius + _camera.rules.shoulder_bounds_edge
		else:
			hit_valid = lane.grow(_camera.rules.shoulder_bounds_edge).has_point(candidate)
	if hit_valid:
		point = hit as Vector2
	else:
		point = from + _camera.ground_axes()[1] * _camera.rules.aim_reach
	if layout.arena_radius > 0.0:
		return layout.clamp_to_arena(point, rules.player_radius)
	return point.clamp(lane.position, lane.end)


## The street point a drag of `way` (screen, at most 1 long) on the ability arc aims the card in `slot` at: that share of the card's range from
## your kid, along that way on the street, kept inside the arena.
func arc_aim_point(slot: int, way: Vector2) -> Vector2:
	var state: PlayerState = sim.players[local_id] if sim.players.has(local_id) else null
	if state == null:
		return Vector2.ZERO
	var def: WeaponDef = null
	if slot >= 0 and slot < state.weapons.size() and sim.weapon_defs.has(state.weapons[slot]):
		def = sim.weapon_defs[state.weapons[slot]]
	var reach: float = def.max_range if def != null else 0.0
	var point: Vector2 = state.position + _camera.ground_way(way) * reach
	if layout.arena_radius > 0.0:
		return layout.clamp_to_arena(point, rules.player_radius)
	var lane: Rect2 = layout.lane_rect()
	return point.clamp(lane.position, lane.end)


func card_released(slot: int, at: Vector2, on_card: bool, over_hand: bool) -> void:
	card_moved(slot, at, on_card, over_hand)
	var kind: MatchInput.Release = input.card_release(Time.get_ticks_msec())
	hud.note_released(slot, kind == MatchInput.Release.CANCEL)
	if kind == MatchInput.Release.CANCEL:
		_sfx.play("card_cancel")  # dropped back on the hand


func card_cancelled(_slot: int) -> void:
	input.card_cancel()
	_sfx.play("card_cancel")


## The base side the local team defends this set (+Z = own/bottom at the start).
func own_side() -> int:
	if sim.mode is Patintero:
		# like Agawang Base the camera turns round so that your side is at the bottom: the runners run up from the base at the bottom; the
		# guards (whose runners come from the other end) see it from the far end
		var me: PlayerState = sim.players[local_id] if sim.players.has(local_id) else null
		return MapLayout.SIDE_OWN if me == null or sim.mode.is_attacker(me) else MapLayout.SIDE_ENEMY
	if sim.mode is TumbangPreso:
		# the taya sees the street from the far end, the throwers coming at it from the top; a thrower throws up the street
		return MapLayout.SIDE_ENEMY if (sim.mode as TumbangPreso).taya_id == local_id else MapLayout.SIDE_OWN
	return sim.side_for_team(local_team)


## The end the street is seen from on screen now: `own_side()`, except while the camera swings round (Tumbang Preso's taya turn),
## when it is the end the turn has passed halfway to. The controls and the map's dressing follow this.
func shown_side() -> int:
	if not _camera.is_turning():
		return own_side()
	return MapLayout.SIDE_ENEMY if _camera.shown_flip() else MapLayout.SIDE_OWN


func show_banner(text: String) -> void:
	hud.show_banner(text)


func banner_text() -> String:
	return hud.banner_text()


func overhead() -> OverheadHud:
	return hud.overhead


func scoreboard() -> Scoreboard:
	return hud.scoreboard


func controls_active() -> bool:
	return hud.controls_active()


## True while the weapon pick is open, or (online) while waiting for the server
## to start play after our pick.
func is_picking() -> bool:
	return _pick_screen.is_open() or _opening_pick


## Tests: jump past the opening show straight into play.
func skip_intro() -> void:
	if sim.phase == MatchSim.Phase.INTRO:
		sim.intro_left = 0.0
		sim.phase = MatchSim.Phase.PLAYING
		sim.intro_finished.emit()


## Ends the open pick now (auto-fills empty slots), e.g. for tests or on a rescue.
func finish_pick() -> void:
	_pick_screen.force_finish()


## Opens the swap screen (only while tagged; reopen as often as you like).
func open_swap() -> void:
	if not sim.players[local_id].tagged:
		return
	_open_swap_screen()


## Runs as many fixed sim ticks as `delta` allows, then updates the visuals.
func advance(delta: float) -> void:
	_pick_screen.tick(delta)
	hud.sim_held = _held_still()
	if online:
		client.interpolate()
		_try_reconnect(delta)
	if _opening_pick:
		_presentation.sync(delta)
		return
	if _held_still():
		if not _hold_ticked:
			# one neutral tick on the way in, so a held aim is cancelled before the match stands still
			_hold_ticked = true
			_step_once(rules.tick_dt())
		_presentation.sync(delta)
		return
	_hold_ticked = false
	_accumulator += delta
	var dt: float = rules.tick_dt()
	var steps: int = 0
	while _accumulator >= dt and steps < MAX_STEPS_PER_FRAME:
		_step_once(dt)
		_accumulator -= dt
		steps += 1
	if steps == MAX_STEPS_PER_FRAME:
		_accumulator = 0.0
	blend.set_progress(_accumulator, dt)
	_sync_camera_locomotion(delta)
	_presentation.sync(delta)


func _sync_camera_locomotion(delta: float) -> void:
	if _camera == null or _camera.rules == null or not _camera.rules.third_person or sim == null or not sim.players.has(local_id):
		return
	var player: PlayerState = sim.players[local_id]
	var is_low: bool = not player.tagged and (player.crouching or player.slide_left > 0.0)
	var air_height: float = 0.0 if player.tagged else player.air_y
	_camera.sync_posture(is_low, air_height, delta)
	if sim.mode != null or sim.phase != MatchSim.Phase.PLAYING or player.tagged:
		_suppress_idle_behind = false
		return
	var screen_move: Vector2 = _pc_input.last_screen_way if _pc_input.last_screen_way != Vector2.ZERO else _stick_screen_way
	var backpedaling: bool = screen_move.y > 0.35
	var moving_forward_or_side: bool = (screen_move != Vector2.ZERO and not backpedaling) \
		or player.has_move_target or player.dash_left > 0.0 or player.slide_left > 0.0
	if backpedaling:
		_suppress_idle_behind = true
	elif moving_forward_or_side:
		_suppress_idle_behind = false
	if input.primary_held or input.card_held() or backpedaling or (_suppress_idle_behind and not moving_forward_or_side):
		_camera.tick_manual_look(delta)
		return
	_camera.sync_behind_character(player.facing, moving_forward_or_side or input.stick != Vector2.ZERO, delta)


## Offline, the match stands still while the match menu or the briefing is open (a match against the clock of bots
## does not wait for nobody online, so online play keeps running).
func _held_still() -> bool:
	return not online and (hud.menu.visible or hud.briefing.visible)


# ---- setup ---------------------------------------------------------------------------

func _setup_practice() -> void:
	if Session.practice_kind == &"match":
		rules = rules.with_bot_level(Settings.bot_level)  # a copy with the chosen difficulty's bot numbers; the shared rules stay as they are
	sim = MatchSim.new(rules, layout, Time.get_ticks_usec())
	if Session.practice_kind == &"match":
		var game: StreetModeRules = StreetMode.rules_for(Session.practice_game)
		if game != null:
			_setup_street_game(game)
		else:
			_setup_bot_match()
		return
	sim.add_player(local_id, local_team)
	var stand_spot: Vector2 = Vector2(rules.dummy_stand_x, rules.dummy_z)
	var patrol_spot: Vector2 = Vector2(rules.dummy_patrol_x, rules.dummy_z)
	var patrol: DummyBrain = DummyBrain.patrolling(rules.dummy_patrol_x, rules.dummy_patrol_range, rules.dummy_patrol_speed_scale)
	# the first sparring partners are the patrol dummy, then the standing one (`practice_fighters`)
	sim.add_dummy(ENEMY_STAND_ID, 1, stand_spot, FightBrain.fighter(stand_spot) if rules.practice_fighters >= 2 else DummyBrain.standing())
	sim.add_dummy(ENEMY_PATROL_ID, 1, patrol_spot, FightBrain.fighter(patrol_spot) if rules.practice_fighters >= 1 else patrol)
	var ally_spot: Vector2 = Vector2(rules.dummy_ally_x, rules.dummy_ally_z)
	sim.add_dummy(ALLY_ID, local_team, ally_spot, DummyBrain.rescuer(ally_spot))
	sim.add_dummy(ENEMY_RESCUE_ID, 1, layout.rescue_rect(sim.side_for_team(1)).get_center(), DummyBrain.standing())
	sim.assign_characters()
	sim.set_character(local_id, StringName(Session.avatar_id))
	sim.equip(local_id)
	for id: int in [ENEMY_STAND_ID, ENEMY_PATROL_ID]:
		if sim.is_fighter(id):
			sim.equip(id, true)


## Five kids a side (5v5, 10 unique kids): you and four allies against five enemies, all real players of the sim.
## The allies back you up (they attack when you do, defend and rescue otherwise); the enemies wait, then attack (see TeamCoach).
func _setup_bot_match() -> void:
	sim.add_player(local_id, local_team)
	var allies: TeamCoach = TeamCoach.new(local_team, local_id)
	var foes: TeamCoach = TeamCoach.new(1 - local_team)
	for id: int in BOT_ALLY_IDS:
		sim.add_player(id, local_team)
		allies.add(sim, id, rules.bot_skill)
	for id: int in BOT_ENEMY_IDS:
		sim.add_player(id, 1 - local_team)
		foes.add(sim, id, rules.bot_skill)
	_coaches = [allies, foes]
	_assign_unique_roster()
	sim.equip(local_id)
	for id: int in BOT_ALLY_IDS + BOT_ENEMY_IDS:
		sim.equip(id, true)


## Deals 10 unique Filipino kids across all 10 players in 5v5 when the roster has enough characters.
func _assign_unique_roster() -> void:
	if rules.characters.size() < sim.players.size():
		sim.assign_characters()
		sim.set_character(local_id, StringName(Session.avatar_id))
		return
	var pool: Array[StringName] = []
	for character: CharacterDef in rules.characters:
		pool.append(character.id)
	for i: int in range(pool.size() - 1, 0, -1):
		var j: int = sim.rng.randi_range(0, i)
		var held: StringName = pool[i]
		pool[i] = pool[j]
		pool[j] = held
	var wanted: StringName = StringName(Session.avatar_id)
	var wanted_idx: int = pool.find(wanted)
	if wanted_idx > 0:
		pool[wanted_idx] = pool[0]
		pool[0] = wanted
	var cursor: int = 0
	for id: int in sim.players:
		sim.players[id].character_id = pool[cursor]
		cursor += 1


## A street game in Practice. Patintero: you and two bot allies against three bots; your team runs first. Tumbang Preso: every kid plays
## for itself (all on one team): you and five bots, one of them the taya to begin with; Langit Lupa the same.
func _setup_street_game(game: StreetModeRules) -> void:
	var for_all: bool = StreetMode.create(game).is_free_for_all()  # Tumbang Preso, Langit Lupa
	sim.add_player(local_id, local_team)
	_mode_coach = StreetModeCoach.new()
	for id: int in STREET_ALLY_IDS:
		sim.add_player(id, local_team)
		_mode_coach.add(id)
	for id: int in STREET_ENEMY_IDS:
		sim.add_player(id, local_team if for_all else 1 - local_team)
		_mode_coach.add(id)
	sim.assign_characters()
	sim.set_character(local_id, StringName(Session.avatar_id))
	sim.start_mode(StreetMode.create(game))  # no skills in these games: nobody is dealt a hand


func _setup_online() -> void:
	if client == null:
		client = Net.client
	sim = client.mirror
	local_id = client.player_id
	local_team = sim.players[local_id].team


# ---- one tick of input ---------------------------------------------------------------

func _step_once(dt: float) -> void:
	if online:
		if _resume_rejected or _awaiting_reconnect_welcome or not client.is_online():
			return
		_input_tick += 1
		client.send_input(_local_input(_input_tick), dt)
		return
	for coach: TeamCoach in _coaches:
		coach.drive(sim)
	if _mode_coach != null:
		_mode_coach.drive(sim)
	sim.set_input(local_id, _local_input(sim.tick))
	sim.step(dt)
	blend.record(sim)


## A menu, a pause or the pick screen drops what is held: a held aim must cancel rather than cast.
func _local_input(tick: int) -> PlayerInput:
	if not _application_focused or _application_paused or not controls_active() or is_picking() or hud.menu.visible:
		_pc_input.reset(input)
		input.clear()
		_stick_screen_way = Vector2.ZERO
		_stop_tilt()
		return PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_AIM_CANCEL, tick)
	steer_by_tilt(TiltInput.gravity())
	if _stick_screen_way != Vector2.ZERO:
		input.stick = _camera.ground_way(_stick_screen_way)
	if sim.mode == null and _camera.rules.third_person:
		_pc_input.poll_frame(input, _camera.ground_way, crosshair_point(), true)
	if _camera.rules.third_person and input.card_held() and input.over_street:
		# the shoulder view: a held card follows the crosshair every tick (the kid walks, the view turns), not only when the finger moves
		input.card_drag(false, crosshair_point(), true)
	return input.build(tick)


## Tilt to run, once a tick: a lean past the dead zone puts the move target `TILT_AHEAD` pixels from the kid that way on screen and keeps
## it there (the follow-the-finger path, so the kid runs at its one speed and every rule of walking applies); back to level, the target
## comes to where the kid stands, so it stops. A finger already on the street wins until it lets go; a tagged kid waits.
func steer_by_tilt(reading: Vector3) -> void:
	if not Settings.tilt_to_run or not sim.players.has(local_id) or sim.mode != null:
		_stop_tilt()  # (the street games are steered with the knob)
		return
	var state: PlayerState = sim.players[local_id]
	if state.tagged or (input.move_held and not tilt_steering):
		tilt.reset()
		tilt_steering = false
		return
	var way: Vector2 = tilt.direction(reading)
	var here: Vector2 = _camera.screen_position(Vector3(state.position.x, 0.0, state.position.y))
	if way != Vector2.ZERO:
		var point: Variant = street_point(here + way * TILT_AHEAD)
		if not point is Vector2:
			return
		# a fresh target every tick (not a drag), so it does not depend on GameRules.move_follow_hold
		input.move_press(point as Vector2)
		tilt_steering = true
	elif tilt_steering:
		input.move_press(state.position)
		input.move_release()
		tilt_steering = false


## Tilt lets go of the move target and takes the next reading as level.
func _stop_tilt() -> void:
	if tilt_steering:
		input.move_release()
	tilt_steering = false
	tilt.reset()


## The camera is turned around when the local team defends the -Z base (as shown: the controls swap halfway through a turn).
func _flip() -> bool:
	return shown_side() == MapLayout.SIDE_ENEMY


# ---- Tumbang Preso's throw -----------------------------------------------------------------

## The Throw button was pressed: aim at the can, and the hold starts the line.
func throw_pressed() -> void:
	var game: TumbangPreso = sim.mode as TumbangPreso
	if game == null or not sim.players.has(local_id):
		return
	_throw_angle = _angle_of(game.aim_at_can(sim.players[local_id].position))
	input.throw_press(_way_of(_throw_angle))


## The finger dragged `drag` pixels: the part of it across the throw line on the screen (toward where +X is shown: right on the portrait
## street, down on the landscape one) turns the aim by that much times the sensitivity of the line's length now (fine while it is short,
## fast when it is full), within the arc the rules allow.
func throw_dragged(drag: Vector2) -> void:
	var game: TumbangPreso = sim.mode as TumbangPreso
	if game == null or not input.throw_held:
		return
	var turn: float = drag.dot(_camera.screen_way(Vector2.RIGHT)) * game.sensitivity_at(game.charge(local_id))
	var arc: float = deg_to_rad(game.rules.aim_arc_degrees)
	_throw_angle = clampf(_throw_angle + turn, -arc, arc)
	input.throw_turn(_way_of(_throw_angle))


func throw_released(cancel: bool) -> void:
	input.throw_release(cancel)


## The movement knob or the joystick points `way` on screen (up the screen is -1): the way on the street that is, however the camera is turned.
func knob_moved(way: Vector2) -> void:
	_stick_screen_way = way
	input.stick = _camera.ground_way(way)


func jump_pressed() -> void:
	input.press_jump()


func crouch_changed(held: bool) -> void:
	input.set_crouch(held)


func slide_pressed() -> void:
	input.press_slide()


func _ensure_locomotion_hud_signals() -> void:
	if hud.has_signal(&"jump_pressed") and hud.get_signal_connection_list(&"jump_pressed").is_empty():
		hud.connect(&"jump_pressed", jump_pressed)
	if hud.has_signal(&"crouch_changed") and hud.get_signal_connection_list(&"crouch_changed").is_empty():
		hud.connect(&"crouch_changed", crouch_changed)
	if hud.has_signal(&"slide_pressed") and hud.get_signal_connection_list(&"slide_pressed").is_empty():
		hud.connect(&"slide_pressed", slide_pressed)


## The Kick or Dive button: one tap.
func action_pressed() -> void:
	input.kick_press()


## The angle (radians, 0 = straight up the street toward the far end, positive = toward +X) of a way in sim XZ, and back.
static func _angle_of(way: Vector2) -> float:
	return atan2(way.x, -way.y)


static func _way_of(angle: float) -> Vector2:
	return Vector2(sin(angle), -cos(angle))


# ---- weapon pick and swap ------------------------------------------------------------

func _available_weapon_ids() -> Array[StringName]:
	var ids: Array[StringName] = []
	for def: WeaponDef in rules.weapons:
		ids.append(def.id)
	return ids


func _open_pick(heading: String, current: Array[StringName]) -> void:
	var slots: int = clampi(rules.loadout_size, 2, 3)
	var mine: StringName = sim.players[local_id].character_id if online else StringName(Session.avatar_id)
	if not rules.characters.any(func(c: CharacterDef) -> bool: return c.id == mine):
		mine = sim.players[local_id].character_id
	var available: Array[StringName] = _available_weapon_ids()
	_pick_screen.hosting = online and Net.host != null
	_pick_screen.cards_mode = rules.card_cycle or sim.mode != null
	var remembered: Array[StringName] = Session.loadout_for(available, slots) if current.is_empty() else current
	_pick_screen.open(WeaponPick.new(available, rules.weapon_pick_time, remembered, slots), rules.weapons, heading, sim.rng, rules.characters, mine, not online)
	_set_controls_active(false)
	if rules.card_cycle:
		_pick_screen.force_finish()


func _open_swap_screen() -> void:
	if rules.card_cycle or sim.mode != null:
		return
	var slots: int = clampi(rules.loadout_size, 2, 3)
	var player: PlayerState = sim.players[local_id]
	var available: Array[StringName] = _available_weapon_ids()
	_pick_screen.open_swap(WeaponPick.new(available, 0.0, player.weapons, slots), rules.weapons, "CHANGE WEAPONS")
	_set_controls_active(false)


func _on_pick_confirmed(picks: Array[StringName]) -> void:
	var has_weapons: bool = not rules.card_cycle and sim.mode == null
	if has_weapons and picks.size() >= 2:
		Session.remember_loadout(picks)
		sim.set_loadout_list(local_id, picks)
	var prev_kid: StringName = sim.players[local_id].character_id
	var kid: StringName = _pick_screen.character_id
	if kid != &"":
		sim.set_character(local_id, kid)
		if not online and not _coaches.is_empty() and rules.characters.size() >= sim.players.size():
			for other_id: int in sim.players:
				if other_id != local_id and sim.players[other_id].character_id == kid:
					sim.players[other_id].character_id = prev_kid
					break
		Session.avatar_id = String(kid)
		Session.save()
		_presentation.refresh_kids()
	if online:
		var first_w: StringName = picks[0] if picks.size() >= 1 else &""
		var second_w: StringName = picks[1] if picks.size() >= 2 else &""
		var third_w: StringName = picks[2] if picks.size() >= 3 else &""
		client.pick_weapons(first_w, second_w, kid, third_w)
		if client.stage != "play":
			show_banner("Waiting for your friends…")
			return
	_opening_pick = false
	sim.begin_intro()
	_set_controls_active(true)
	if Session.briefing_pending and not online:
		Session.briefing_pending = false
		hud.briefing.start(StreetMode.rules_for(Session.practice_game), rules.briefing_seconds)
	_presentation.sync(0.0)


func _on_swapped(picks: Array[StringName]) -> void:
	if picks.size() < 2:
		return
	Session.remember_loadout(picks)
	if online:
		var third_w: StringName = picks[2] if picks.size() >= 3 else &""
		client.swap_weapons(picks[0], picks[1], third_w)
		sim.players[local_id].weapons.assign(picks)
	else:
		sim.swap_loadout_list(local_id, picks)
	_presentation.sync(0.0)


## The touch controls read raw touches, so they are switched off under the pick
## screen; otherwise tapping a weapon card could also press the button beneath it.
func _set_controls_active(active: bool) -> void:
	hud.set_controls_active(active)
	if not active:
		_pc_input.reset(input)
		input.clear()
		_stick_screen_way = Vector2.ZERO
	# however the phone is held when the controls come back is level
	_stop_tilt()


func _cancel_local_input() -> void:
	if is_node_ready():
		hud.cancel_controls()
	_pc_input.reset(input)
	input.clear()
	_stick_screen_way = Vector2.ZERO
	input.pending_buttons = PlayerInput.BTN_AIM_CANCEL


## Online: the server ended the weapon pick (loadouts are final) and play starts.
func _on_stage_changed(stage: String) -> void:
	if stage != "play" or not _opening_pick:
		return
	_pick_screen.dismiss()
	_presentation.refresh_kids()
	_opening_pick = false
	_set_controls_active(true)


# ---- online connection ---------------------------------------------------------------

## The connection dropped (phone locked, network switch...). Keep trying; the
## server holds our slot for 60 s and the token gets it back.
func _on_connection_changed(is_up: bool) -> void:
	if is_up:
		_reconnect_left = -1.0
		if _awaiting_reconnect_welcome:
			hud.set_connection("Restoring your battle…")
		return
	if _resume_rejected:
		return
	_cancel_local_input()
	hud.set_connection("Connection lost\nReconnecting…")
	_awaiting_reconnect_welcome = true
	_resume_rejected = false
	_reconnect_left = 0.0


func _on_welcomed(reconnected: bool) -> void:
	if not _awaiting_reconnect_welcome:
		return
	_awaiting_reconnect_welcome = false
	if reconnected:
		hud.set_connection("")
		_set_controls_active(not is_picking() and not hud.menu.visible)
		show_banner("Back online!")
		return
	_resume_rejected = true
	_set_controls_active(false)
	hud.set_connection("Battle unavailable", true)


## Online: someone else dropped out of the match, came back, or has been gone so long they are away (AFK). The server keeps a
## dropped player's slot for a while and ends the match when a whole team is away.
func _on_room_changed(room: Dictionary) -> void:
	var gone: PackedStringArray = PackedStringArray()
	var others: int = 0
	var members_raw: Variant = room.get("members", [])
	var members: Array = members_raw if members_raw is Array else []
	for item: Variant in members:
		if not (item is Dictionary):
			continue
		var entry: Dictionary = item
		var id: int = int(entry.get("player_id", -1))
		var connected: bool = bool(entry.get("connected", true))
		var afk: bool = bool(entry.get("afk", false))
		var name_text: String = String(entry.get("name", ""))
		if not name_text.is_empty():
			hud.overhead.names[id] = name_text
		var known: bool = _connected.has(id)
		var was: bool = _connected[id] if known else connected
		var was_afk: bool = hud.overhead.away_ids.has(id)
		_connected[id] = connected
		if afk:
			hud.overhead.away_ids[id] = true
		else:
			hud.overhead.away_ids.erase(id)
		if int(entry.get("team", -1)) == 1 - local_team:
			others += 1
			if afk:
				gone.append(name_text)
		if id == local_id or not known:
			continue
		if afk and not was_afk:
			show_banner("%s is AFK" % name_text)
		elif was != connected:
			show_banner(("%s is back!" if connected else "%s left the battle") % name_text)
	gone_note = "%s is gone for a while" % " and ".join(gone) if others > 0 and gone.size() == others else ""


func _on_room_closed() -> void:
	_resume_rejected = true
	_awaiting_reconnect_welcome = false
	_reconnect_left = -1.0
	_cancel_local_input()
	hud.set_connection(ROOM_CLOSED_TEXT, true)


func _try_reconnect(delta: float) -> void:
	if _reconnect_left < 0.0 or client.is_online() or client.is_connecting():
		return
	_reconnect_left -= delta
	if _reconnect_left <= 0.0:
		_reconnect_left = RECONNECT_INTERVAL
		var url: String = Session.reconnect_url if not Session.reconnect_url.is_empty() else Settings.server_url
		client.connect_to(url, not Session.lobby_hotspot)


## Leaving a Practice match against bots that has really started counts as a loss, so History does not forget it.
func record_if_abandoned() -> bool:
	if Session.practice_kind != &"match" or sim == null or sim.phase == MatchSim.Phase.MATCH_OVER or sim.match_time < ABANDON_AFTER:
		return false
	var points: Array[int] = sim.score.points
	MatchHistory.record("practice", false, points[local_team], points[1 - local_team])
	return true


## Online: a player (you too) sent a quick chat sticker: it shows over their kid.
func _on_said(player: int, sticker: int) -> void:
	if sticker >= 0 and sticker < QuickChat.PHRASES.size():
		hud.overhead.say(player, QuickChat.PHRASES[sticker])


## Settings menu > Exit match: online, give the room slot back first.
func _on_exit_pressed() -> void:
	if online:
		client.leave_room()
	else:
		record_if_abandoned()
	get_tree().change_scene_to_file(TITLE_SCENE)
