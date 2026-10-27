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
## A free enemy standing in its own rescue area (bottom right), a third target.
const ENEMY_RESCUE_ID: int = 5
## Practice match vs bots: your two allies and the three enemies (real players of the sim, driven by TeamCoach).
const BOT_ALLY_IDS: Array[int] = [2, 3]
const BOT_ENEMY_IDS: Array[int] = [4, 5, 6]
const MAX_STEPS_PER_FRAME: int = 8
const RECONNECT_INTERVAL: float = 2.0
const TITLE_SCENE: String = "res://scenes/ui/title_screen.tscn"
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
var _hold_ticked: bool = false

@onready var hud: MatchHud = %Hud
@onready var _camera: ArenaCamera = %ArenaCamera
@onready var _actors: Node3D = %Actors
@onready var _fx: WeaponFxView = %WeaponFx
@onready var _aim_indicator: AimIndicator = %AimIndicator
@onready var _pick_screen: WeaponPickScreen = %PickScreen
@onready var _map: StreetMap = %Map


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
		client.room_changed.connect(_on_room_changed)
		client.room_closed.connect(_on_room_closed)
		_on_room_changed(client.room)
		client.loaded()
		if client.stage == "play":
			_on_stage_changed("play")
	_presentation.sync(0.0)


func _process(delta: float) -> void:
	advance(delta)


## A tap anywhere skips the opening show (offline: online the server runs it for everyone).
func _input(event: InputEvent) -> void:
	if online or sim == null or sim.phase != MatchSim.Phase.INTRO or is_picking():
		return
	if event is InputEventScreenTouch and (event as InputEventScreenTouch).pressed:
		skip_intro()


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


## The phone's back button opens the match settings (Resume, Leave). The pick screen, the walkthrough and the settings take the
## press themselves; the results card has its own buttons. Deferred, so they still read as open here (see MenuDialog).
func _back_pressed() -> void:
	if is_picking() or hud.menu.visible or hud.coach.visible or sim.phase == MatchSim.Phase.MATCH_OVER:
		return
	hud.toggle_menu()


func _exit_tree() -> void:
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
		hud.marker.ping(point as Vector2)


func move_released() -> void:
	input.move_release()


## A card in `slot` was picked up from the hand.
func card_pressed(slot: int) -> void:
	input.card_press(slot, Time.get_ticks_msec())


## The held card's finger is at screen point `at` (on its own card, over the hand).
func card_moved(_slot: int, at: Vector2, on_card: bool, over_hand: bool) -> void:
	input.card_drag(on_card, null if over_hand else street_point(at))


## The held card was let go at screen point `at`: a cast there, a tap, or a drop back on the hand (MatchInput decides).
func card_released(slot: int, at: Vector2, on_card: bool, over_hand: bool) -> void:
	card_moved(slot, at, on_card, over_hand)
	var kind: MatchInput.Release = input.card_release(Time.get_ticks_msec())
	hud.note_released(slot, kind == MatchInput.Release.CANCEL)


func card_cancelled(_slot: int) -> void:
	input.card_cancel()


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


## Opens the swap screen (only while tagged; reopen as often as you like).
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
	_presentation.sync(delta)


## Offline, the match stands still while the match menu or the first-run walkthrough is open (a match against the clock of bots
## does not wait for nobody online, so online play keeps running).
func _held_still() -> bool:
	return not online and (hud.menu.visible or hud.coach.visible)


# ---- setup ---------------------------------------------------------------------------

func _setup_practice() -> void:
	sim = MatchSim.new(rules, layout, Time.get_ticks_usec())
	sim.tag_release_time = rules.practice_tag_release_time
	if Session.practice_kind == &"match":
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


## Three kids a side: you and two allies against three enemies, all real players of the sim. The allies back you up (they attack
## when you do, defend and rescue otherwise); the enemies wait, then attack (see TeamCoach).
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
	sim.assign_characters()
	sim.set_character(local_id, StringName(Session.avatar_id))
	sim.equip(local_id)
	for id: int in BOT_ALLY_IDS + BOT_ENEMY_IDS:
		sim.equip(id, true)


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
	sim.set_input(local_id, _local_input(sim.tick))
	sim.step(dt)


## A menu, a pause or the pick screen drops what is held: a held aim must cancel rather than cast.
func _local_input(tick: int) -> PlayerInput:
	if not _application_focused or _application_paused or not controls_active() or is_picking() or hud.menu.visible:
		input.clear()
		return PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_AIM_CANCEL, tick)
	return input.build(tick)


## The camera is turned around when the local team defends the -Z base.
func _flip() -> bool:
	return own_side() == MapLayout.SIDE_ENEMY


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
	for entry: Dictionary in room.get("members", []) as Array:
		var id: int = entry.get("player_id", -1) as int
		var connected: bool = entry.get("connected", true) as bool
		var afk: bool = entry.get("afk", false) as bool
		var name: String = entry.get("name", "") as String
		var known: bool = _connected.has(id)
		var was: bool = _connected.get(id, connected)
		var was_afk: bool = hud.overhead.away_ids.has(id)
		_connected[id] = connected
		if afk:
			hud.overhead.away_ids[id] = true
		else:
			hud.overhead.away_ids.erase(id)
		if entry.get("team", -1) == 1 - local_team:
			others += 1
			if afk:
				gone.append(name)
		if id == local_id or not known:
			continue
		if afk and not was_afk:
			show_banner("%s is AFK" % name)
		elif was != connected:
			show_banner(("%s is back!" if connected else "%s left the battle") % name)
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
		client.connect_to(url)


## Leaving a Practice match against bots that has really started counts as a loss, so History does not forget it.
func record_if_abandoned() -> bool:
	if Session.practice_kind != &"match" or sim == null or sim.phase == MatchSim.Phase.MATCH_OVER or sim.match_time < ABANDON_AFTER:
		return false
	var points: Array[int] = sim.score.points
	MatchHistory.record("practice", false, points[local_team], points[1 - local_team])
	return true


## Settings menu > Exit match: online, give the room slot back first.
func _on_exit_pressed() -> void:
	if online:
		client.leave_room()
	else:
		record_if_abandoned()
	get_tree().change_scene_to_file(TITLE_SCENE)
