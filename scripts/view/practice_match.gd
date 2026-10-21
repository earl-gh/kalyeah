class_name PracticeMatch
extends Node3D
## The match screen. Offline practice runs a local MatchSim with training dummies;
## online (`online = true`, scenes/match/online.tscn) it draws the GameClient's
## mirror of the server's match and sends inputs instead of stepping a sim.
## It wires the pieces together: MatchInput (devices -> PlayerInput), MatchHud
## (all HUD controls), the 3D views, audio and the camera. Reads sim state only;
## all rules live in scripts/sim/.

const LOCAL_ID: int = 1
const LOCAL_TEAM: int = 0
const ENEMY_STAND_ID: int = 2
const ENEMY_PATROL_ID: int = 3
const ALLY_ID: int = 4
## A free enemy standing in its own tagged box (bottom right), to see its bar hide (owner).
const ENEMY_BOX_ID: int = 5
const MAX_STEPS_PER_FRAME: int = 8
const WEAPON_SLOTS: int = 2
const RECONNECT_INTERVAL: float = 2.0
## Practice-only debug button to test getting tagged without an enemy that fights back.
const DEBUG_DAMAGE: int = 30
## How long a status from the practice menu lasts (seconds), long enough to watch its animation.
const STATUS_TEST_TIME: float = 3.0
const TITLE_SCENE: String = "res://scenes/ui/title_screen.tscn"

@export var rules: GameRules
@export var layout: MapLayout
## Online match: driven by Net.client (set `client` before adding to the tree to
## use another one, e.g. in tests).
@export var online: bool = false

var sim: MatchSim
var client: GameClient
var local_id: int = LOCAL_ID
var local_team: int = LOCAL_TEAM
var input: MatchInput = MatchInput.new()
var _accumulator: float = 0.0
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
var _application_focused: bool = true
var _application_paused: bool = false
var _presentation: PracticeMatchPresentation

@onready var hud: MatchHud = %Hud
@onready var _camera: ArenaCamera = %ArenaCamera
@onready var _actors: Node3D = %Actors
@onready var _fx: WeaponFxView = %WeaponFx
@onready var _aim_indicator: AimIndicator = %AimIndicator
@onready var _pick_screen: WeaponPickScreen = %PickScreen
@onready var _map: StreetMap = %Map
@onready var _audio: MatchAudio = %Audio


func _ready() -> void:
	if online:
		_setup_online()
	else:
		_setup_practice()
	_presentation = PracticeMatchPresentation.new(self)
	for character: CharacterDef in rules.characters:
		_characters[character.id] = character
	for id: int in sim.players:
		_presentation.make_actor_view(sim.players[id])
	_fx.watch(sim)
	_audio.watch(sim, local_team, local_id)
	_views[local_id].footstep.connect(func() -> void: _audio.play(&"footstep", 0.15))
	_map.set_own_side(own_side())
	_map.show_back_button(false)
	_presentation.setup_hud()
	_presentation.connect_hud()
	_presentation.connect_sim()
	_pick_screen.exit_requested.connect(_on_exit_pressed)
	_pick_screen.confirmed.connect(_on_pick_confirmed)
	_pick_screen.swapped.connect(_on_swapped)
	_pick_screen.closed.connect(_set_controls_active.bind(true))
	_open_pick("PICK A KID + 2 WEAPONS", [])
	_opening_pick = true
	if online:
		client.stage_changed.connect(_on_stage_changed)
		client.picks_changed.connect(func() -> void: _pick_screen.set_taken(client.taken_kids()))
		client.connection_changed.connect(_on_connection_changed)
		client.welcomed.connect(_on_welcomed)
		client.loaded()
		if client.stage == "play":
			_on_stage_changed("play")
	_presentation.sync(0.0)


func _process(delta: float) -> void:
	advance(delta)


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
		_:
			return
	_cancel_local_input()
	# Android can stop rendering now; send cancellation before the next frame.
	if is_node_ready() and online and client.is_online():
		_step_once(0.0)


func _exit_tree() -> void:
	if online and client != null and client.in_match():
		client.leave_room()
		Session.reconnect_token = ""
		Session.save()


# ---- public API (used by tests and the HUD) ---------------------------------------

## The movement stick (the on-screen dpad, tests).
func set_stick(value: Vector2) -> void:
	input.set_stick(value)


## A skill button was pressed; it is sent with the next sim tick.
func press_skill(button_bit: int) -> void:
	input.press_skill(button_bit)


## A weapon button started aiming.
func aim_started(slot: int) -> void:
	input.aim_started(slot)


## A weapon button was let go with a screen-space `aim` (zero = auto-aim).
func aim_released(aim: Vector2, cancelled: bool, slot: int) -> void:
	input.aim_released(_screen_to_world(aim), cancelled, slot)


## Practice menu: plays the animation a skill hit causes (stunned, airborne, knocked down) on the
## local kid by giving it that status for a few seconds, and closes the menu to watch it.
func play_status_test(type: int) -> void:
	if online:
		return
	sim.players[local_id].effects.apply(type as StatusEffects.Type, STATUS_TEST_TIME)
	hud.menu.close()
	_presentation.sync(0.0)


func hurt_local(amount: int) -> void:
	sim.damage(local_id, amount)
	_presentation.sync(0.0)


## The base side the local team defends this set (+Z = own/bottom at the start).
func own_side() -> int:
	return sim.side_for_team(local_team)


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


## Opens the box swap screen (only while tagged; reopen as often as you like).
func open_swap() -> void:
	if not sim.players[local_id].tagged:
		return
	_open_swap_screen()


## Runs as many fixed sim ticks as `delta` allows, then updates the visuals.
func advance(delta: float) -> void:
	_pick_screen.tick(delta)
	if online:
		client.interpolate()
		_try_reconnect(delta)
	if _opening_pick:
		_presentation.sync(delta)
		return
	_accumulator += delta
	var dt: float = rules.tick_dt()
	var steps: int = 0
	while _accumulator >= dt and steps < MAX_STEPS_PER_FRAME:
		_step_once(dt)
		_accumulator -= dt
		steps += 1
	if steps == MAX_STEPS_PER_FRAME:
		_accumulator = 0.0
	_presentation.sync(delta)


# ---- setup ---------------------------------------------------------------------------

func _setup_practice() -> void:
	sim = MatchSim.new(rules, layout, Time.get_ticks_usec())
	sim.add_player(local_id, local_team)
	sim.add_dummy(ENEMY_STAND_ID, 1, Vector2(rules.dummy_stand_x, rules.dummy_z), DummyBrain.standing())
	var patrol: DummyBrain = DummyBrain.patrolling(
		rules.dummy_patrol_x, rules.dummy_patrol_range, rules.dummy_patrol_speed_scale
	)
	sim.add_dummy(ENEMY_PATROL_ID, 1, Vector2(rules.dummy_patrol_x, rules.dummy_z), patrol)
	var ally_spot: Vector2 = Vector2(rules.dummy_ally_x, rules.dummy_ally_z)
	sim.add_dummy(ALLY_ID, local_team, ally_spot, DummyBrain.rescuer(ally_spot))
	var box_spot: Vector2 = layout.jail_slot(sim.side_for_team(1), 0)
	sim.add_dummy(ENEMY_BOX_ID, 1, box_spot, DummyBrain.standing())
	sim.assign_characters()
	sim.set_character(local_id, StringName(Session.avatar_id))
	var loadout: Array[StringName] = sim.random_loadout()
	sim.set_loadout(local_id, loadout[0], loadout[1])


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
	sim.set_input(local_id, _local_input(sim.tick))
	sim.step(dt)


## Menus consume keyboard intent too; a held aim must cancel rather than cast.
func _local_input(tick: int) -> PlayerInput:
	if not _application_focused or _application_paused or not controls_active() or is_picking() or hud.menu.visible:
		input.clear()
		return PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_AIM_CANCEL, tick)
	return input.build(_flip(), _mouse_aim, tick)


## The camera and stick are turned around when the local team defends the -Z base.
func _flip() -> bool:
	return own_side() == MapLayout.SIDE_ENEMY


func _screen_to_world(screen_aim: Vector2) -> Vector2:
	return LocalInput.to_world(screen_aim, _flip(), input.camera_yaw)


## Mouse position on the ground, relative to the player, scaled by the weapon range.
func _mouse_aim(slot: int) -> Vector2:
	var player: PlayerState = sim.players[local_id]
	var viewport: Viewport = get_viewport()
	if viewport == null or slot >= player.weapons.size():
		return Vector2.ZERO
	var mouse: Vector2 = viewport.get_mouse_position()
	var flat: Vector2 = _camera.flat_position(mouse)
	var hit: Variant = Plane(Vector3.UP, 0.0).intersects_ray(_camera.project_ray_origin(flat), _camera.project_ray_normal(flat))
	if hit == null:
		return Vector2.ZERO
	var point: Vector3 = hit as Vector3
	var offset: Vector2 = Vector2(point.x, point.z) - player.position
	var def: WeaponDef = sim.weapon_defs[player.weapons[slot]]
	var reach: float = def.max_range if def.max_range > 0.0 else 1.0
	return (offset / reach).limit_length(1.0)


# ---- weapon pick and swap ------------------------------------------------------------

func _open_pick(heading: String, current: Array[StringName]) -> void:
	MatchLoadoutFlow.open(self, heading, current)


func _open_swap_screen() -> void:
	MatchLoadoutFlow.open_swap(self)


func _on_pick_confirmed(picks: Array[StringName]) -> void:
	MatchLoadoutFlow.confirm(self, picks)


func _on_swapped(picks: Array[StringName]) -> void:
	MatchLoadoutFlow.swap(self, picks)


## The touch controls read raw touches, so they are switched off under the pick
## screen; otherwise tapping a weapon card could also press the button beneath it.
func _set_controls_active(active: bool) -> void:
	hud.set_controls_active(active)
	if not active:
		input.clear()


func _cancel_local_input() -> void:
	if is_node_ready():
		hud.cancel_controls()
	input.clear()
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
			hud.set_connection("Restoring match…")
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
	hud.set_connection("Match unavailable", true)


func _try_reconnect(delta: float) -> void:
	if _reconnect_left < 0.0 or client.is_online() or client.is_connecting():
		return
	_reconnect_left -= delta
	if _reconnect_left <= 0.0:
		_reconnect_left = RECONNECT_INTERVAL
		var url: String = Session.reconnect_url if not Session.reconnect_url.is_empty() else Settings.server_url
		client.connect_to(url)


## Settings menu > Exit match: online, give the room slot back first.
func _on_exit_pressed() -> void:
	if online:
		client.leave_room()
	get_tree().change_scene_to_file(TITLE_SCENE)
