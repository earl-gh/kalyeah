extends GutTest
## Round 15 of the interface plan: the floors every screen is held to, so they stay fixed. Opens the screens, then
## fails on text smaller than UiMetrics.MIN_TEXT, a tappable control shorter than UiMetrics.MIN_TARGET, or a
## phrase that is no longer true ("skills", "dash", "dummies", "..." and "•" in player-facing words).

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")
const LOBBY_SCENE: PackedScene = preload("res://scenes/ui/lobby.tscn")
const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0
## Words that must not appear in anything a player reads.
const BANNED: Array[String] = ["skill", "dash", "dummies", "...", "•", "Lobby", "that address"]
## Source folders whose string literals are read by players.
const SOURCES: Array[String] = ["res://scripts/ui", "res://scripts/view"]

var _motion: bool
var _history_path: String


func before_each() -> void:
	_motion = Settings.reduced_motion
	_history_path = MatchHistory.path
	MatchHistory.path = "user://history_lint.cfg"
	MatchHistory.clear()
	for entry: Array in [["practice", true, 2, 0], ["hotspot", false, 0, 2], ["online", true, 2, 1]]:
		MatchHistory.record(entry[0] as String, entry[1] as bool, entry[2] as int, entry[3] as int)
	Settings.set_reduced_motion(true)


func after_each() -> void:
	MatchHistory.clear()
	MatchHistory.path = _history_path
	Settings.set_reduced_motion(_motion)


## What is wrong on screen under `root`: text under the floor, short targets, banned words. Empty when fine.
func _problems(root: Node, where: String) -> Array[String]:
	var found: Array[String] = []
	for node: Node in root.find_children("*", "Label", true, false):
		var label: Label = node as Label
		if not label.is_visible_in_tree() or label.text.is_empty():
			continue
		var size: int = label.get_theme_font_size("font_size")
		if size < UiMetrics.MIN_TEXT:
			found.append("%s: text '%s' is %d px (floor %d)" % [where, label.text.substr(0, 28), size, UiMetrics.MIN_TEXT])
		for word: String in BANNED:
			if label.text.containsn(word):
				found.append("%s: '%s' says '%s'" % [where, label.text.substr(0, 40), word])
	for node: Node in root.find_children("*", "BaseButton", true, false):
		var button: Control = node as Control
		if not button.is_visible_in_tree():
			continue
		var rect: Rect2 = button.get_global_rect()
		if rect.size.x < 1.0 or rect.size.y < 1.0:
			continue
		if minf(rect.size.x, rect.size.y) < UiMetrics.MIN_TARGET:
			found.append("%s: '%s' (%s) is %dx%d px (floor %d)" % [where, button.name, (button as Button).text if button is Button else "", rect.size.x, rect.size.y, UiMetrics.MIN_TARGET])
		if button is Button:
			for word: String in BANNED:
				if (button as Button).text.containsn(word):
					found.append("%s: button '%s' says '%s'" % [where, (button as Button).text, word])
		if not UiA11y.is_named(button):
			found.append("%s: '%s' (%s) has no text and no accessibility name [%s]" % [where, button.name, button.get_class(), str(button.get_path()).get_slice("/", 4) + "/" + str(button.get_parent().name)])
	for node: Node in root.find_children("*", "HSlider", true, false):
		var slider: Control = node as Control
		if slider.is_visible_in_tree() and not UiA11y.is_named(slider):
			found.append("%s: slider '%s' has no accessibility name" % [where, slider.name])
	for node: Node in root.find_children("*", "LineEdit", true, false):
		var field: Control = node as Control
		if field.is_visible_in_tree() and not UiA11y.is_named(field):
			found.append("%s: text field '%s' has no accessibility name" % [where, field.name])
	for kind: String in ["CardHand", "MenuCarousel"]:
		for node: Node in root.find_children("*", kind, true, false):
			var drawn: Control = node as Control
			if drawn.is_visible_in_tree() and drawn.accessibility_name.is_empty():
				found.append("%s: %s '%s' has no accessibility name" % [where, kind, drawn.name])
	return found


func _phone() -> TitleScreen:
	var phone: Control = autofree(Control.new()) as Control
	phone.custom_minimum_size = Vector2(720.0, 1280.0)
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	phone.add_child(screen)
	return screen


func _check(problems: Array[String]) -> void:
	assert_eq(problems, [] as Array[String], "\n".join(problems))


# ---- Home and its pages -------------------------------------------------------------------

func test_home_in_every_mode_meets_the_floors() -> void:
	var screen: TitleScreen = _phone()
	await wait_process_frames(4)
	var all: Array[String] = []
	for mode: int in 3:
		screen._on_mode_selected(mode)
		await wait_process_frames(2)
		all.append_array(_problems(screen, "home mode %d" % mode))
	_check(all)


func test_every_page_and_sheet_meets_the_floors() -> void:
	var screen: TitleScreen = _phone()
	await wait_process_frames(4)
	var all: Array[String] = []
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	for id: StringName in [&"codex", &"history", &"settings", &"howto", &"gallery"]:
		screen.open_page(id)
		await wait_process_frames(3)
		all.append_array(_problems(screen, "page %s" % id))
	screen.open_page(&"codex")
	screen.codex_page.show_tab(&"weapons")
	await wait_process_frames(2)
	all.append_array(_problems(screen, "weapons by game"))
	screen.weapons_page.show_grouping(&"type")
	await wait_process_frames(2)
	all.append_array(_problems(screen, "weapons by type"))
	screen.open_weapon_detail(rules.weapons[6])
	await wait_process_frames(3)
	all.append_array(_problems(screen, "weapon sheet"))
	screen.dialog.close(false)
	screen.codex_page.show_tab(&"characters")
	screen.open_character_detail(load("res://data/characters/migo.tres") as CharacterDef)
	await wait_process_frames(3)
	all.append_array(_problems(screen, "kid sheet"))
	screen.dialog.close(false)
	screen.open_name_editor()
	await wait_process_frames(3)
	all.append_array(_problems(screen, "profile sheet"))
	screen.dialog.close(false)
	screen.open_quit_confirm()
	await wait_process_frames(3)
	all.append_array(_problems(screen, "quit"))
	screen.dialog.close(false)
	_check(all)


# ---- rooms ----------------------------------------------------------------------------------

func test_the_join_and_create_forms_meet_the_floors() -> void:
	var all: Array[String] = []
	for intent: int in [0, 1]:
		Session.lobby_hotspot = false
		Session.lobby_preview = false
		Session.lobby_intent = intent
		var lobby: Node = autofree(LOBBY_SCENE.instantiate())
		add_child(lobby)
		await wait_process_frames(4)
		all.append_array(_problems(lobby, "lobby intent %d" % intent))
		lobby.queue_free()
		await wait_process_frames(2)
	_check(all)


func test_the_room_views_meet_the_floors() -> void:
	var all: Array[String] = []
	var members: Array = [
		{"player_id": 1, "name": "Ana", "team": 0, "ready": true, "connected": true, "char": "junjun"},
		{"player_id": 2, "name": "Ben", "team": 1, "ready": false, "connected": true, "char": "migo"}]
	for online: bool in [true, false]:
		for me: int in [1, 2]:
			var holder: Control = autofree(Control.new()) as Control
			holder.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
			add_child(holder)
			var view: HotspotRoomView = HotspotRoomView.new()
			view.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
			holder.add_child(view)
			view.set_online(online)
			view.show_room({"code": "K7M2Q9XA" if online else "", "team_size": 2, "host": 1, "first_to": 3, "state": 0, "members": members, "can_start": false, "can_start_host": me == 1, "map": &"street"}, me)
			await wait_process_frames(3)
			all.append_array(_problems(view, "room online=%s me=%d" % [online, me]))
			holder.queue_free()
			await wait_process_frames(2)
	_check(all)


# ---- the match ------------------------------------------------------------------------------

func test_the_loadout_and_the_match_meet_the_floors() -> void:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	await wait_process_frames(4)
	var all: Array[String] = _problems(practice, "loadout")
	practice.finish_pick()
	practice.skip_intro()
	for i: int in 20:
		practice.advance(DT)
	await wait_process_frames(2)
	all.append_array(_problems(practice, "match"))
	practice.hud.toggle_menu()
	await wait_process_frames(2)
	all.append_array(_problems(practice, "match menu"))
	practice.hud.toggle_menu()
	practice.sim.damage(practice.local_id, 9999)
	for i: int in 10:
		practice.advance(DT)
	await wait_process_frames(2)
	all.append_array(_problems(practice, "tagged"))
	_check(all)


func test_the_text_drawn_on_the_buttons_and_over_the_kids_is_at_the_floor() -> void:
	assert_gte(CardHand.COST_FONT, UiMetrics.MIN_TEXT, "a card's cost and its tag")
	assert_gte(CardHand.BAR_FONT, UiMetrics.MIN_TEXT, "the elixir number")
	assert_gte(ClockChip.CAPTION_FONT, UiMetrics.MIN_TEXT, "TIME over the clock")
	assert_gte(OverheadHud.COUNT_FONT, UiMetrics.MIN_TEXT, "the health number over a bar")
	assert_gte(CardHand.NEXT_FONT, UiMetrics.MIN_TEXT, "NEXT over the next card")
	assert_gte(OverheadHud.STATUS_FONT, UiMetrics.MIN_TEXT, "the status over a kid")


# ---- words ----------------------------------------------------------------------------------

func test_no_player_facing_string_in_the_code_says_a_banned_thing() -> void:
	var found: Array[String] = []
	var ellipsis: RegEx = RegEx.create_from_string("\"[^\"\\n]*\\.\\.\\.\"")
	for folder: String in SOURCES:
		for file: String in DirAccess.get_files_at(folder):
			if not file.ends_with(".gd"):
				continue
			var text: String = FileAccess.get_file_as_string("%s/%s" % [folder, file])
			var number: int = 0
			for line: String in text.split("\n"):
				number += 1
				var code: String = line.strip_edges()
				if code.begins_with("#") or code.begins_with("##"):
					continue
				if ellipsis.search(code) != null and not code.contains("ends_with"):
					found.append("%s/%s:%d three dots in a string (use …)" % [folder, file, number])
				if code.contains("•") and not code.contains("# "):
					found.append("%s/%s:%d a bullet (use ·)" % [folder, file, number])
	assert_eq(found, [] as Array[String], "\n".join(found))


func test_the_boot_tips_are_true() -> void:
	for tip: String in BootScreen.TIPS:
		for word: String in BANNED:
			assert_false(tip.containsn(word), "'%s' says '%s'" % [tip, word])
		assert_true(tip.ends_with("."), tip)


func test_a_dialog_opened_by_touch_has_no_focus_ring_until_a_key_is_pressed() -> void:
	var kept: bool = Settings.keyboard_active
	Settings.keyboard_active = false
	var dialog: MenuDialog = MenuDialog.create("ASK")
	var cancel: Button = MenuDialog.make_button("Cancel", "grey")
	var sure: Button = MenuDialog.make_button("Sure", "orange")
	dialog.add_buttons([cancel, sure])
	dialog.open(self)
	await wait_process_frames(3)
	assert_false(cancel.has_focus() or sure.has_focus(), "no ring when it was opened by touch")
	var tab: InputEventKey = InputEventKey.new()
	tab.keycode = KEY_TAB
	tab.pressed = true
	Input.parse_input_event(tab)
	await wait_process_frames(2)
	assert_true(cancel.has_focus(), "the first key press brings it")
	dialog.close(false)
	Settings.keyboard_active = true
	var again: MenuDialog = MenuDialog.create("ASK")
	var first: Button = MenuDialog.make_button("Cancel", "grey")
	again.add_buttons([first])
	again.open(self)
	await wait_process_frames(3)
	assert_true(first.has_focus(), "after the keyboard was used it opens with the ring")
	again.close(false)
	Settings.keyboard_active = kept


func test_a_text_field_always_takes_focus_so_the_keyboard_can_open() -> void:
	Settings.keyboard_active = false
	var dialog: MenuDialog = MenuDialog.create("NAME")
	var field: LineEdit = LineEdit.new()
	dialog.body.add_child(field)
	dialog.add_buttons([MenuDialog.make_button("Save", "orange")])
	dialog.open(self)
	await wait_process_frames(3)
	assert_true(field.has_focus())
	dialog.close(false)


# ---- shapes and type (appended last: placed mid-file, a scene test's children were reported unfreed) ----------

## The interface's folders whose corners and title sizes must come from the shared scales. `ui_skin.gd` holds the scale.
const SHAPE_SOURCES: Array[String] = ["res://scripts/ui", "res://scripts/ui/hud"]
## Each shape call and the index of its corner-radius argument (`surface_box`'s second argument is a border width,
## `plate`'s second its padding, `chunky`'s first three a canvas, a rect and a fill).
const RADIUS_ARGS: Dictionary[String, int] = {
	"set_corner_radius_all": 0, "focus_ring": 0, "pill": 0, "surface_box": 0, "plate": 2, "chunky": 3,
}


## Every whole-number literal in `code` that is not part of a decimal or a name (16 counts, 16.0 padding and RADIUS_2 do not).
func _bare_whole_number(code: String) -> bool:
	return RegEx.create_from_string("(?<![\\w.])\\d+(?![\\w.])").search(code) != null


## The arguments of the call whose `(` is at `open`, split on top-level commas (a call may span lines).
func _call_args(text: String, open: int) -> Array[String]:
	var args: Array[String] = []
	var depth: int = 0
	var start: int = open + 1
	for i: int in range(open, text.length()):
		var c: String = text[i]
		if c == "(" or c == "[" or c == "{":
			depth += 1
		elif c == ")" or c == "]" or c == "}":
			depth -= 1
			if depth == 0:
				args.append(text.substr(start, i - start).strip_edges())
				return args
		elif c == "," and depth == 1:
			args.append(text.substr(start, i - start).strip_edges())
			start = i + 1
	return args


## The source line holding `at`, and whether `at` sits in a comment on it.
func _line_of(text: String, at: int) -> Dictionary:
	var begin: int = text.rfind("\n", at) + 1
	var end: int = text.find("\n", at)
	var line: String = text.substr(begin, (text.length() if end < 0 else end) - begin)
	return {"number": text.substr(0, at).count("\n") + 1, "code": line.strip_edges(), "comment": text.substr(begin, at - begin).contains("#")}


## Every radius-bearing spot in one source file that holds a bare whole number.
func _bare_radii(path: String) -> Array[String]:
	var found: Array[String] = []
	var text: String = FileAccess.get_file_as_string(path)
	var call: RegEx = RegEx.create_from_string("\\b(" + "|".join(RADIUS_ARGS.keys()) + ")\\(")
	for hit: RegExMatch in call.search_all(text):
		var where: Dictionary = _line_of(text, hit.get_start())
		if where["comment"] or (where["code"] as String).begins_with("static func") or (where["code"] as String).begins_with("func "):
			continue
		var args: Array[String] = _call_args(text, hit.get_end() - 1)
		var index: int = RADIUS_ARGS[hit.get_string(1)]
		if index < args.size() and _bare_whole_number(args[index]):
			found.append("%s:%d %s" % [path, where["number"], where["code"]])
	# `corner_radius_top_left = 12`, and `const BADGE_RADIUS: int = 12` / `corner: int = 12` (a constant, a variable or a default)
	var named: RegEx = RegEx.create_from_string("(?i)(corner_radius\\w*\\s*=(?!=)|\\w*(radius|corner)\\w*\\s*:\\s*int\\s*=)([^\\n,)#]*)")
	for hit: RegExMatch in named.search_all(text):
		var where: Dictionary = _line_of(text, hit.get_start())
		if not where["comment"] and _bare_whole_number(hit.get_string(3)):
			found.append("%s:%d %s" % [path, where["number"], where["code"]])
	return found


## One corner family (docs/ART.md "Shared UI hierarchy"): every corner radius in the interface is UiSkin.RADIUS_S/M/L/XL or
## UiSkin.round_radius of a height, never a bare number, so the many one-off radii the screens had cannot creep back.
## Only UiSkin itself holds the numbers.
func test_every_corner_radius_comes_from_the_scale() -> void:
	var found: Array[String] = []
	for folder: String in SHAPE_SOURCES:
		for file: String in DirAccess.get_files_at(folder):
			if file.ends_with(".gd") and file != "ui_skin.gd":
				found.append_array(_bare_radii("%s/%s" % [folder, file]))
	assert_eq(found, [] as Array[String], "%d radii must come from UiSkin.RADIUS_* or UiSkin.round_radius:\n%s" % [found.size(), "\n".join(found)])


## The lint above catches what it should and nothing else: a padding float, a border width and a named step pass.
func test_the_corner_lint_tells_a_radius_from_a_padding_or_a_border() -> void:
	assert_eq(_bare_radii_in("UiSkin.plate(\"glass\", 16.0, UiSkin.RADIUS_M)"), 0, "padding floats and named steps pass")
	assert_eq(_bare_radii_in("KalyeahTheme.surface_box(UiSkin.RADIUS_L, 2)"), 0, "surface_box's border width is not a radius")
	assert_eq(_bare_radii_in("RoyaleStyle.chunky(self, Rect2(0, 0, 40, 40), fill, UiSkin.RADIUS_S)"), 0, "a rect is not a radius")
	assert_eq(_bare_radii_in("UiSkin.plate(\"glass\", 16.0, 26)"), 1)
	assert_eq(_bare_radii_in("KalyeahTheme.surface_box(14, 2)"), 1)
	assert_eq(_bare_radii_in("box.set_corner_radius_all(\n\t12)"), 1, "a call over two lines")
	assert_eq(_bare_radii_in("box.corner_radius_top_left = 10"), 1)
	assert_eq(_bare_radii_in("const BADGE_RADIUS: int = 18"), 1)
	assert_eq(_bare_radii_in("box.set_corner_radius_all(UiSkin.round_radius(52.0))"), 0)


func _bare_radii_in(code: String) -> int:
	var path: String = "user://lint_probe.gd"
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	file.store_string(code + "\n")
	file.close()
	var count: int = _bare_radii(path).size()
	DirAccess.remove_absolute(ProjectSettings.globalize_path(path))
	return count


func test_the_radius_scale_is_four_steps_that_grow() -> void:
	var steps: Array[int] = [UiSkin.RADIUS_S, UiSkin.RADIUS_M, UiSkin.RADIUS_L, UiSkin.RADIUS_XL]
	for i: int in range(1, steps.size()):
		assert_gt(steps[i], steps[i - 1], "each step of the radius scale is larger than the one before")
	assert_eq(UiSkin.round_radius(52.0), 26, "a round shape's radius is half its height")
	assert_eq(UiSkin.round_radius(51.0), 26, "rounded up, so an odd height is still fully round")
	for radius: int in steps:
		var ring: StyleBoxFlat = UiSkin.focus_ring(radius)
		for corner: int in [ring.corner_radius_top_left, ring.corner_radius_top_right, ring.corner_radius_bottom_left, ring.corner_radius_bottom_right]:
			assert_eq(corner, radius + UiSkin.FOCUS_EXPAND, "a focus ring follows its control's curve, outward by its own width")
	assert_eq(KalyeahTheme.focus_ring(UiSkin.RADIUS_M).corner_radius_top_left, UiSkin.RADIUS_M + UiSkin.FOCUS_EXPAND, "the theme's ring is the skin's")


## One type scale (docs/ART.md): every font size set in the interface comes from UiMetrics (the reading roles or the display
## scale), never a bare number, so the one-off title sizes the screens had cannot creep back.
func test_every_font_size_comes_from_the_type_scale() -> void:
	var found: Array[String] = []
	var bare: RegEx = RegEx.create_from_string("\"font_size\",\\s*\\d+\\s*\\)")
	for folder: String in SHAPE_SOURCES:
		for file: String in DirAccess.get_files_at(folder):
			if not file.ends_with(".gd"):
				continue
			var path: String = "%s/%s" % [folder, file]
			var text: String = FileAccess.get_file_as_string(path)
			for hit: RegExMatch in bare.search_all(text):
				var where: Dictionary = _line_of(text, hit.get_start())
				if not where["comment"]:
					found.append("%s:%d %s" % [path, where["number"], where["code"]])
	assert_eq(found, [] as Array[String], "%d font sizes must come from UiMetrics:\n%s" % [found.size(), "\n".join(found)])


## Font-size arguments whose parameter is not named `font_size` (a `size` that is a text size): the call and the argument index.
## `.add_label` is MenuDialog's instance method, called on a card variable.
const FONT_ARGS: Dictionary[String, int] = {
	"UiKit.letter": 1, "UiKit.chip": 3, "UiKit.tag": 2, "UiKit.text": 1, "LoadoutCards.label": 1, "RoyaleStyle.lettered": 4,
	".add_label": 1,
}


## Every `func` in `text` with a parameter named `font_size`: {name: index of that parameter}.
func _font_params(text: String) -> Dictionary[String, int]:
	var found: Dictionary[String, int] = {}
	var decl: RegEx = RegEx.create_from_string("(?m)^\\s*(?:static\\s+)?func\\s+(\\w+)\\s*\\(")
	for hit: RegExMatch in decl.search_all(text):
		var params: Array[String] = _call_args(text, hit.get_end() - 1)
		for i: int in params.size():
			if params[i].begins_with("font_size:") or params[i] == "font_size":
				found[hit.get_string(1)] = i
	return found


## Every font-size call in the interface folders, qualified by its class: {"MenuDialog.make_button": 2, "UiSegmented.new": 2, ...},
## plus FONT_ARGS.
func _font_calls() -> Dictionary[String, int]:
	var calls: Dictionary[String, int] = FONT_ARGS.duplicate()
	var named: RegEx = RegEx.create_from_string("(?m)^class_name\\s+(\\w+)")
	for folder: String in SHAPE_SOURCES:
		for file: String in DirAccess.get_files_at(folder):
			if not file.ends_with(".gd"):
				continue
			var text: String = FileAccess.get_file_as_string("%s/%s" % [folder, file])
			var owner: RegExMatch = named.search(text)
			if owner == null:
				continue
			var params: Dictionary[String, int] = _font_params(text)
			for name: String in params:
				calls["%s.%s" % [owner.get_string(1), "new" if name == "_init" else name]] = params[name]
	return calls


## Every font size in one source file that is a bare whole number: a `font_size: int = 28` default, or a call to a
## `calls` entry (or to one of this file's own `font_size` functions) passing one at the font-size argument.
func _bare_font_sizes(path: String, calls: Dictionary[String, int]) -> Array[String]:
	var found: Array[String] = []
	var text: String = FileAccess.get_file_as_string(path)
	var default: RegEx = RegEx.create_from_string("\\bfont_size\\s*:\\s*int\\s*=([^\\n,)#]*)")
	for hit: RegExMatch in default.search_all(text):
		var where: Dictionary = _line_of(text, hit.get_start())
		if not where["comment"] and _bare_whole_number(hit.get_string(1)):
			found.append("%s:%d %s" % [path, where["number"], where["code"]])
	for spot: Dictionary in _font_spots(text, calls, false):
		if _bare_whole_number(spot["arg"] as String):
			found.append("%s:%d %s" % [path, spot["line"], spot["code"]])
	return found


## Engine calls that take a font size, and its index: the text drawing calls, and (`with_overrides`) the theme overrides,
## which the `"font_size", N)` lint above already reads for bare numbers.
const DRAW_FONT_ARGS: Dictionary[String, int] = {
	"draw_string": 5, "draw_string_outline": 5, "draw_multiline_string": 5, "get_string_size": 3, "get_multiline_string_size": 3,
}
const OVERRIDE_FONT_ARGS: Dictionary[String, int] = {"add_theme_font_size_override": 1, "set_font_size": 2}


## Every argument in `text` that is a font size: {line, code, arg} for each call to a `calls` entry, to one of this file's own
## `font_size` functions or to an engine text call (`with_overrides`: the theme overrides too). Comments and declarations skipped.
func _font_spots(text: String, calls: Dictionary[String, int], with_overrides: bool) -> Array[Dictionary]:
	var spots: Dictionary[String, int] = {}
	for key: String in calls:
		var dot: int = key.find(".")
		var pattern: String = "\\." + key.substr(1) if dot == 0 else "\\b" + key.replace(".", "\\.")
		spots[pattern + "\\("] = calls[key]
	var local: Dictionary[String, int] = _font_params(text)
	for name: String in local:
		if name != "_init":
			spots["(?<![\\w.])" + name + "\\("] = local[name]
	var engine: Dictionary[String, int] = DRAW_FONT_ARGS.duplicate()
	if with_overrides:
		engine.merge(OVERRIDE_FONT_ARGS)
	for name: String in engine:
		spots["\\b" + name + "\\("] = engine[name]
	var found: Array[Dictionary] = []
	for pattern: String in spots:
		for hit: RegExMatch in RegEx.create_from_string(pattern).search_all(text):
			var where: Dictionary = _line_of(text, hit.get_start())
			var code: String = where["code"] as String
			if where["comment"] or code.begins_with("func ") or code.begins_with("static func "):
				continue
			var args: Array[String] = _call_args(text, hit.get_end() - 1)
			var index: int = spots[pattern]
			if index < args.size():
				found.append({"line": where["number"], "code": code, "arg": args[index]})
	return found


## One type scale, on buttons too (docs/ART.md): a size handed to MenuDialog.make_button, LobbyWidgets.button or any function with a
## `font_size` parameter (and that parameter's default) comes from UiMetrics, never a bare number. The `"font_size", N)` lint above
## cannot see these: the number sits in the call, not in the override.
func test_every_font_size_handed_to_a_button_or_label_comes_from_the_type_scale() -> void:
	var calls: Dictionary[String, int] = _font_calls()
	assert_eq(calls.get("MenuDialog.make_button", -1), 2, "the scanner found make_button's font_size")
	assert_eq(calls.get("LobbyWidgets.button", -1), 3, "the scanner found LobbyWidgets.button's font_size")
	var found: Array[String] = []
	for folder: String in SHAPE_SOURCES:
		for file: String in DirAccess.get_files_at(folder):
			if file.ends_with(".gd"):
				found.append_array(_bare_font_sizes("%s/%s" % [folder, file], calls))
	assert_eq(found, [] as Array[String], "%d font sizes must come from UiMetrics:\n%s" % [found.size(), "\n".join(found)])


## The lint above catches a bare size and nothing else: a min_size Vector2, a bound count, a float and a named size pass.
func test_the_font_size_lint_tells_a_size_from_a_count_or_a_min_size() -> void:
	assert_eq(_bare_font_sizes_in("MenuDialog.make_button(\"Go\", \"grey\", 30)"), 1)
	assert_eq(_bare_font_sizes_in("MenuDialog.make_button(\"Go\", \"grey\", UiMetrics.ACTION, Vector2(120.0, 88))"), 0, "min_size is not a font size")
	assert_eq(_bare_font_sizes_in("MenuDialog.make_button(\"Go\", \"grey\")"), 0, "the default")
	assert_eq(_bare_font_sizes_in("MenuDialog.make_button(\n\t\"Go\", \"grey\",\n\t32)"), 1, "a call over three lines")
	assert_eq(_bare_font_sizes_in("# MenuDialog.make_button(\"Go\", \"grey\", 30)"), 0, "a comment")
	assert_eq(_bare_font_sizes_in("LobbyWidgets.button(\"%dv%d\" % [2, 2], pick.bind(2), \"grey\")"), 0, "counts in other arguments")
	assert_eq(_bare_font_sizes_in("LobbyWidgets.button(\"Go\", go, \"orange\", 28)"), 1)
	assert_eq(_bare_font_sizes_in("UiSegmented.new(options, 80.0, 30)"), 1, "a constructor's font_size")
	assert_eq(_bare_font_sizes_in("UiSegmented.new(options, 80.0)"), 0, "a float height")
	assert_eq(_bare_font_sizes_in("static func button(text: String, font_size: int = 28) -> Button:\n\tpass"), 1, "a bare default")
	assert_eq(_bare_font_sizes_in("func _init(height: float = 80.0, font_size: int = UiMetrics.ACTION) -> void:\n\tpass"), 0, "a named default")
	assert_eq(_bare_font_sizes_in("func _pill(text: String, font_size: int) -> void:\n\tpass\nfunc _go() -> void:\n\t_pill(\"x\", 34)"), 1, "a file's own font_size function")
	assert_eq(_bare_font_sizes_in("func _pill(text: String, font_size: int) -> void:\n\tpass\nfunc _go() -> void:\n\t_pill(\"x\", PILL_FONT)"), 0)
	assert_eq(_bare_font_sizes_in("UiKit.chip(\"x\", fill, Color.WHITE, 22)"), 1, "a size-named text size")
	assert_eq(_bare_font_sizes_in("card.add_label(\"x\", 30, KalyeahTheme.CREAM)"), 1)
	assert_eq(_bare_font_sizes_in("var count: int = 30\nvar gap: Vector2 = Vector2(0.0, 88.0)"), 0, "unrelated numbers")


func _bare_font_sizes_in(code: String) -> int:
	var path: String = "user://lint_font_probe.gd"
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	file.store_string(code + "\n")
	file.close()
	var count: int = _bare_font_sizes(path, _font_calls()).size()
	DirAccess.remove_absolute(ProjectSettings.globalize_path(path))
	return count


## Every constant among `paths` that sets a font size from a bare whole number: an int constant named `*FONT*`, or any constant
## used as a font size (at a spot `_font_spots` finds, here or as `Class.NAME` from another file). `GRABBER_SIZE` used for a
## Vector2 is not a font size and passes; `const NAME_SIZE: int = 26` handed to a label does not.
func _bare_font_constants(paths: Array[String], calls: Dictionary[String, int]) -> Array[String]:
	var texts: Dictionary[String, String] = {}
	var local_uses: Dictionary[String, Dictionary] = {}
	var qualified_uses: Dictionary[String, bool] = {}
	var qualified: RegEx = RegEx.create_from_string("(?<![\\w.])([A-Z]\\w*)\\.([A-Z][A-Z0-9_]+)\\b(?!\\s*\\()")
	var bare_name: RegEx = RegEx.create_from_string("(?<![\\w.])([A-Z][A-Z0-9_]+)\\b(?!\\s*[.(])")
	for path: String in paths:
		var text: String = FileAccess.get_file_as_string(path)
		texts[path] = text
		var mine: Dictionary = {}
		for spot: Dictionary in _font_spots(text, calls, true):
			var arg: String = spot["arg"] as String
			for hit: RegExMatch in qualified.search_all(arg):
				qualified_uses["%s.%s" % [hit.get_string(1), hit.get_string(2)]] = true
			for hit: RegExMatch in bare_name.search_all(arg):
				mine[hit.get_string(1)] = true
		local_uses[path] = mine
	var found: Array[String] = []
	var named: RegEx = RegEx.create_from_string("(?m)^class_name\\s+(\\w+)")
	var constant: RegEx = RegEx.create_from_string("(?m)^\\s*const\\s+([A-Z][A-Z0-9_]*)\\s*(?::\\s*(\\w+)\\s*)?:?=([^\\n#]*)")
	for path: String in paths:
		var text: String = texts[path]
		var owner: RegExMatch = named.search(text)
		for hit: RegExMatch in constant.search_all(text):
			var name: String = hit.get_string(1)
			var type: String = hit.get_string(2)
			if type != "" and type != "int":
				continue
			var used: bool = local_uses[path].has(name) or (owner != null and qualified_uses.has("%s.%s" % [owner.get_string(1), name]))
			if (used or (type == "int" and name.contains("FONT"))) and _bare_whole_number(hit.get_string(3)):
				var where: Dictionary = _line_of(text, hit.get_start(1))
				found.append("%s:%d %s" % [path, where["number"], where["code"]])
	return found


## One type scale, in the constants too: a constant used as a font size (or an int named *FONT*) is defined from UiMetrics, so a
## bare 30 cannot move out of a call into a local constant and pass. ui_metrics.gd holds the scale itself.
func test_every_font_size_constant_comes_from_the_type_scale() -> void:
	var paths: Array[String] = []
	for folder: String in SHAPE_SOURCES:
		for file: String in DirAccess.get_files_at(folder):
			if file.ends_with(".gd") and file != "ui_metrics.gd":
				paths.append("%s/%s" % [folder, file])
	var found: Array[String] = _bare_font_constants(paths, _font_calls())
	assert_eq(found, [] as Array[String], "%d font-size constants must come from UiMetrics:\n%s" % [found.size(), "\n".join(found)])


## The constant lint catches a bare font constant and nothing else: a Vector2 size, a float and a count pass.
func test_the_font_constant_lint_tells_a_font_size_from_another_size() -> void:
	assert_eq(_bare_font_constants_in(["const NAME_SIZE: int = UiMetrics.DETAIL\nfunc _go() -> void:\n\tMenuDialog.make_button(\"x\", \"grey\", NAME_SIZE)"]), 0)
	assert_eq(_bare_font_constants_in(["const NAME_SIZE: int = 26\nfunc _go() -> void:\n\tlabel.add_theme_font_size_override(\"font_size\", NAME_SIZE)"]), 1, "used by an override")
	assert_eq(_bare_font_constants_in(["const PAD: int = 30\nfunc _draw() -> void:\n\tdraw_string(font, at, t, HORIZONTAL_ALIGNMENT_LEFT, -1.0, PAD, Color.WHITE)"]), 1, "used by a draw, whatever its name")
	assert_eq(_bare_font_constants_in(["const STATUS_FONT: int = 22"]), 1, "an int named FONT")
	assert_eq(_bare_font_constants_in(["const BIG_FONT: int = UiMetrics.TITLE - 6"]), 1, "a bare offset off the scale")
	assert_eq(_bare_font_constants_in(["const BIG_FONT: int = UiMetrics.TITLE\nfunc _go() -> void:\n\t_center_text(BIG_FONT)"]), 0)
	assert_eq(_bare_font_constants_in(["const GRABBER_SIZE: int = 44\nfunc _go() -> void:\n\tMenuDialog.make_button(\"\", \"grey\", UiMetrics.ACTION, Vector2(GRABBER_SIZE, GRABBER_SIZE))"]), 0, "a min_size")
	assert_eq(_bare_font_constants_in(["const ICON_SIZE: float = 52.0\nconst CARD_SIZE: Vector2 = Vector2(132.0, 178.0)"]), 0, "not ints")
	assert_eq(_bare_font_constants_in(["const ALIGN: int = 1\nfunc _go() -> void:\n\tfont.get_string_size(t, ALIGN, -1.0, UiMetrics.BODY)"]), 0, "another argument of a text call")
	assert_eq(_bare_font_constants_in(["class_name LintProbeA\nconst TAG_SIZE: int = 22", "func _go() -> void:\n\tUiKit.chip(\"x\", fill, Color.WHITE, LintProbeA.TAG_SIZE)"]), 1, "used from another file")
	assert_eq(_bare_font_constants_in(["class_name LintProbeA\nconst TAG_SIZE: int = 22", "func _go() -> void:\n\tUiKit.chip(\"x\", fill, Color.WHITE, UiMetrics.CAPTION)"]), 0, "named SIZE but no font size")


func _bare_font_constants_in(codes: Array[String]) -> int:
	var paths: Array[String] = []
	for i: int in codes.size():
		var path: String = "user://lint_const_probe_%d.gd" % i
		var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
		file.store_string(codes[i] + "\n")
		file.close()
		paths.append(path)
	var count: int = _bare_font_constants(paths, _font_calls()).size()
	for path: String in paths:
		DirAccess.remove_absolute(ProjectSettings.globalize_path(path))
	return count


## The font-size spots `_font_spots` cannot see in one file: plain `name(` calls to this file's own class's font-size functions
## (`label("Teammate", 22)` inside LoadoutCards, where only `LoadoutCards.label(` was matched). {line, code, arg} each.
func _self_font_spots(text: String, calls: Dictionary[String, int]) -> Array[Dictionary]:
	var found: Array[Dictionary] = []
	var owner: RegExMatch = RegEx.create_from_string("(?m)^class_name\\s+(\\w+)").search(text)
	if owner == null:
		return found
	var prefix: String = owner.get_string(1) + "."
	for key: String in calls:
		if not key.begins_with(prefix) or key == prefix + "new":
			continue
		var name: String = key.substr(prefix.length())
		for hit: RegExMatch in RegEx.create_from_string("(?<![\\w.])" + name + "\\(").search_all(text):
			var where: Dictionary = _line_of(text, hit.get_start())
			var code: String = where["code"] as String
			if where["comment"] or code.begins_with("func ") or code.begins_with("static func "):
				continue
			var args: Array[String] = _call_args(text, hit.get_end() - 1)
			if calls[key] < args.size():
				found.append({"line": where["number"], "code": code, "arg": args[calls[key]]})
	return found


## In one file: a bare size handed to the file's own font-size function by its plain name, and an int variable that holds a bare
## whole number and is either used as a font size (at any font-size spot in the file) or named for a font.
func _bare_font_vars_and_self_calls(path: String, calls: Dictionary[String, int]) -> Array[String]:
	var found: Array[String] = []
	var text: String = FileAccess.get_file_as_string(path)
	var own: Array[Dictionary] = _self_font_spots(text, calls)
	for spot: Dictionary in own:
		if _bare_whole_number(spot["arg"] as String):
			found.append("%s:%d %s" % [path, spot["line"], spot["code"]])
	var used: Dictionary[String, bool] = {}
	var word: RegEx = RegEx.create_from_string("(?<![\\w.])([a-z_]\\w*)\\b(?!\\s*[.(])")
	var spots: Array[Dictionary] = _font_spots(text, calls, true)
	spots.append_array(own)
	for spot: Dictionary in spots:
		for hit: RegExMatch in word.search_all(spot["arg"] as String):
			used[hit.get_string(1)] = true
	var variable: RegEx = RegEx.create_from_string("(?m)^\\s*var\\s+(\\w+)\\s*(?::\\s*(\\w+)\\s*)?:?=([^\\n#]*)")
	for hit: RegExMatch in variable.search_all(text):
		var name: String = hit.get_string(1)
		var type: String = hit.get_string(2)
		if type != "" and type != "int":
			continue
		if (used.has(name) or (type == "int" and name.containsn("font"))) and _bare_whole_number(hit.get_string(3)):
			var where: Dictionary = _line_of(text, hit.get_start(1))
			if not where["comment"]:
				found.append("%s:%d %s" % [path, where["number"], where["code"]])
	return found


## One type scale, in a class's own calls and in its variables: `label("Teammate", 22)` inside LoadoutCards and a
## `var caption_size: int = 26` later drawn come from UiMetrics too, not just qualified calls and constants.
func test_every_font_size_in_own_calls_and_variables_comes_from_the_type_scale() -> void:
	var calls: Dictionary[String, int] = _font_calls()
	var found: Array[String] = []
	for folder: String in SHAPE_SOURCES:
		for file: String in DirAccess.get_files_at(folder):
			if file.ends_with(".gd") and file != "ui_metrics.gd":
				found.append_array(_bare_font_vars_and_self_calls("%s/%s" % [folder, file], calls))
	assert_eq(found, [] as Array[String], "%d font sizes must come from UiMetrics:\n%s" % [found.size(), "\n".join(found)])


## The lint above catches a plain own call and a font variable, and leaves counts, floats and other sizes alone.
func test_the_own_call_and_variable_lint_tells_a_font_size_from_a_count() -> void:
	var cards: String = "class_name LoadoutCards\nstatic func label(text: String, size: int, color: Color = Color.WHITE) -> Label:\n\treturn null\nfunc _go() -> void:\n\t"
	assert_eq(_own_and_vars_in(cards + "var taken: Label = label(\"Teammate\", 22)"), 1, "the class's own size-named function by its plain name")
	assert_eq(_own_and_vars_in(cards + "var badge: Label = label(\"1\", 26, KalyeahTheme.INK)"), 1)
	assert_eq(_own_and_vars_in(cards + "var taken: Label = label(\"Teammate\", UiMetrics.CAPTION)"), 0)
	assert_eq(_own_and_vars_in(cards + "var other: Label = row.label(\"x\", 22)"), 0, "someone else's label method")
	assert_eq(_own_and_vars_in("func _go() -> void:\n\tlabel(\"x\", 22)"), 0, "no class: not LoadoutCards.label")
	var draw: String = "func _draw() -> void:\n\tdraw_string(font, at, t, HORIZONTAL_ALIGNMENT_LEFT, -1.0, caption_size, Color.WHITE)\n"
	assert_eq(_own_and_vars_in("func _draw() -> void:\n\tvar caption_size: int = 26\n" + draw.substr(draw.find("\n") + 1)), 1, "a drawn size variable")
	assert_eq(_own_and_vars_in("func _draw() -> void:\n\tvar caption_size: int = UiMetrics.BODY\n" + draw.substr(draw.find("\n") + 1)), 0)
	assert_eq(_own_and_vars_in("func _go() -> void:\n\tvar count := 26\n\tlabel.add_theme_font_size_override(\"font_size\", count)"), 1, "untyped, by use")
	assert_eq(_own_and_vars_in("var font_px: int = 30"), 1, "named for a font")
	assert_eq(_own_and_vars_in("func _go() -> void:\n\tvar grid_size: int = 3\n\tfor i: int in grid_size:\n\t\tpass"), 0, "a size that is a count")
	assert_eq(_own_and_vars_in("var size_hint: Vector2 = Vector2(10, 10)\nvar ratio: float = 26.0"), 0, "not ints")
	assert_eq(_own_and_vars_in("func _go() -> void:\n\tvar slot: int = 2\n\tfont.get_string_size(t, HORIZONTAL_ALIGNMENT_LEFT, slot, UiMetrics.BODY)"), 0, "another argument of a text call")


func _own_and_vars_in(code: String) -> int:
	var path: String = "user://lint_vars_probe.gd"
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	file.store_string(code + "\n")
	file.close()
	var count: int = _bare_font_vars_and_self_calls(path, _font_calls()).size()
	DirAccess.remove_absolute(ProjectSettings.globalize_path(path))
	return count
