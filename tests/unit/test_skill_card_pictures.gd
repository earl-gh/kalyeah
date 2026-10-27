extends GutTest
## The owner's full-card pictures in the match (`assets/ui/skill_cards/<weapon_id>.webp`): a weapon that has one is drawn AS that picture (it
## already is a card: torn cardboard frame, art and glow), bare, with no ground colour, wear, age tint, power line or card behind it; a weapon
## without one keeps the teks card with the painted icon. Everything that lies over a card (the cost drop, the washes, the red flash, the
## lift cue) still works and follows the picture's own silhouette, not a rectangle. Adding a picture is adding its file.

const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const WEAPON_DIR: String = "res://data/weapons/"
const FOLDER: String = "res://assets/ui/skill_cards/"
const PICTURE_HAND: Array[StringName] = [&"heal", &"papel_shield", &"papel_trap", &"gunting_heavy"]
const PICTURE_NEXT: StringName = &"langit_lupa"
const WITH_PICTURE: Array[StringName] = [&"heal", &"papel_shield", &"papel_trap", &"gunting_heavy", &"langit_lupa", &"jacks", &"trumpo", &"gunting_light", &"bato_heavy", &"bola", &"bato_light", &"lata", &"tsinelas_heavy", &"tsinelas_light"]
## Every weapon has its picture now; the teks card with the painted icon stays as the fallback for a weapon without one. These stand in for
## such weapons in the tests that look at it (`_no_picture`).
const TEKS_ONLY: Array[StringName] = [&"bato_light", &"lata", &"tsinelas_heavy", &"tsinelas_light"]
## What the pictures may add to the APK: the imported textures are about 190 KB each, the whole folder stays under this (the APK is close
## to its 150 MB limit).
const FOLDER_BUDGET_BYTES: int = 2048 * 1024

var _rules: GameRules
var _was: bool
var _was_elixir: bool
var _motion: bool


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_was = _rules.card_cycle
	_was_elixir = _rules.elixir_enabled
	_rules.card_cycle = true
	_rules.elixir_enabled = true
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false


func after_each() -> void:
	for id: StringName in TEKS_ONLY:
		CardStyle._pictures.erase(id)
	_rules.card_cycle = _was
	_rules.elixir_enabled = _was_elixir
	Settings.reduced_motion = _motion
	Settings.left_handed = false


## Pretends the owner has no picture for `ids`: they are drawn as the fallback teks card (the lookup cache is reset after each test).
func _no_picture(ids: Array[StringName] = TEKS_ONLY) -> void:
	for id: StringName in ids:
		CardStyle._pictures[id] = null


func _hud() -> MatchHud:
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var hud: MatchHud = MatchHud.new()
	phone.add_child(hud)
	hud.layout_controls()
	return hud


## A hud showing a hand of picture cards (heal, papel_shield, papel_trap, gunting_heavy) with langit_lupa next.
func _shown(ids: Array[StringName] = PICTURE_HAND, next: StringName = PICTURE_NEXT) -> Array:
	var sim: MatchSim = MatchSim.new(_rules, LAYOUT, 5)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	sim.players[2].position = Vector2(100.0, 100.0)
	sim.deal_cards(1)
	sim.deal_cards(2)
	sim.begin_intro()
	sim.intro_left = 0.0
	sim.phase = MatchSim.Phase.PLAYING
	sim.players[1].weapons.assign(ids)
	sim.players[1].next_card = next
	var hud: MatchHud = _hud()
	hud.sync_player(sim, sim.players[1], false)
	return [sim, hud, sim.players[1]]


func _hand() -> CardHand:
	return (_shown()[1] as MatchHud).hand


func _weapon(id: StringName) -> WeaponDef:
	return load(WEAPON_DIR + String(id) + ".tres") as WeaponDef


func _weapon_ids() -> Array[StringName]:
	var ids: Array[StringName] = []
	for file: String in DirAccess.get_files_at(WEAPON_DIR):
		if file.ends_with(".tres"):
			ids.append(StringName(file.trim_suffix(".tres")))
	return ids


func _picture_files() -> Array[String]:
	var files: Array[String] = []
	for file: String in DirAccess.get_files_at(FOLDER):
		if file.ends_with(".webp"):
			files.append(file)
	return files


func _picture_node(card: HandCard) -> TextureRect:
	return card.get_node("Picture") as TextureRect


# ---- the files (adding a picture is data only) ------------------------------------

func test_every_file_in_the_folder_belongs_to_a_weapon() -> void:
	var ids: Array[StringName] = _weapon_ids()
	var files: Array[String] = _picture_files()
	assert_gte(files.size(), 14, "a picture for each of the 14 weapons")
	for file: String in files:
		var id: StringName = StringName(file.trim_suffix(".webp"))
		assert_true(ids.has(id), "%s is the card of a weapon (a file named like a weapon id: %s)" % [file, ids])
		assert_not_null(CardStyle.picture(id), "%s loads as a texture (its .import is there)" % file)
		assert_true(FileAccess.file_exists(FOLDER + file + ".import"), "%s has its .import" % file)


func test_every_weapon_has_a_picture() -> void:
	for id: StringName in _weapon_ids():
		assert_not_null(CardStyle.picture(id), "%s has the owner's card picture" % id)
		assert_true(WITH_PICTURE.has(id), "%s is in the list" % id)
	assert_eq(WITH_PICTURE.size(), _weapon_ids().size(), "the list is every weapon")
	assert_null(CardStyle.picture(&"no_such_weapon"), "an unknown id is no picture, not an error")


func test_the_pictures_are_trimmed_small_and_keep_the_cards_proportions() -> void:
	var total: int = 0
	for file: String in _picture_files():
		var texture: Texture2D = CardStyle.picture(StringName(file.trim_suffix(".webp")))
		assert_between(texture.get_width(), 400, 640, "%s: downscaled (about 512 px wide)" % file)
		var aspect: float = float(texture.get_width()) / float(texture.get_height())
		assert_between(aspect, 0.74, 0.79, "%s: trimmed to the card (the slot is 0.79 wide over tall), no empty margin" % file)
		total += FileAccess.get_file_as_bytes(FOLDER + file).size()
	assert_lt(total, FOLDER_BUDGET_BYTES, "the pictures stay small (the APK is close to its limit)")


func test_the_import_is_lossy_with_mipmaps_and_not_vram_compressed() -> void:
	for file: String in _picture_files():
		var text: String = FileAccess.get_file_as_string(FOLDER + file + ".import")
		assert_true(text.contains("compress/mode=1"), "%s: lossy WebP, no VRAM compression (a crisp 2D picture)" % file)
		assert_true(text.contains("mipmaps/generate=true"), "%s: mipmaps, it is drawn at a quarter of its size" % file)
		assert_false(text.contains("vram_texture\": true"), file)


# ---- fitting ----------------------------------------------------------------------

func test_a_picture_fits_its_slot_centred_with_its_own_proportions() -> void:
	var slot: Rect2 = Rect2(Vector2(40.0, 28.0), CardHand.card_size(720.0))
	for id: StringName in WITH_PICTURE:
		var texture: Texture2D = CardStyle.picture(id)
		var shown: Rect2 = CardStyle.picture_rect(slot, texture)
		assert_true(slot.grow(0.01).encloses(shown), "%s: inside its slot" % id)
		assert_almost_eq(shown.get_center(), slot.get_center(), Vector2(0.01, 0.01), "%s: centred" % id)
		assert_almost_eq(shown.size.x / shown.size.y, float(texture.get_width()) / float(texture.get_height()), 0.001, "%s: not stretched" % id)
		assert_gte(maxf(shown.size.x / slot.size.x, shown.size.y / slot.size.y), 0.999, "%s: touches the slot on one axis (as big as it can be)" % id)
		assert_gt(shown.size.x / slot.size.x, 0.9, "%s: the frame fills the slot (no empty margin)" % id)
	assert_eq(CardStyle.picture_rect(slot, null), slot, "no picture: the slot itself")
	var wide: Rect2 = Rect2(Vector2.ZERO, Vector2(200.0, 100.0))
	var inside: Rect2 = CardStyle.picture_rect(wide, CardStyle.picture(&"heal"))
	assert_true(wide.encloses(inside), "a wide slot also fits it inside")


# ---- the card ---------------------------------------------------------------------

func test_a_picture_weapon_is_drawn_as_its_picture_bare_with_no_ground_wear_tint_or_power_line() -> void:
	for id: StringName in WITH_PICTURE:
		var card: HandCard = autofree(HandCard.new()) as HandCard
		add_child(card)
		card.show_weapon(_weapon(id))
		card.place(Rect2(Vector2.ZERO, CardHand.card_size(720.0)))
		assert_true(card.has_picture(), "%s has its picture" % id)
		assert_true(card.bare, "%s: a bare card, the picture as made" % id)
		assert_eq(_picture_node(card).texture, CardStyle.picture(id), "%s: its own picture" % id)
		assert_null(_picture_node(card).material, "%s: unshaded (no worn paper shader)" % id)
		assert_eq(_picture_node(card).stretch_mode, TextureRect.STRETCH_KEEP_ASPECT_CENTERED, "%s: fitted, never stretched" % id)
		assert_eq(card.ground.a, 0.0, "%s: no ground colour printed under it" % id)
		assert_false((card.get_node("Tint") as ColorRect).visible, "%s: no age tint" % id)
		assert_false((card.get_node("PowerLine") as CardPowerLine).visible, "%s: no power line" % id)
		assert_eq((card.get_node("PowerLine") as CardPowerLine).text, "", "%s: and no power text" % id)
		assert_eq(card.art_inset, 0.0, "%s: no art inset (there is no card paper to inset from)" % id)


func test_a_weapon_without_a_picture_keeps_the_teks_card_with_the_painted_icon() -> void:
	_no_picture()
	for id: StringName in TEKS_ONLY:
		var def: WeaponDef = _weapon(id)
		var card: HandCard = autofree(HandCard.new()) as HandCard
		add_child(card)
		card.show_weapon(def)
		card.place(Rect2(Vector2.ZERO, CardHand.card_size(720.0)))
		assert_false(card.has_picture(), id)
		assert_false(card.bare, "%s: the worn teks card" % id)
		assert_eq(_picture_node(card).texture, Icons.skill(id), "%s: with its painted skill icon" % id)
		assert_eq(card.ground, CardStyle.ground(def.kind), "%s: printed in its kind colour" % id)
		assert_eq(((_picture_node(card).material) as ShaderMaterial).shader.resource_path, "res://shaders/card_picture.gdshader", "%s: worn" % id)
		assert_true((card.get_node("Tint") as ColorRect).visible, "%s: aged" % id)
		assert_eq((card.get_node("PowerLine") as CardPowerLine).text, "POWER %d" % def.card_power, "%s: with its power line" % id)
		assert_null(card.material, "%s: no silhouette material on a teks card" % id)


func test_a_card_that_cycles_between_a_picture_and_a_teks_card_is_fully_each_one() -> void:
	_no_picture()
	var card: HandCard = autofree(HandCard.new()) as HandCard
	add_child(card)
	card.place(Rect2(Vector2.ZERO, CardHand.card_size(720.0)))
	card.show_weapon(_weapon(&"heal"))
	card.show_weapon(_weapon(&"lata"))
	assert_false(card.has_picture())
	assert_false(card.bare)
	assert_not_null(_picture_node(card).material, "the worn shader is back")
	assert_true((card.get_node("Tint") as ColorRect).visible)
	assert_eq((card.get_node("PowerLine") as CardPowerLine).text, "POWER %d" % _weapon(&"lata").card_power)
	card.show_weapon(_weapon(&"papel_trap"))
	assert_true(card.has_picture())
	assert_null(_picture_node(card).material)
	assert_eq((card.get_node("PowerLine") as CardPowerLine).text, "")
	card.show_back(2)
	assert_false(card.has_picture(), "a card back is no picture")
	assert_false(card.bare)
	assert_eq(card.ground, CardStyle.BACK_GROUND, "dark cardboard")


func test_the_hand_shows_the_pictures_in_its_four_slots_and_next() -> void:
	var hand: CardHand = _hand()
	for slot: int in CardHand.SLOTS:
		var card: HandCard = hand.card_node(slot)
		assert_true(card.has_picture(), "slot %d" % slot)
		assert_eq(card.picture, CardStyle.picture(PICTURE_HAND[slot]))
		assert_eq(card.position, hand.card_rects[slot].position, "the card node is the slot")
		assert_eq(card.size, hand.card_rects[slot].size)
		var shown: Rect2 = card.picture_rect()
		assert_true(Rect2(Vector2.ZERO, card.size).grow(0.01).encloses(shown), "the picture is inside the slot")
	assert_true(hand.next_node().has_picture(), "NEXT is the picture too, smaller")
	assert_eq(hand.next_node().picture, CardStyle.picture(PICTURE_NEXT))
	assert_almost_eq(hand.next_node().size.x, hand.card_rects[0].size.x * CardHand.NEXT_SCALE, 0.6)


func test_the_picture_dims_when_the_hand_is_blocked_and_comes_back() -> void:
	var hand: CardHand = _hand()
	var card: HandCard = hand.card_node(0)
	assert_eq(_picture_node(card).modulate, Color.WHITE, "as made")
	hand.blocked_text = "STUNNED"
	hand._refresh()
	assert_eq(card.dim, CardHand.BLOCK_DIM)
	assert_lt(_picture_node(card).modulate.r, 0.8, "darker while blocked (a tint of the bare picture, alpha kept)")
	assert_eq(_picture_node(card).modulate.a, 1.0)
	assert_eq(card.shade_all.a, 0.0, "a blocked card is not also washed grey")
	hand.blocked_text = ""
	hand._refresh()
	assert_eq(_picture_node(card).modulate, Color.WHITE)


# ---- over the card: cost, washes, flash -------------------------------------------

func test_the_cost_drop_is_inside_the_card_and_inside_the_picture() -> void:
	var hand: CardHand = _hand()
	for slot: int in CardHand.SLOTS:
		var card: HandCard = hand.card_node(slot)
		var slot_rect: Rect2 = Rect2(card.position, card.size)
		var art: Rect2 = CardHand.art_rect(card, slot_rect)
		var drop: Rect2 = CardHand.cost_drop_rect(art)
		assert_true(slot_rect.encloses(drop), "slot %d: the drop is inside the card rect" % slot)
		assert_true(art.encloses(drop), "slot %d: and inside the picture, on its bottom edge" % slot)
		assert_gt(drop.end.y, art.end.y - art.size.y * 0.1, "slot %d: at the bottom" % slot)


func test_a_card_the_bar_has_not_reached_is_washed_from_the_pictures_own_silhouette() -> void:
	var hand: CardHand = _hand()
	hand.pickable[1] = false
	hand.reached[1] = 0.4
	hand._refresh()
	var card: HandCard = hand.card_node(1)
	var shown: Rect2 = card.picture_rect()
	assert_eq(card.shade_all, CardHand.GREY_REACHED, "lighter over the whole picture")
	assert_eq(card.shade_top, CardHand.GREY, "dark over the share still missing")
	assert_almost_eq(card.shade_top_px, shown.size.y * 0.6, 0.01, "the missing 60% from the top of the PICTURE")
	var shade: Control = card.get_node("Shade") as Control
	var material: ShaderMaterial = shade.material as ShaderMaterial
	assert_eq(material.shader.resource_path, "res://shaders/silhouette.gdshader", "drawn flat from the picture's alpha, so the empty corners stay empty")
	assert_eq(shade.size, card.size)
	assert_eq(hand.card_node(0).shade_all.a, 0.0, "an affordable card has no wash")
	hand.reached[1] = 1.0
	hand.pickable[1] = true
	hand._refresh()
	assert_eq(card.shade_all.a, 0.0, "reached: the wash is gone")
	assert_eq(card.shade_top_px, 0.0)


func test_the_silhouette_shader_uses_only_the_pictures_alpha() -> void:
	var source: String = FileAccess.get_file_as_string("res://shaders/silhouette.gdshader")
	assert_true(source.contains("texture(TEXTURE, UV).a"), "its alpha")
	assert_false(source.contains("texture(TEXTURE, UV).rgb"), "never its colours")
	assert_true(source.contains("tint.rgb"), "the colour it is drawn with")


func test_a_denied_picture_card_flashes_red_from_its_silhouette_and_shakes_with_motion_only() -> void:
	var hand: CardHand = _hand()
	hand.deny(2)
	hand.step(0.05)
	hand._refresh()
	var card: HandCard = hand.card_node(2)
	assert_gt(card.flash.a, 0.0, "the red flash is on")
	assert_gt(card.flash.r, card.flash.g, "red")
	assert_ne(card.position.x, hand.card_rects[2].position.x, "it shakes")
	Settings.reduced_motion = true
	hand._refresh()
	assert_gt(card.flash.a, 0.0, "Reduce motion keeps the flash")
	assert_eq(card.position.x, hand.card_rects[2].position.x, "and removes the shake")
	hand._deny[2] = 0.0
	hand._refresh()
	assert_eq(card.flash.a, 0.0, "it ends")


func test_next_is_shaded_from_the_bottom_up_while_it_is_shuffled_in() -> void:
	var hand: CardHand = _hand()
	hand.next_fill = 0.25
	hand._refresh()
	var next: HandCard = hand.next_node()
	assert_eq(next.shade_top, CardStyle.FILLING_SHADE)
	assert_almost_eq(next.shade_top_px, next.picture_rect().size.y * 0.75, 0.01)
	hand.next_fill = 1.0
	hand._refresh()
	assert_eq(next.shade_top_px, 0.0)


# ---- the lift --------------------------------------------------------------------

func test_the_held_picture_card_follows_the_finger_lifts_and_leaves_its_slot_empty() -> void:
	var hand: CardHand = _hand()
	hand.active = true
	hand.held_slot = 1
	hand.over_hand = true
	hand.finger = hand.card_rects[1].get_center() + Vector2(10.0, -40.0)
	hand._refresh()
	var held: HandCard = hand.held_node()
	assert_false(hand.card_node(1).visible, "the slot it came from shows the empty dent")
	assert_true(held.visible)
	assert_true(held.has_picture(), "the picture follows the finger")
	assert_eq(held.picture, CardStyle.picture(&"papel_shield"))
	assert_almost_eq(held.position, hand.held_card_rect().position, Vector2(0.01, 0.01))
	assert_true(held.shadowed, "lifted: the soft shadow and the gold edge, from the picture's silhouette, behind it")
	assert_eq((held.material as ShaderMaterial).shader.resource_path, "res://shaders/silhouette.gdshader", "drawn flat in gold and shadow, so the edge follows the torn border")
	assert_false(hand.card_node(1).shadowed, "a card lying in the hand has no lift")
	assert_gt(CardStyle.LIFT_EDGE, 2.0, "the gold edge is thick enough to read")
	assert_gte(CardStyle.PICTURE_EDGE_STEPS, 8, "round the whole silhouette")
	assert_gt(CardStyle.PICTURE_SHADOW_DROP, 0.0, "a shadow below")
	assert_eq(held.shade_all.a, 0.0, "no wash on the card under the finger")
	hand.over_hand = false
	hand.finger = Vector2(300.0, -200.0)
	hand._refresh()
	assert_almost_eq(hand.held_node().size.x, hand.card_rects[1].size.x * CardHand.STREET_SCALE, 0.6, "shrunk over the street, still the picture")
	assert_true(hand.held_node().has_picture())


func test_a_teks_card_under_the_finger_keeps_its_polygon_lift() -> void:
	_no_picture()
	var hand: CardHand = (_shown([&"bato_light", &"lata", &"tsinelas_light", &"tsinelas_heavy"], &"bato_light")[1] as MatchHud).hand
	hand.active = true
	hand.held_slot = 1
	hand.over_hand = true
	hand.finger = hand.card_rects[1].get_center() + Vector2(10.0, -40.0)
	hand._refresh()
	var held: HandCard = hand.held_node()
	assert_false(held.has_picture())
	assert_true(held.shadowed, "the same polygon shadow as before")
	assert_null(held.material, "no silhouette material: its gold line is the card's own worn outline, drawn over it")
	assert_eq(held.outline().size() > 4, true, "with the worn outline's corners")


# ---- the flight ------------------------------------------------------------------

func test_the_flight_card_is_the_same_picture_as_the_hand_card() -> void:
	var parent: Control = autofree(Control.new()) as Control
	add_child(parent)
	var flight: CardFlight = CardFlight.fly(parent, _weapon(&"gunting_heavy"), Rect2(Vector2(100.0, 100.0), Vector2(120.0, 150.0)), Rect2(Vector2(300.0, 100.0), Vector2(60.0, 75.0)))
	assert_not_null(flight)
	var card: HandCard = flight.get_node("FlyingCard") as HandCard
	assert_true(card.has_picture())
	assert_eq(card.picture, CardStyle.picture(&"gunting_heavy"))
	assert_true(card.bare)
	assert_eq((card.get_node("PowerLine") as CardPowerLine).text, "")
	flight._process(CardFlight.TIME / 2.0)
	assert_gt(card.dim, 0.0)
	assert_lt(_picture_node(card).modulate.r, 1.0, "a little darker as it goes")
	assert_almost_eq(card.position, flight.rect_at(0.5).position, Vector2(0.01, 0.01), "along its arc")
	assert_eq(card.texture_filter, CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS, "smooth outside the hand too")


func test_a_card_that_arrives_from_next_flies_as_its_picture() -> void:
	var shown: Array = _shown()
	var sim: MatchSim = shown[0] as MatchSim
	var hud: MatchHud = shown[1] as MatchHud
	var player: PlayerState = shown[2] as PlayerState
	sim.cycle_used(player, 2)
	hud.sync_player(sim, player, false)
	var flights: Array[CardFlight] = []
	for child: Node in hud.hand.get_children():
		if child is CardFlight:
			flights.append(child as CardFlight)
	assert_eq(flights.size(), 2, "the used card out, NEXT in")
	var leaving: HandCard = flights[0].get_node("FlyingCard") as HandCard
	var arriving: HandCard = flights[1].get_node("FlyingCard") as HandCard
	assert_eq(leaving.picture, CardStyle.picture(&"papel_trap"), "the card that was played leaves as its picture")
	assert_eq(arriving.picture, CardStyle.picture(PICTURE_NEXT), "the card that was NEXT arrives as its picture")
	for flight: CardFlight in flights:
		flight.queue_free()


# ---- the rest of the hand --------------------------------------------------------

func test_a_hand_can_mix_picture_cards_and_teks_cards() -> void:
	_no_picture()
	var hand: CardHand = (_shown([&"heal", &"lata", &"jacks", &"tsinelas_light"], &"bola")[1] as MatchHud).hand
	var expected: Array[bool] = [true, false, true, false]
	for slot: int in CardHand.SLOTS:
		assert_eq(hand.card_node(slot).has_picture(), expected[slot], "slot %d" % slot)
		assert_eq(hand.card_node(slot).bare, expected[slot])
		assert_eq(hand.card_node(slot).size, hand.card_rects[slot].size, "the same slot size either way")
	assert_true(hand.next_node().has_picture())
	# a teks card keeps its drop on its own rect, a picture card on the picture's
	assert_eq(CardHand.art_rect(hand.card_node(1), hand.card_rects[1]), hand.card_rects[1])
	assert_ne(CardHand.art_rect(hand.card_node(0), hand.card_rects[0]), hand.card_rects[0])
	hand.pickable[1] = false
	hand.reached[1] = 0.5
	hand._refresh()
	assert_eq(hand.card_node(1).shade_all.a, 0.0, "a teks card is washed by the hand's polygons, not by a silhouette")


func test_a_card_back_and_an_empty_slot_are_unchanged() -> void:
	_rules.elixir_enabled = false
	var shown: Array = _shown()
	var sim: MatchSim = shown[0] as MatchSim
	var hud: MatchHud = shown[1] as MatchHud
	var player: PlayerState = shown[2] as PlayerState
	sim.cycle_used(player, 0)
	sim.cycle_used(player, 1)
	for i: int in 3:
		sim.step(1.0 / 30.0)
	hud.sync_player(sim, player, false)
	var back: HandCard = hud.hand.card_node(1)
	assert_true(back.back)
	assert_false(back.has_picture())
	assert_eq(back.ground, CardStyle.BACK_GROUND)
	assert_eq(_picture_node(back).material.get("shader").resource_path, "res://shaders/card_picture.gdshader")


func test_left_handed_mirrors_the_picture_cards_with_the_hand() -> void:
	var hand: CardHand = _hand()
	hand.set_mirrored(true)
	for slot: int in CardHand.SLOTS:
		assert_eq(hand.card_node(slot).position, hand.card_rects[slot].position, "slot %d follows the mirrored layout" % slot)
		assert_true(hand.card_node(slot).has_picture())
	assert_gt(hand.next_node().position.x, hand.card_node(3).position.x, "NEXT at the right")


func test_the_mode_cards_dim_and_shadow_are_untouched() -> void:
	var face: CardFace = autofree(CardFace.new()) as CardFace
	add_child(face)
	face.dim = 0.5
	assert_eq(face.get_node("Picture").modulate, Color.WHITE, "a worn card dims in its shader, not by a tint")
	face.bare = true
	assert_lt(face.get_node("Picture").modulate.r, 1.0, "a bare diorama is tinted dark")
	face.bare = false
	assert_eq(face.get_node("Picture").modulate, Color.WHITE)


func test_every_state_of_a_picture_hand_draws_without_errors() -> void:
	var hand: CardHand = _hand()
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
	hand.blocked_text = ""
	hand.held_slot = 3
	hand.over_hand = true
	hand.finger = hand.card_rects[3].get_center()
	hand._refresh()
	await wait_frames(2)
	assert_true(is_instance_valid(hand))
