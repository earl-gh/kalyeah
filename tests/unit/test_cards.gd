extends GutTest
## Weapon cards (docs/GDD.md "Weapon cards"): every player has a shuffled deck of all the weapons and a hand of two; using a card
## sends it to the back of the deck and, when its cooldown is over, the next card takes its place.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0

var _rules: GameRules
var _was: bool
var _was_elixir: bool
var _was_hand: int


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_was = _rules.card_cycle
	_rules.card_cycle = true
	# The shuffler with a hand of two is the elixir-off path now (docs/GDD.md "Controls: tap to move, cards to cast", D26).
	_was_elixir = _rules.elixir_enabled
	_was_hand = _rules.hand_size
	_rules.elixir_enabled = false
	_rules.hand_size = 2


func after_each() -> void:
	_rules.card_cycle = _was
	_rules.elixir_enabled = _was_elixir
	_rules.hand_size = _was_hand


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


func _all(state: PlayerState) -> Array[StringName]:
	var found: Array[StringName] = []
	for id: StringName in state.weapons:
		if id != &"":
			found.append(id)
	if state.next_card != &"":
		found.append(state.next_card)
	if state.shuffle_card != &"":
		found.append(state.shuffle_card)
	found.append_array(state.incoming)
	found.append_array(state.deck)
	return found


func test_every_player_holds_two_cards_one_waits_in_the_shuffler_and_the_rest_are_the_deck() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	assert_eq(state.weapons.size(), 2)
	assert_ne(state.next_card, &"", "the shuffler's window shows the next card")
	assert_eq(state.deck.size(), _rules.weapons.size() - 3)
	var all: Dictionary = {}
	for id: StringName in _all(state):
		all[id] = true
	assert_eq(all.size(), _rules.weapons.size(), "each weapon exactly once")


func test_the_same_seed_deals_the_same_decks_and_another_seed_another() -> void:
	assert_eq(_sim(7).players[1].weapons, _sim(7).players[1].weapons)
	assert_eq(_sim(7).players[1].deck, _sim(7).players[1].deck)
	assert_ne(_sim(7).players[1].deck, _sim(8).players[1].deck)


func test_using_a_card_drops_the_next_card_into_the_hand_at_once_and_the_used_card_goes_to_the_shuffler() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	var used: StringName = state.weapons[0]
	var next: StringName = state.next_card
	var other: StringName = state.weapons[1]
	_use(sim, 0)
	assert_eq(state.weapons[0], next, "the next card is in the hand at once, ready")
	assert_eq(state.weapons[1], other, "the other card is untouched")
	assert_eq(state.shuffle_card, used, "the used card is in the shuffler")
	assert_eq(state.next_card, &"", "the window is empty while the shuffler works")
	assert_eq(state.weapon_cooldowns, [0.0, 0.0] as Array[float], "no card in the hand is slowed")
	assert_true(sim.weapon_ready(state, 0))
	assert_true(sim.weapon_ready(state, 1))


func test_the_shuffler_plays_the_used_cards_cooldown_then_shows_a_new_next_card() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	var used: StringName = state.weapons[0]
	var front: StringName = state.deck[0]
	_use(sim, 0)
	assert_almost_eq(state.shuffle_total, sim.weapon_defs[used].cooldown, 0.001)
	for i: int in ceili((sim.weapon_defs[used].cooldown - 0.5) / DT):
		sim.step(DT)
	assert_eq(state.next_card, &"", "still shuffling")
	for i: int in ceili(1.0 / DT):
		sim.step(DT)
	assert_eq(state.next_card, front, "the shuffler shows the new next card")
	assert_eq(state.shuffle_card, &"")
	assert_eq(state.deck.back(), used, "the used card went to the back of the deck")


func test_using_the_second_card_while_the_shuffler_works_leaves_a_slot_empty_until_it_is_done() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	var first: StringName = state.weapons[0]
	var second: StringName = state.weapons[1]
	var front: StringName = state.deck[0]
	_use(sim, 0)
	_use(sim, 1)
	assert_eq(state.weapons[1], &"", "no card to drop in: the slot waits")
	assert_false(sim.weapon_ready(state, 1))
	assert_eq(state.incoming, [second] as Array[StringName], "the second used card queues for the shuffler")
	for i: int in ceili((sim.weapon_defs[first].cooldown + 0.5) / DT):
		sim.step(DT)
	assert_eq(state.weapons[1], front, "the first card's cooldown over: the new card fills the empty slot")
	assert_eq(state.shuffle_card, second, "and the shuffler goes on with the second used card")


func test_the_deck_cycles_through_every_card_before_a_used_one_comes_back() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	var order: Array[StringName] = [state.next_card]
	order.append_array(state.deck)
	var arrived: Array[StringName] = []
	# a card is seen when it appears in the window and again when it drops into the hand: count it once
	sim.card_drawn.connect(func(_id: int, slot: int) -> void:
		var card: StringName = state.weapons[slot] if slot >= 0 else state.next_card
		if arrived.is_empty() or arrived.back() != card:
			arrived.append(card))
	for use: int in order.size() - 1:
		var used: StringName = state.weapons[0]
		sim.players[2].tagged = false
		sim.players[2].hp = _rules.player_max_hp
		sim.players[2].position = state.position + Vector2(0.0, -3.0)
		_use(sim, 0)
		for i: int in ceili((sim.weapon_defs[used].cooldown + 0.5) / DT):
			sim.step(DT)
	assert_eq(arrived, order.slice(0, arrived.size()), "the cards arrive in the deck's order, strictly rotating")
	assert_gte(arrived.size(), order.size() - 1)
	assert_eq(_all(state).size(), _rules.weapons.size(), "no card lost or doubled")
	var seen: Dictionary = {}
	for id: StringName in arrived:
		seen[id] = true
	assert_eq(seen.size(), arrived.size(), "none repeats before the whole deck has been through")


func test_a_new_round_empties_the_shuffler_and_fills_the_hand_and_the_window() -> void:
	var sim: MatchSim = _sim()
	var state: PlayerState = sim.players[1]
	_use(sim, 0)
	_use(sim, 1)
	assert_eq(state.weapons[1], &"")
	sim.objectives._reset_round(sim)
	assert_eq(state.shuffle_card, &"")
	assert_true(state.incoming.is_empty())
	assert_ne(state.weapons[0], &"")
	assert_ne(state.weapons[1], &"")
	assert_ne(state.next_card, &"")
	assert_eq(_all(state).size(), _rules.weapons.size(), "every card is somewhere")


func test_there_is_no_swap_in_the_box_with_cards() -> void:
	var sim: MatchSim = _sim()
	sim.kill(1)
	assert_false(sim.swap_loadout(1, &"bato_light", &"lata"), "the weapons are cards: nothing to change")


func test_the_snapshot_carries_the_hand_the_window_and_the_shuffler() -> void:
	var sim: MatchSim = _sim()
	_use(sim, 1)
	sim.step(DT)
	var data: Dictionary = Snapshot.capture(sim, {})
	var mirror: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	mirror.add_player(1, 0)
	mirror.add_player(2, 1)
	Snapshot.apply(mirror, data, 1)
	var a: PlayerState = sim.players[1]
	var b: PlayerState = mirror.players[1]
	assert_eq(b.weapons, a.weapons)
	assert_eq(b.next_card, a.next_card)
	assert_eq(b.shuffle_card, a.shuffle_card)
	assert_almost_eq(b.shuffle_left, a.shuffle_left, 0.001)
	assert_almost_eq(b.shuffle_total, a.shuffle_total, 0.001)


func test_without_cards_nothing_cycles() -> void:
	_rules.card_cycle = false
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	sim.add_player(1, 0)
	sim.set_loadout(1, &"bato_light", &"papel_shield")
	sim.cycle_used(sim.players[1], 0)
	assert_eq(sim.players[1].weapons, [&"bato_light", &"papel_shield"] as Array[StringName])
	assert_false(sim.players[1].cards)


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
		assert_eq(_all(sim.players[id]).size(), _rules.weapons.size(), "no card is lost or doubled")


func test_the_kid_screen_is_only_the_kid_carousel_its_name_and_play() -> void:
	var screen: WeaponPickScreen = WeaponPickScreen.new()
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	phone.add_child(screen)
	screen.set_anchors_preset(Control.PRESET_TOP_LEFT)
	screen.size = phone.size
	screen.cards_mode = true
	var ids: Array[StringName] = []
	for weapon: WeaponDef in _rules.weapons:
		ids.append(weapon.id)
	screen.open(WeaponPick.new(ids, 10.0), _rules.weapons, "", RandomNumberGenerator.new(), _rules.characters, &"toni", true)
	await wait_process_frames(2)
	assert_null(screen.find_child("DeckPreview", true, false), "the deck is in the Book, not on the way to the match")
	assert_false(screen._mode_label.visible, "no subtitle")
	assert_eq(screen._carousel.cards.size(), 6, "the six card pictures")
	assert_false(screen._character_row.visible, "no row of small tiles")
	assert_eq(screen._ready_button.text, "Play")
	assert_false(screen._hint.visible, "no weapon hints in cards mode")
