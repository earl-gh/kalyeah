extends GutTest
## The hand of 4 and NEXT (docs/GDD.md "Controls: tap to move, cards to cast" > The hand: 4 cards and Next).
## Seam: MatchSim (PlayerInput in, the hand, NEXT and deck of PlayerState out).

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules


func before_each() -> void:
	_rules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules
	_rules.card_cycle = true
	_rules.elixir_enabled = true


func _sim(seed_value: int = 5) -> MatchSim:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, seed_value)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.players[1].position = Vector2(0.0, 2.0)
	sim.players[2].position = Vector2(0.0, -1.0)
	sim.deal_cards(1)
	sim.deal_cards(2)
	return sim


## Casts `slot` of player 1 with a full bar and a healthy enemy in reach of every card (so every card casts).
func _cast(sim: MatchSim, slot: int) -> void:
	sim.players[1].elixir = _rules.elixir_max
	sim.players[2].hp = _rules.player_max_hp
	sim.players[1].hp = _rules.player_max_hp - 50
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.CARD_BITS[slot], sim.tick))
	sim.step(DT)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick))
	sim.step(DT)
	sim.players[1].cast_lock_left = 0.0
	sim.players[1].effects.clear()
	sim.players[2].effects.clear()
	sim.weapons.reset()


func test_every_player_holds_four_cards_and_a_next_and_the_rest_is_the_deck() -> void:
	var state: PlayerState = _sim().players[1]
	assert_eq(state.weapons.size(), 4)
	assert_ne(state.next_card, &"")
	assert_eq(state.deck.size(), _rules.weapons.size() - 5)
	var seen: Dictionary[StringName, bool] = {}
	for id: StringName in state.weapons + state.deck + ([state.next_card] as Array[StringName]):
		seen[id] = true
	assert_eq(seen.size(), _rules.weapons.size(), "each weapon exactly once")


func test_the_same_seed_deals_the_same_hand_and_deck() -> void:
	assert_eq(_sim(7).players[1].weapons, _sim(7).players[1].weapons)
	assert_eq(_sim(7).players[1].deck, _sim(7).players[1].deck)
	assert_ne(_sim(7).players[1].deck, _sim(8).players[1].deck)


func test_a_played_card_goes_to_the_back_and_next_fills_its_slot_on_the_same_tick() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	var played: StringName = state.weapons[2]
	var next: StringName = state.next_card
	var front: StringName = state.deck[0]
	var others: Array[StringName] = [state.weapons[0], state.weapons[1], state.weapons[3]]
	watch_signals(sim)
	_cast(sim, 2)
	assert_signal_emitted_with_parameters(sim, "card_drawn", [1, 2])
	assert_eq(state.weapons[2], next, "NEXT dropped into the played slot")
	assert_eq(state.next_card, front, "the front of the deck is the new NEXT")
	assert_eq(state.deck.back(), played, "the played card is at the back of the deck")
	assert_eq([state.weapons[0], state.weapons[1], state.weapons[3]] as Array[StringName], others, "the rest of the hand is untouched")
	assert_eq(state.weapons.size(), 4, "never an empty slot")
	assert_false(state.weapons.has(&""))


func test_a_played_card_is_back_in_the_hand_after_ten_more_casts() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	var played: StringName = state.weapons[0]
	_cast(sim, 0)
	for cast: int in 9:
		assert_false(state.weapons.has(played), "not back after %d casts" % cast)
		_cast(sim, 1)
	assert_eq(state.next_card, played, "NEXT after 9 more casts")
	_cast(sim, 1)
	assert_true(state.weapons.has(played), "in the hand after 10 more casts")


func test_card_three_and_four_cast_their_own_slots() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	var third: StringName = state.weapons[2]
	var fourth: StringName = state.weapons[3]
	watch_signals(sim)
	_cast(sim, 3)
	assert_signal_emitted_with_parameters(sim, "weapon_cast", [1, fourth])
	_cast(sim, 2)
	assert_signal_emitted_with_parameters(sim, "weapon_cast", [1, third])


func test_only_one_card_at_a_time() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	state.elixir = _rules.elixir_max
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_1, sim.tick))
	sim.step(DT)
	sim.set_input(1, PlayerInput.create(Vector2.ZERO, Vector2.ZERO, PlayerInput.BTN_CARD_1 | PlayerInput.BTN_CARD_4, sim.tick))
	sim.step(DT)
	assert_gte(state.aim_hold[0], 0.0)
	assert_lt(state.aim_hold[3], 0.0, "while one card is held the others cannot be picked up")


func test_a_new_round_keeps_the_hand_and_the_deck_order() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	_cast(sim, 0)
	var hand: Array[StringName] = state.weapons.duplicate()
	var deck: Array[StringName] = state.deck.duplicate()
	var next: StringName = state.next_card
	sim.kill(2)
	for i: int in 30 * 12:
		sim.step(DT)
		if sim.score.points[0] > 0 and sim.phase == MatchSim.Phase.PLAYING:
			break
	assert_eq(sim.score.points[0], 1)
	assert_eq(state.weapons, hand)
	assert_eq(state.deck, deck)
	assert_eq(state.next_card, next)


func test_the_hand_size_is_data() -> void:
	_rules.hand_size = 3
	var state: PlayerState = _sim().players[1]
	assert_eq(state.weapons.size(), 3)
	assert_eq(state.deck.size(), _rules.weapons.size() - 4)
