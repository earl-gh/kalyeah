extends GutTest
## The card tray's feedback (owner: improve the gameplay GUI): a card rises into the hand, the used card flies to the shuffler, an empty
## slot is a card back with the shuffler's progress, and the shuffler card fills as its cooldown runs.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules
var _was: bool
var _was_elixir: bool
var _was_hand: int
var _motion: bool


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_was = _rules.card_cycle
	_rules.card_cycle = true
	# The shuffler with a hand of two is the elixir-off path now (docs/GDD.md "Controls: tap to move, cards to cast", D26).
	_was_elixir = _rules.elixir_enabled
	_was_hand = _rules.hand_size
	_rules.elixir_enabled = false
	_rules.hand_size = 2
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false


func after_each() -> void:
	_rules.card_cycle = _was
	_rules.elixir_enabled = _was_elixir
	_rules.hand_size = _was_hand
	Settings.reduced_motion = _motion


func _hud() -> MatchHud:
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var hud: MatchHud = MatchHud.new()
	phone.add_child(hud)
	hud.set_card_mode(true)
	return hud


func _sim() -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.players[2].position = Vector2(100.0, 100.0)
	sim.deal_cards(1)
	sim.deal_cards(2)
	sim.begin_intro()
	sim.intro_left = 0.0
	sim.phase = MatchSim.Phase.PLAYING
	return sim


func test_a_new_card_rises_into_its_slot_and_settles() -> void:
	var button: AimButton = AimButton.new()
	add_child_autofree(button)
	var def: WeaponDef = _rules.weapons[0]
	button.set_card(def)
	assert_gt(button._deal, 0.0, "the card is being dealt")
	button._process(TouchButton.DEAL_TIME + 0.01)
	assert_eq(button._deal, 0.0, "and has settled")
	button.set_card(def)
	assert_eq(button._deal, 0.0, "the same card does not rise again")


func test_reduce_motion_skips_the_rise_and_the_flight() -> void:
	Settings.reduced_motion = true
	var button: AimButton = AimButton.new()
	add_child_autofree(button)
	button.set_card(_rules.weapons[0])
	assert_eq(button._deal, 0.0)
	assert_null(CardFlight.fly(button, _rules.weapons[0], Rect2(), Rect2()), "no flight")


func test_the_used_card_flies_to_the_shuffler() -> void:
	var sim: MatchSim = _sim()
	var hud: MatchHud = _hud()
	var player: PlayerState = sim.players[1]
	hud.sync_player(sim, player, false)
	var used: StringName = player.weapons[0]
	sim.cycle_used(player, 0)
	hud.sync_player(sim, player, false)
	var flights: Array[CardFlight] = []
	for child: Node in hud.get_children():
		if child is CardFlight:
			flights.append(child as CardFlight)
	assert_eq(flights.size(), 1, "one card in the air")
	assert_eq(flights[0].def.id, used, "the card that was used")
	var start: Rect2 = flights[0].rect_at(0.0)
	var end: Rect2 = flights[0].rect_at(1.0)
	assert_eq(start, hud.weapon_buttons[0].get_rect(), "from the slot")
	assert_lt(end.size.x, start.size.x, "it shrinks into the shuffler")
	assert_almost_eq(end.position.x, hud.shuffler.position.x, 0.5, "and lands in its window")


func test_a_new_round_deal_does_not_fly_cards() -> void:
	var sim: MatchSim = _sim()
	var hud: MatchHud = _hud()
	var player: PlayerState = sim.players[1]
	hud.sync_player(sim, player, false)
	sim.deal_cards(1)
	hud.sync_player(sim, player, false)
	for child: Node in hud.get_children():
		assert_false(child is CardFlight, "nothing was used, nothing flies")


func test_an_empty_slot_shows_the_shufflers_progress() -> void:
	var sim: MatchSim = _sim()
	var hud: MatchHud = _hud()
	var player: PlayerState = sim.players[1]
	sim.cycle_used(player, 0)
	sim.cycle_used(player, 1)
	for i: int in 3:
		sim.step(DT)
	hud.sync_player(sim, player, false)
	var empty: AimButton = hud.weapon_buttons[1]
	assert_null(empty.weapon_def, "the second slot waits")
	assert_gt(empty.wait_seconds, 0, "with the seconds left on its card back")
	assert_gte(empty.wait_fraction, 0.0)
	assert_lt(empty.wait_fraction, 1.0)

