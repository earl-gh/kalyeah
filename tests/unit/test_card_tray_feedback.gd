extends GutTest
## The hand's motion (owner: "played card animates out and NEXT slides in"): a played card flies out and NEXT slides into its slot,
## a fresh deal flies nothing, Reduce motion flies nothing, and the elixir-off path (the old shuffler, D26) still shows an empty slot
## as a card back with the shuffler's progress.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules
var _was: bool
var _was_elixir: bool
var _motion: bool


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_was = _rules.card_cycle
	_was_elixir = _rules.elixir_enabled
	_rules.card_cycle = true
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false


func after_each() -> void:
	_rules.card_cycle = _was
	_rules.elixir_enabled = _was_elixir
	Settings.reduced_motion = _motion


func _hud() -> MatchHud:
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var hud: MatchHud = MatchHud.new()
	phone.add_child(hud)
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


func _flights(hud: MatchHud) -> Array[CardFlight]:
	var found: Array[CardFlight] = []
	for child: Node in hud.hand.get_children():
		if child is CardFlight:
			found.append(child as CardFlight)
	return found


func test_a_played_card_flies_out_and_next_slides_into_its_slot() -> void:
	var sim: MatchSim = _sim()
	var hud: MatchHud = _hud()
	var player: PlayerState = sim.players[1]
	hud.sync_player(sim, player, false)
	var used: StringName = player.weapons[2]
	var next: StringName = player.next_card
	sim.cycle_used(player, 2)
	hud.sync_player(sim, player, false)
	var flights: Array[CardFlight] = _flights(hud)
	assert_eq(flights.size(), 2, "one card leaves, one arrives")
	assert_eq(flights[0].def.id, used, "the played card leaves")
	assert_lt(flights[0].rect_at(1.0).size.x, 2.0, "shrinking away")
	assert_eq(flights[1].def.id, next, "NEXT arrives")
	assert_eq(flights[1].rect_at(0.0), hud.hand.next_rect, "from the NEXT slot")
	assert_eq(flights[1].rect_at(1.0), hud.hand.card_rects[2], "into the slot that was played")
	assert_gt(hud.hand._arrive[2], 0.0, "the slot waits for it")
	assert_eq(hud.hand.next_def.id, player.next_card, "and NEXT shows the new front of the deck")


func test_reduce_motion_flies_nothing() -> void:
	Settings.reduced_motion = true
	var sim: MatchSim = _sim()
	var hud: MatchHud = _hud()
	var player: PlayerState = sim.players[1]
	hud.sync_player(sim, player, false)
	sim.cycle_used(player, 0)
	hud.sync_player(sim, player, false)
	assert_eq(_flights(hud).size(), 0)
	assert_eq(hud.hand._arrive[0], 0.0, "the new card is there at once")


func test_a_fresh_deal_flies_nothing() -> void:
	var sim: MatchSim = _sim()
	var hud: MatchHud = _hud()
	var player: PlayerState = sim.players[1]
	hud.sync_player(sim, player, false)
	sim.deal_cards(1)
	hud.sync_player(sim, player, false)
	assert_eq(_flights(hud).size(), 0, "nothing was played, nothing flies")


func test_with_elixir_off_an_empty_slot_shows_the_shufflers_progress() -> void:
	_rules.elixir_enabled = false
	var sim: MatchSim = _sim()
	var hud: MatchHud = _hud()
	var player: PlayerState = sim.players[1]
	sim.cycle_used(player, 0)
	sim.cycle_used(player, 1)
	for i: int in 3:
		sim.step(DT)
	hud.sync_player(sim, player, false)
	assert_null(hud.hand.defs[1], "the second slot waits")
	assert_gt(hud.hand.wait_seconds, 0, "with the seconds left on its card back")
	assert_lt(hud.hand.wait_fraction, 1.0)
	assert_false(hud.hand.uses_elixir, "no elixir bar or cost drops")
