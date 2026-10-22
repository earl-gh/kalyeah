extends GutTest
## Weapon cards (docs/GDD.md "Weapon cards"): every player has a shuffled deck of all the weapons and a hand of two; using a card
## sends it to the back of the deck and, when its cooldown is over, the next card takes its place.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules
var _was: bool


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_was = _rules.card_cycle
	_rules.card_cycle = true


func after_each() -> void:
	_rules.card_cycle = _was


func _sim(seed_value: int = 5) -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, seed_value)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.players[2].position = Vector2(100.0, 100.0)
	sim.deal_cards(1)
	sim.deal_cards(2)
	sim.begin_intro()
	sim.intro_left = 0.0
	sim.phase = MatchSim.Phase.PLAYING
	return sim


func _use(sim: MatchSim, slot: int) -> void:
	var state: PlayerState = sim.players[1]
	var bit: int = WeaponCasting.WEAPON_BITS[slot]
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, bit, sim.tick))
	sim.step(DT)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
	sim.step(DT)


func test_every_player_holds_two_cards_and_the_rest_of_all_the_weapons_wait_in_the_deck() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	assert_eq(state.weapons.size(), 2)
	assert_eq(state.deck.size(), _rules.weapons.size() - 2, "hand + deck is every weapon")
	var all: Dictionary = {}
	for id: StringName in state.weapons + state.deck:
		all[id] = true
	assert_eq(all.size(), _rules.weapons.size(), "each weapon exactly once")


func test_the_same_seed_deals_the_same_decks_and_another_seed_another() -> void:
	assert_eq(_sim(7).players[1].weapons, _sim(7).players[1].weapons)
	assert_eq(_sim(7).players[1].deck, _sim(7).players[1].deck)
	assert_ne(_sim(7).players[1].deck, _sim(8).players[1].deck)


func test_using_a_card_sends_it_to_the_back_and_the_slot_reloads_for_its_cooldown() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	var used: StringName = state.weapons[0]
	var next: StringName = state.deck[0]
	_use(sim, 0)
	assert_eq(state.deck.back(), used, "the used card is at the back of the deck")
	assert_true(state.reloading[0])
	assert_eq(state.weapons[0], used, "it keeps the slot while it reloads")
	assert_gt(state.weapon_cooldowns[0], 0.0)
	assert_eq(state.deck[0], next, "the card that comes next is the front of the deck")


func test_when_the_cooldown_is_over_the_next_card_takes_the_slot() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	var used: StringName = state.weapons[0]
	var next: StringName = state.deck[0]
	var drawn: Array[int] = []
	sim.card_drawn.connect(func(_id: int, slot: int) -> void: drawn.append(slot))
	_use(sim, 0)
	for i: int in ceili((sim.weapon_defs[used].cooldown + 0.5) / DT):
		sim.step(DT)
	assert_eq(state.weapons[0], next, "the next card arrived")
	assert_false(state.reloading[0])
	assert_eq(drawn, [0] as Array[int])
	assert_eq(state.weapon_cooldowns[0], 0.0, "and it is ready")
	assert_eq(state.deck.size(), _rules.weapons.size() - 2, "the deck keeps its size")


func test_the_other_slot_stays_usable_while_one_reloads() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	_use(sim, 0)
	assert_true(state.reloading[0])
	assert_eq(state.weapon_cooldowns[1], 0.0, "the other card is ready")
	assert_true(sim.weapon_ready(state, 1))
	assert_false(sim.weapon_ready(state, 0), "the used one is not")


func test_the_deck_cycles_through_every_card_before_a_used_one_comes_back() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	var order: Array[StringName] = state.deck.duplicate()
	var drawn: Array[StringName] = []
	sim.card_drawn.connect(func(_id: int, slot: int) -> void: drawn.append(state.weapons[slot]))
	for use: int in order.size():
		var used: StringName = state.weapons[0]
		# a target in reach and alive, so that every weapon can be used
		sim.players[2].tagged = false
		sim.players[2].hp = _rules.player_max_hp
		sim.players[2].position = state.position + Vector2(0.0, -3.0)
		_use(sim, 0)
		assert_true(state.reloading[0], "%s was cast" % used)
		for i: int in ceili((sim.weapon_defs[used].cooldown + 0.5) / DT):
			sim.step(DT)
	assert_eq(drawn, order, "the cards arrive in the deck's order, strictly rotating")
	assert_eq(drawn.size(), order.size())
	var seen: Dictionary = {}
	for id: StringName in drawn:
		seen[id] = true
	assert_eq(seen.size(), drawn.size(), "none repeats before the whole deck has been through")


func test_a_new_round_finishes_every_reload() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	_use(sim, 0)
	assert_true(state.reloading[0])
	sim.objectives._reset_round(sim)
	assert_false(state.reloading[0])
	assert_eq(state.weapon_cooldowns[0], 0.0)


func test_there_is_no_swap_in_the_box_with_cards() -> void:
	var sim: MatchSim = _sim()
	sim.kill(1)
	assert_false(sim.swap_loadout(1, &"bato_light", &"lata"), "the weapons are cards: nothing to change")


func test_the_snapshot_carries_the_next_card_and_the_reload_flags() -> void:
	var sim: MatchSim = _sim()
	_use(sim, 1)
	var data: Dictionary = Snapshot.capture(sim, {})
	var mirror: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	mirror.add_player(1, 0)
	mirror.add_player(2, 1)
	Snapshot.apply(mirror, data, 1)
	assert_eq(mirror.players[1].weapons, sim.players[1].weapons)
	assert_eq(mirror.players[1].deck, [sim.players[1].deck[0]] as Array[StringName], "the client only needs the next card")
	assert_eq(mirror.players[1].reloading, sim.players[1].reloading)


func test_without_cards_nothing_cycles() -> void:
	_rules.card_cycle = false
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	sim.add_player(1, 0)
	sim.set_loadout(1, &"bato_light", &"papel_shield")
	sim.cycle_used(sim.players[1], 0)
	assert_eq(sim.players[1].weapons, [&"bato_light", &"papel_shield"] as Array[StringName])
	assert_true(sim.players[1].deck.is_empty())


func test_bots_play_with_cards_too_and_their_hands_keep_cycling() -> void:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 11)
	var coaches: Array[TeamCoach] = [TeamCoach.new(0), TeamCoach.new(1)]
	for id: int in [1, 2, 3]:
		sim.add_player(id, 0)
		coaches[0].add(sim, id, 0.4)
	for id: int in [4, 5, 6]:
		sim.add_player(id, 1)
		coaches[1].add(sim, id, 0.4)
	for id: int in sim.players:
		sim.equip(id, true)
	sim.begin_intro()
	sim.intro_left = 0.0
	sim.phase = MatchSim.Phase.PLAYING
	var casts: Array[int] = [0]
	var draws: Array[int] = [0]
	sim.weapon_cast.connect(func(_id: int, _w: StringName) -> void: casts[0] += 1)
	sim.card_drawn.connect(func(_id: int, _slot: int) -> void: draws[0] += 1)
	for i: int in 30 * 90:
		for coach: TeamCoach in coaches:
			coach.drive(sim)
		sim.step(DT)
	assert_gt(casts[0], 5, "the bots throw their cards")
	assert_gt(draws[0], 3, "and new cards arrive")
	for id: int in sim.players:
		assert_eq(sim.players[id].weapons.size() + sim.players[id].deck.size(), _rules.weapons.size(), "no card is lost or doubled")
