extends GutTest
## The hand's cards look like old Filipino teks cards (owner: "apply last styling of modes ... to the cards at gameplay"): the SAME worn look
## as the mode card (`CardFace`: the card_picture shader, the age tint and the power line), printed in the weapon's kind colour with the
## round art on it, each weapon with its own wear, the power number from data (cosmetic), and everything that worked before still working
## (cost drop inside the card, washes, deny shake, the held card's gold edge, the flight, Reduce motion, left-handed).

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const DT: float = 1.0 / 30.0
const WEAPON_DIR: String = "res://data/weapons/"

var _rules: GameRules
var _was: bool
var _was_elixir: bool
var _motion: bool
var _left: bool


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_was = _rules.card_cycle
	_was_elixir = _rules.elixir_enabled
	_rules.card_cycle = true
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false
	_left = Settings.left_handed
	Settings.left_handed = false


func after_each() -> void:
	_rules.card_cycle = _was
	_rules.elixir_enabled = _was_elixir
	Settings.reduced_motion = _motion
	Settings.left_handed = _left


func _hud() -> MatchHud:
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var hud: MatchHud = MatchHud.new()
	phone.add_child(hud)
	hud.layout_controls()
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


## A hud showing the first player's hand.
func _shown() -> Array:
	var sim: MatchSim = _sim()
	var hud: MatchHud = _hud()
	hud.sync_player(sim, sim.players[1], false)
	return [sim, hud, sim.players[1]]


func _weapons() -> Array[WeaponDef]:
	var found: Array[WeaponDef] = []
	for file: String in DirAccess.get_files_at(WEAPON_DIR):
		if file.ends_with(".tres"):
			found.append(load(WEAPON_DIR + file) as WeaponDef)
	return found


func _picture(card: HandCard) -> TextureRect:
	return card.get_node("Picture") as TextureRect


# ---- the data -------------------------------------------------------------------

func test_every_weapon_has_a_power_number_and_a_card_without_one_prints_no_line() -> void:
	var weapons: Array[WeaponDef] = _weapons()
	assert_gte(weapons.size(), 14, "every weapon file")
	for def: WeaponDef in weapons:
		assert_gt(def.card_power, 0, "%s has a teks power" % def.id)
		assert_lt(def.card_power, 10000, "%s: at most four digits, as long as the card's line allows" % def.id)
	assert_eq(WeaponDef.new().card_power, 0, "a weapon with no power number draws no line")
	var card: HandCard = autofree(HandCard.new()) as HandCard
	var plain: WeaponDef = WeaponDef.new()
	plain.id = &"nothing"
	card.show_weapon(plain)
	assert_eq((card.get_node("PowerLine") as CardPowerLine).text, "", "no power, no line")


func test_the_power_number_is_cosmetic_no_rule_reads_it() -> void:
	for file: String in DirAccess.get_files_at("res://scripts/sim/"):
		if file.ends_with(".gd"):
			assert_false(FileAccess.get_file_as_string("res://scripts/sim/" + file).contains("card_power") and file != "weapon_def.gd", "%s does not read card_power" % file)


# ---- the look -------------------------------------------------------------------

func test_a_hand_card_is_the_mode_cards_worn_face_printed_in_the_kind_colour_with_the_art_on_it() -> void:
	for def: WeaponDef in _weapons():
		var card: HandCard = autofree(HandCard.new()) as HandCard
		add_child(card)
		card.show_weapon(def)
		card.place(Rect2(Vector2.ZERO, CardHand.card_size(720.0)))
		assert_true(card is CardFace, "it is the mode card's own face, not a second look")
		var material: ShaderMaterial = _picture(card).material as ShaderMaterial
		assert_eq(material.shader.resource_path, "res://shaders/card_picture.gdshader", "%s: the worn card shader" % def.id)
		assert_eq(((card.get_node("Tint") as ColorRect).material as ShaderMaterial).shader.resource_path, "res://shaders/card_tint.gdshader", "%s: and the age tint over all" % def.id)
		assert_eq(material.get_shader_parameter("ground") as Color, WeaponPickScreen.kind_color(def.kind).darkened(CardStyle.GROUND_DARKEN), "%s: printed in its kind colour" % def.id)
		assert_eq(_picture(card).texture, Icons.art(StringName("btn_%s" % String(def.id))), "%s: the round art on it" % def.id)
		assert_eq((card.get_node("PowerLine") as CardPowerLine).text, "POWER %d" % def.card_power, "%s: its power up the edge, from data" % def.id)


func test_the_four_kinds_read_apart_by_their_ground_colour() -> void:
	var seen: Array[Color] = []
	for kind: WeaponDef.Kind in [WeaponDef.Kind.ATTACK, WeaponDef.Kind.CROWD_CONTROL, WeaponDef.Kind.BLOCK, WeaponDef.Kind.HEAL]:
		var ground: Color = CardStyle.ground(kind)
		for other: Color in seen:
			assert_gt(absf(ground.h - other.h) + absf(ground.s - other.s), 0.1, "kinds differ")
		seen.append(ground)


func test_no_black_outline_no_gloss_band_flat_straight_card() -> void:
	var source: String = FileAccess.get_file_as_string("res://scripts/ui/hud/card_style.gd")
	assert_false(source.contains("GLOSS") or source.contains("draw_polyline_colors"), "no gloss band")
	assert_false(source.contains("OUTLINE: Color"), "no black outline")
	var card: HandCard = autofree(HandCard.new()) as HandCard
	assert_false(card.shadowed, "no shadow under a card lying in the hand")
	assert_eq(card.rotation, 0.0, "straight")


func test_every_weapon_wears_differently_and_a_weapon_always_the_same() -> void:
	var seeds: Dictionary[float, StringName] = {}
	for def: WeaponDef in _weapons():
		var seed_value: float = CardStyle.wear_seed(def.id)
		assert_false(seeds.has(seed_value), "%s and %s do not share a wear" % [def.id, seeds.get(seed_value, &"")])
		seeds[seed_value] = def.id
		assert_eq(CardStyle.wear_seed(def.id), seed_value, "%s: stable" % def.id)


func test_the_four_cards_of_a_hand_wear_and_round_their_corners_differently() -> void:
	var shown: Array = _shown()
	var hand: CardHand = (shown[1] as MatchHud).hand
	var seen: Array[float] = []
	var radii: Array[Vector4] = []
	for slot: int in CardHand.SLOTS:
		var card: HandCard = hand.card_node(slot)
		assert_true(card.visible, "slot %d shows a card" % slot)
		assert_false(seen.has(card.wear_seed), "slot %d has its own wear seed" % slot)
		seen.append(card.wear_seed)
		assert_eq(card.wear_seed, CardStyle.wear_seed(hand.defs[slot].id), "the wear comes from the weapon")
		assert_false(radii.has(card.corner_radii()), "slot %d has its own corner radii" % slot)
		radii.append(card.corner_radii())
		var r: Vector4 = card.corner_radii()
		var low: float = CardFace.CORNER_RADIUS_MIN * card.unit
		var high: float = CardFace.CORNER_RADIUS_MAX * card.unit
		for value: float in [r.x, r.y, r.z, r.w]:
			assert_between(value, low - 0.001, high + 0.001, "a slight radius, scaled to the small card")
		assert_lt(r.x + r.y, card.size.x * 0.25, "never round")


func test_the_wear_scales_with_the_card_and_holds_its_proportions_when_it_shrinks() -> void:
	var shown: Array = _shown()
	var hand: CardHand = (shown[1] as MatchHud).hand
	var card: HandCard = hand.card_node(0)
	var full: float = card.unit
	assert_almost_eq(full, hand.card_rects[0].size.x / CardFace.REFERENCE_WIDTH * HandCard.WEAR_BOOST, 0.001)
	assert_lt(full, 1.0, "a smaller card than the mode card's")
	assert_almost_eq(card.art_inset, hand.card_rects[0].size.x * CardStyle.ART_EDGE_SHARE, 0.001, "the art stands in from the edge by a share of the width")
	hand.held_slot = 0
	hand.over_hand = false
	hand.finger = Vector2(300.0, -200.0)
	hand._refresh()
	assert_lt(hand.held_node().unit, full, "a card shrunk over the street wears over shorter lengths")
	assert_lt(hand.held_node().art_inset, card.art_inset, "its art edge shrinks with it")


func test_the_mode_card_keeps_its_own_size_of_wear() -> void:
	var face: CardFace = autofree(CardFace.new()) as CardFace
	assert_eq(face.unit, 1.0)
	assert_eq(face.ground.a, 0.0, "the picture is its whole print")
	assert_true(face.shadowed)


# ---- the hand -------------------------------------------------------------------

func test_next_and_the_slots_are_the_same_kind_of_card_in_the_right_places_and_the_next_one_wears_the_same_in_its_slot() -> void:
	var shown: Array = _shown()
	var hand: CardHand = (shown[1] as MatchHud).hand
	var next: HandCard = hand.next_node()
	assert_true(next.visible)
	assert_almost_eq(next.position, hand.next_rect.position, Vector2(0.01, 0.01))
	assert_almost_eq(next.size, hand.next_rect.size, Vector2(0.01, 0.01))
	assert_eq(next.weapon, hand.next_def)
	for slot: int in CardHand.SLOTS:
		var card: HandCard = hand.card_node(slot)
		assert_eq(card.position, hand.card_rects[slot].position, "slot %d sits on its rect" % slot)
		assert_eq(card.size, hand.card_rects[slot].size)
		assert_eq(card.weapon, hand.defs[slot])
	assert_eq(next.wear_seed, CardStyle.wear_seed(hand.next_def.id), "the next card wears as it will in the hand")


func test_the_cards_come_before_what_lies_over_them_and_touch_goes_to_the_hand_alone() -> void:
	var hand: CardHand = (_shown()[1] as MatchHud).hand
	var order: Array[String] = []
	for child: Node in hand.get_children():
		order.append(child.name)
	assert_eq(order, ["NextCard", "Card0", "Card1", "Card2", "Card3", "Over", "HeldCard", "OverHeld"], "cards, then their overlay, then the held card and its overlay on top")
	for child: Node in hand.get_children():
		assert_eq((child as Control).mouse_filter, Control.MOUSE_FILTER_IGNORE, "%s takes no touch" % child.name)
	assert_eq(hand.mouse_filter, Control.MOUSE_FILTER_STOP)


func test_the_cost_drop_still_sits_inside_the_worn_card() -> void:
	var hand: CardHand = (_shown()[1] as MatchHud).hand
	for slot: int in CardHand.SLOTS:
		var card: HandCard = hand.card_node(slot)
		var drop: Rect2 = CardHand.cost_drop_rect(Rect2(card.position, card.size))
		assert_true(Rect2(card.position, card.size).encloses(drop), "slot %d: the drop is inside the card, bottom aligned" % slot)


func test_a_card_the_hand_cannot_use_is_dimmed_through_the_card_and_back() -> void:
	var shown: Array = _shown()
	var hand: CardHand = (shown[1] as MatchHud).hand
	assert_eq(hand.card_node(0).dim, 0.0)
	hand.blocked_text = "STUNNED"
	hand._refresh()
	for slot: int in CardHand.SLOTS:
		assert_eq(hand.card_node(slot).dim, CardHand.BLOCK_DIM, "blocked: dim")
	hand.blocked_text = ""
	hand._refresh()
	assert_eq(hand.card_node(1).dim, 0.0)


func test_the_held_card_has_its_slot_empty_and_a_gold_edge_on_its_own_worn_outline() -> void:
	var hand: CardHand = (_shown()[1] as MatchHud).hand
	hand.active = true
	hand.held_slot = 1
	hand.over_hand = true
	hand.finger = hand.card_rects[1].get_center() + Vector2(10.0, -40.0)
	hand._refresh()
	assert_false(hand.card_node(1).visible, "the slot it came from shows a hollow, not a second card")
	var held: HandCard = hand.held_node()
	assert_true(held.visible)
	assert_almost_eq(held.position, hand.held_card_rect().position, Vector2(0.01, 0.01), "it follows the finger")
	assert_true(held.shadowed, "lifted: a soft shadow under it")
	assert_eq(held.weapon, hand.defs[1])
	assert_eq(held.wear_seed, hand.card_node(1).wear_seed, "the same wear as in its slot")
	var outline: PackedVector2Array = held.outline()
	var line: PackedVector2Array = CardStyle.closed(outline, Vector2.ZERO)
	assert_eq(line[0], line[line.size() - 1], "the gold line closes round the card")
	assert_gt(CardStyle.LIFT_EDGE, 2.0, "thick enough to read on a flat card")
	for point: Vector2 in outline:
		assert_true(Rect2(Vector2.ZERO, held.size).grow(0.01).has_point(point), "on the card's own corners")
	hand.held_slot = -1
	hand._refresh()
	assert_false(hand.held_node().visible)
	assert_true(hand.card_node(1).visible, "and it is back")


func test_a_denied_card_shakes_with_motion_and_stays_put_with_reduce_motion() -> void:
	var hand: CardHand = (_shown()[1] as MatchHud).hand
	hand.deny(2)
	hand.step(0.05)
	hand._refresh()
	assert_ne(hand.card_node(2).position.x, hand.card_rects[2].position.x, "it is pushed sideways")
	assert_eq(hand.card_node(2).position.y, hand.card_rects[2].position.y)
	Settings.reduced_motion = true
	hand._refresh()
	assert_eq(hand.card_node(2).position.x, hand.card_rects[2].position.x, "no shake with Reduce motion (only the flash)")


func test_a_card_arriving_from_next_is_the_flight_not_the_slot() -> void:
	var shown: Array = _shown()
	var sim: MatchSim = shown[0] as MatchSim
	var hud: MatchHud = shown[1] as MatchHud
	var player: PlayerState = shown[2] as PlayerState
	var next: StringName = player.next_card
	sim.cycle_used(player, 2)
	hud.sync_player(sim, player, false)
	assert_false(hud.hand.card_node(2).visible, "the slot is a hollow while its card flies in")
	var flights: Array[CardFlight] = []
	for child: Node in hud.hand.get_children():
		if child is CardFlight:
			flights.append(child as CardFlight)
	assert_eq(flights.size(), 2)
	var arriving: HandCard = flights[1].get_node("FlyingCard") as HandCard
	assert_eq(arriving.weapon.id, next)
	assert_eq(arriving.wear_seed, CardStyle.wear_seed(next), "a flying card is the same worn card")
	assert_eq(_picture(arriving).material.get("shader"), _picture(hud.hand.card_node(0)).material.get("shader"), "with the same shader")
	assert_almost_eq(arriving.position, hud.hand.next_rect.position, Vector2(0.01, 0.01), "it starts at NEXT")
	assert_almost_eq(arriving.size, hud.hand.next_rect.size, Vector2(0.01, 0.01))
	flights[1].queue_free()
	flights[0].queue_free()
	hud.hand._arrive[2] = 0.0
	hud.hand._refresh()
	assert_true(hud.hand.card_node(2).visible, "and the slot shows the card once it has landed")
	assert_eq(hud.hand.card_node(2).weapon.id, next)


func test_the_flight_card_follows_its_arc_and_darkens() -> void:
	var weapon: WeaponDef = _weapons()[0]
	var parent: Control = autofree(Control.new()) as Control
	add_child(parent)
	var flight: CardFlight = CardFlight.fly(parent, weapon, Rect2(Vector2(100.0, 100.0), Vector2(120.0, 150.0)), Rect2(Vector2(300.0, 100.0), Vector2(60.0, 75.0)))
	assert_not_null(flight)
	var card: HandCard = flight.get_node("FlyingCard") as HandCard
	assert_almost_eq(card.position, flight.rect_at(0.0).position, Vector2(0.01, 0.01))
	flight._process(CardFlight.TIME / 2.0)
	assert_almost_eq(card.position, flight.rect_at(0.5).position, Vector2(0.01, 0.01))
	assert_gt(card.dim, 0.0, "a little darker as it goes")
	assert_lt(card.unit, 120.0 / CardFace.REFERENCE_WIDTH * HandCard.WEAR_BOOST, "its wear shrinks with it")


func test_left_handed_mirrors_the_cards_with_the_hand() -> void:
	var hand: CardHand = (_shown()[1] as MatchHud).hand
	hand.set_mirrored(true)
	for slot: int in CardHand.SLOTS:
		assert_eq(hand.card_node(slot).position, hand.card_rects[slot].position, "slot %d follows the mirrored layout" % slot)
	assert_almost_eq(hand.next_node().position, hand.next_rect.position, Vector2(0.01, 0.01))
	assert_gt(hand.next_node().position.x, hand.card_node(3).position.x, "NEXT at the right")


func test_with_elixir_off_an_empty_slot_is_a_worn_card_back_with_the_ring_over_it() -> void:
	_rules.elixir_enabled = false
	var sim: MatchSim = _sim()
	var hud: MatchHud = _hud()
	var player: PlayerState = sim.players[1]
	sim.cycle_used(player, 0)
	sim.cycle_used(player, 1)
	for i: int in 3:
		sim.step(DT)
	hud.sync_player(sim, player, false)
	var back: HandCard = hud.hand.card_node(1)
	assert_true(back.visible)
	assert_true(back.back, "the slot shows a card back")
	assert_eq(_picture(back).material.get("shader").resource_path, "res://shaders/card_picture.gdshader", "worn like the rest")
	assert_eq((back.get_node("PowerLine") as CardPowerLine).text, "", "a back has no power")
	assert_eq(back.ground, CardStyle.BACK_GROUND, "dark brown cardboard")
	assert_ne(back.wear_seed, hud.hand.card_node(0).wear_seed, "each back wears on its own")


func test_every_state_draws_without_errors() -> void:
	var shown: Array = _shown()
	var hand: CardHand = (shown[1] as MatchHud).hand
	hand.visible = true
	hand.active = true
	for value: float in [0.0, 3.5, 10.0]:
		hand.elixir = value
		hand.shown_elixir = value
		for slot: int in CardHand.SLOTS:
			hand.reached[slot] = clampf(value / float(hand.defs[slot].elixir_cost), 0.0, 1.0)
			hand.pickable[slot] = hand.reached[slot] >= 1.0
		hand.next_fill = 0.4
		hand.notices[0] = "NO TARGET"
		hand.deny(1)
		hand._refresh()
		await wait_frames(2)
	hand.blocked_text = "CAPTURING"
	hand._refresh()
	await wait_frames(2)
	assert_true(is_instance_valid(hand))
