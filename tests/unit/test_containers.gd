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


func test_a_feed_line_sits_on_a_team_coloured_pill() -> void:
	var practice: PracticeMatch = _practice()
	practice.hud.feed(MatchHud.Feed.TAGGED, true, "Junjun")
	practice.hud.feed(MatchHud.Feed.RESCUED, false, "Inday")
	var lines: Array[Node] = practice.hud.feed_box.get_children()
	assert_eq(lines.size(), 2)
	for line: Node in lines:
		var box: StyleBox = (line as Label).get_theme_stylebox("normal")
		assert_true(box is StyleBoxFlat, "a pill behind it")
	assert_ne((lines[0] as Label).get_theme_stylebox("normal").get("bg_color"), (lines[1] as Label).get_theme_stylebox("normal").get("bg_color"), "blue for your team, red for theirs")


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


func test_portraits_in_the_tagged_sheet_have_a_ring_but_no_second_fill() -> void:
	var practice: PracticeMatch = _practice()
	var box: StyleBoxFlat = practice.hud.tagged_panel._card_box(Color.BLUE, false)
	assert_eq(box.bg_color.a, 0.0, "the sheet is already the container")
	assert_gt(box.border_width_left, 0, "a ring shows it can be tapped and whose team it is")
	assert_gt(practice.hud.tagged_panel._card_box(Color.BLUE, true).border_width_left, box.border_width_left, "thicker on the one you watch")
