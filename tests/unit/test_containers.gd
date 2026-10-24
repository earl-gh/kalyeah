extends GutTest
## Where a container earns its place (docs/audits/2026-10-20-containers.md): small state text read over the
## street (score, feed, skill type) sits on one; a container is never put inside another container.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")


func _practice() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.finish_pick()
	practice.skip_intro()
	return practice


func test_the_scoreboard_keeps_its_card() -> void:
	var board: Scoreboard = autofree(Scoreboard.new()) as Scoreboard
	assert_gte(Scoreboard.BIG_FONT, 40, "big numbers on a card, not bare text on the street")
	assert_true(UiSkin.plate("night", 0.0, 18) is StyleBoxTexture)
	assert_not_null(board)


func test_the_opening_words_gear_and_loadout_sockets_have_no_container_of_their_own() -> void:
	var practice: PracticeMatch = _practice()
	assert_false(practice.hud.intro_bar is Panel, "big words with an outline: no plate")
	var siblings: Array[Node] = practice.hud.top.get_children().filter(func(n: Node) -> bool: return n is Panel)
	assert_eq(siblings.size(), 0, "no plate behind the gear: its art has its own ring")
	var screen: WeaponPickScreen = autofree(WeaponPickScreen.new()) as WeaponPickScreen
	add_child(screen)
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var ids: Array[StringName] = []
	for def: WeaponDef in rules.weapons:
		ids.append(def.id)
	screen.open(WeaponPick.new(ids, 30.0), rules.weapons, "", RandomNumberGenerator.new())
	for socket: LoadoutSocket in screen._sockets:
		assert_false(socket.get_parent().get_parent() is PanelContainer, "the sockets are the plates: no plate around them")


func test_the_tagged_message_has_no_player_cards_and_no_sheet() -> void:
	var practice: PracticeMatch = _practice()
	practice.hurt_local(100)
	practice.advance(1.0 / 30.0)
	var panel: TaggedPanel = practice.hud.tagged_panel
	assert_true(panel.visible)
	assert_eq(panel.find_children("*", "PanelContainer", true, false).size(), 0, "the console deck is the container")
	var buttons: Array[Node] = panel.find_children("*", "Button", true, false)
	assert_eq(buttons, [panel.swap_button] as Array[Node], "nothing to pick or watch: the only button is Change weapons")
