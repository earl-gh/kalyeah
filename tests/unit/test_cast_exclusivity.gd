extends GutTest
## Regression coverage for server-authoritative one-skill-at-a-time casting.

var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0
const PLAYER_ID: int = 1
const BOTH_WEAPONS: int = PlayerInput.BTN_WEAPON_1 | PlayerInput.BTN_WEAPON_2


func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, 17)
	var player: PlayerState = sim.add_player(PLAYER_ID, 0)
	player.position = Vector2.ZERO
	assert_true(sim.set_loadout(PLAYER_ID, &"papel_shield", &"langit_lupa"))
	return sim


func _send_input(sim: MatchSim, buttons: int) -> void:
	sim.set_input(PLAYER_ID, PlayerInput.create(Vector2.ZERO, Vector2(0.0, -1.0), buttons, sim.tick))
	sim.step(DT)


func test_simultaneous_press_and_release_casts_only_lower_slot() -> void:
	var sim: MatchSim = _sim()
	var player: PlayerState = sim.players[PLAYER_ID]
	_send_input(sim, BOTH_WEAPONS)
	assert_eq(player.aim_hold[0], 0.0)
	assert_eq(player.aim_hold[1], -1.0, "slot 1 never becomes a second active aim")
	_send_input(sim, 0)
	assert_gt(player.weapon_cooldowns[0], 0.0, "the deterministic lower slot casts")
	assert_eq(player.weapon_cooldowns[1], 0.0, "crafted dual input cannot cast both weapons")


func test_release_cannot_start_the_other_slot_in_the_same_input() -> void:
	var sim: MatchSim = _sim()
	var player: PlayerState = sim.players[PLAYER_ID]
	_send_input(sim, PlayerInput.BTN_WEAPON_1)
	_send_input(sim, PlayerInput.BTN_WEAPON_2)
	assert_gt(player.weapon_cooldowns[0], 0.0, "releasing the active slot still completes it")
	assert_eq(player.aim_hold[1], -1.0, "the other fresh press is consumed by arbitration")
	_send_input(sim, PlayerInput.BTN_WEAPON_2)
	_send_input(sim, 0)
	assert_eq(player.weapon_cooldowns[1], 0.0, "holding the rejected press cannot precast")
	_send_input(sim, PlayerInput.BTN_WEAPON_2)
	_send_input(sim, 0)
	assert_gt(player.weapon_cooldowns[1], 0.0, "a later fresh press can cast normally")


func test_sequential_fresh_presses_can_cast_each_slot_rapidly() -> void:
	var sim: MatchSim = _sim()
	var player: PlayerState = sim.players[PLAYER_ID]
	_send_input(sim, PlayerInput.BTN_WEAPON_1)
	_send_input(sim, 0)
	_send_input(sim, PlayerInput.BTN_WEAPON_2)
	_send_input(sim, 0)
	assert_gt(player.weapon_cooldowns[0], 0.0)
	assert_gt(player.weapon_cooldowns[1], 0.0)


func test_cancel_does_not_handoff_to_a_simultaneous_other_press() -> void:
	var sim: MatchSim = _sim()
	var player: PlayerState = sim.players[PLAYER_ID]
	_send_input(sim, PlayerInput.BTN_WEAPON_1)
	_send_input(sim, PlayerInput.BTN_WEAPON_2 | PlayerInput.BTN_AIM_CANCEL)
	assert_eq(player.weapon_cooldowns, [0.0, 0.0] as Array[float])
	assert_eq(player.aim_hold, [-1.0, -1.0] as Array[float])
	_send_input(sim, PlayerInput.BTN_WEAPON_2)
	_send_input(sim, 0)
	assert_eq(player.weapon_cooldowns[1], 0.0, "rejected held input needs a release and fresh press")


func test_simultaneous_press_on_cooldown_does_not_precast_other_slot() -> void:
	var sim: MatchSim = _sim()
	var player: PlayerState = sim.players[PLAYER_ID]
	player.weapon_cooldowns[0] = DT * 2.0
	_send_input(sim, BOTH_WEAPONS)
	assert_eq(player.aim_hold, [-1.0, -1.0] as Array[float], "lower slot owns and rejects the press")
	_send_input(sim, BOTH_WEAPONS)
	assert_eq(player.weapon_cooldowns[0], 0.0)
	assert_eq(player.aim_hold, [-1.0, -1.0] as Array[float], "held buttons do not start when ready")
	_send_input(sim, 0)
	assert_eq(player.weapon_cooldowns, [0.0, 0.0] as Array[float])


func test_hard_cc_clears_the_active_aim_and_requires_a_fresh_press() -> void:
	var sim: MatchSim = _sim()
	var player: PlayerState = sim.players[PLAYER_ID]
	_send_input(sim, PlayerInput.BTN_WEAPON_1)
	sim.apply_effect(PLAYER_ID, StatusEffects.Type.STUN, DT * 2.0, 0.0)
	_send_input(sim, PlayerInput.BTN_WEAPON_1)
	assert_eq(player.aim_hold[0], -1.0)
	_send_input(sim, PlayerInput.BTN_WEAPON_1)
	_send_input(sim, 0)
	assert_eq(player.weapon_cooldowns[0], 0.0, "holding through crowd control never restarts the aim")
	_send_input(sim, PlayerInput.BTN_WEAPON_1)
	_send_input(sim, 0)
	assert_gt(player.weapon_cooldowns[0], 0.0)


func test_tag_clears_the_active_aim_and_held_input_cannot_cast_after_rescue() -> void:
	var sim: MatchSim = _sim()
	var player: PlayerState = sim.players[PLAYER_ID]
	_send_input(sim, PlayerInput.BTN_WEAPON_1)
	sim.kill(PLAYER_ID)
	assert_eq(player.aim_hold, [-1.0, -1.0] as Array[float])
	var rescuer: PlayerState = sim.add_player(2, 0)
	rescuer.position = player.position
	# the rescue takes `rescue_hold_time` in the area; the held press runs through all of it
	for i: int in ceili((sim.rules.rescue_hold_time + 0.3) / (1.0 / 30.0)):
		_send_input(sim, PlayerInput.BTN_WEAPON_1)
	assert_false(player.tagged, "the teammate rescues the player")
	_send_input(sim, PlayerInput.BTN_WEAPON_1)
	_send_input(sim, 0)
	assert_eq(player.weapon_cooldowns[0], 0.0, "the press held through tag and rescue is not a fresh press")
