class_name TitleScreen
extends Control
## The main screen, built as a game lobby: a living backdrop, a top bar with the game's logo and who
## you are, the mode carousel as the hero (the chosen card framed in light, floating a little), the
## mode's name, one phrase and the main action under it, and a tab bar at the bottom (Play, Book,
## History, Settings). The tabs swap the middle of the screen; the pages take the whole height (the logo and
## profile chip show on Play only, each page has its own title and Settings holds the profile).
## Android Back closes a tab back to Play, and on Play asks to quit.

const SERVER_SCENE: String = "res://server/main.tscn"
const LOBBY_SCENE: String = "res://scenes/ui/lobby.tscn"
const PRACTICE_SCENE: String = "res://scenes/match/practice.tscn"
const LOGO: Texture2D = preload("res://assets/ui/kalyeah_logo.png")

## The game modes, first is the default (owner: hotspot first, not online). `accent` tints the
## frame of the card and the glow behind it.
const MODES: Array[Dictionary] = [
	{"id": "hotspot", "title": "Hotspot", "color": "blue", "accent": "4aa0ff",
		"text": "Play on one local network."},
	{"id": "online", "title": "Online", "color": "orange", "accent": "ffa63a",
		"text": "Invite friends with a room code."},
	{"id": "practice", "title": "Practice", "color": "green", "accent": "45dd78",
		"text": "Face 3 bots, or just train."},
]
## The bottom tabs: Play is the home page, the others are pages.
const TABS: Array[Dictionary] = [
	{"id": &"home", "label": "Play", "icon": &"battle"},
	{"id": &"codex", "label": "Book", "icon": &"book"},
	{"id": &"history", "label": "History", "icon": &"history"},
	{"id": &"settings", "label": "Settings", "icon": &"settings"},
]
## The hero cards (2.5:4, like the owner's card pictures) and how high the stage is.
const HERO_CARD: Vector2 = Vector2(400.0, 640.0)
## The hero grows with the stage (phones taller than 16:9 have the room): 2.5:4, between these heights, with
## this much air round it for the glow and the bob.
const HERO_RATIO: float = 0.625
const HERO_MIN_HEIGHT: float = 480.0
const HERO_MAX_HEIGHT: float = 780.0
const HERO_AIR: float = 56.0
const AMBIENT_BOB: float = 7.0
## The mode chips need a stage at least this tall to leave the hero a good size; on a shorter screen they step aside.
const CHIPS_HEIGHT: float = 42.0
const CHIPS_MIN_STAGE: float = 640.0
## Which way a page slides in: the pages left to right as their tabs are.
const PAGE_RANK: Dictionary[StringName, int] = {&"": 0, &"codex": 1, &"history": 2, &"settings": 3, &"gallery": 3, &"howto": 3}
const RULES_PATH: String = "res://data/rules/game_rules.tres"
const LOGO_HEIGHT: float = 92.0
const SIDE_MARGIN: float = 28.0
const TOP_MARGIN: float = 20.0
const BOTTOM_MARGIN: float = 18.0
const CTA_HEIGHT: float = 104.0
const CREAM: Color = KalyeahTheme.CREAM

## The screen asks for the game to close (replaced in tests).
var quit_action: Callable = _quit_game
## The screen leaves for the lobby (replaced in tests).
var open_lobby_action: Callable = _change_to_lobby

var backdrop: UiBackdrop
var carousel: MenuCarousel
var profile_chip: UiProfileChip
var tab_bar: UiTabBar
var mode_name_label: Label
var mode_text_label: Label
var primary_button: Button
var secondary_button: Button
var dialog: MenuDialog
## The tab buttons by page id (home, codex, history, settings).
var corner_buttons: Dictionary[StringName, Button] = {}
var pages: Dictionary[StringName, Control] = {}
var current_page: StringName = &""
var codex_page: CodexPage
var characters_page: CharactersPage:
	get: return codex_page.characters_page
var weapons_page: WeaponsPage:
	get: return codex_page.weapons_page
var history_page: HistoryPage
var settings_page: SettingsPage
var gallery_page: UiGallery
var howto_page: HowToPlayPage

var _loading: bool = false
var _loading_dots: Label
var _margin: MarginContainer
var _home: VBoxContainer
var _top_bar: Control
var mode_chips: HBoxContainer
var last_match: Button
var _page_holder: Control
var _dots: Array[Panel] = []
var _page_tween: Tween
var _pulse: Tween


## True for the dedicated server export or `godot --headless -- --server`.
static func is_server_run() -> bool:
	return OS.has_feature("dedicated_server") or OS.get_cmdline_user_args().has("--server")


func _ready() -> void:
	if is_server_run():
		get_tree().change_scene_to_file.call_deferred(SERVER_SCENE)
		return
	Session.ensure_profile()
	Net.stop_host()
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	backdrop = UiBackdrop.new()
	add_child(backdrop)
	_build_layout()
	_apply_safe_area()
	_refresh_mode()
	refresh_last_match()
	carousel.selected.connect(_on_mode_selected)
	primary_button.pressed.connect(_on_primary_pressed)
	secondary_button.pressed.connect(_on_secondary_pressed)
	get_viewport().size_changed.connect(_apply_safe_area)
	Settings.accessibility_changed.connect(_on_accessibility_changed)
	set_process(false)
	_pop_in.call_deferred()


# ---- layout ---------------------------------------------------------------------------

func _build_layout() -> void:
	_margin = MarginContainer.new()
	_margin.name = "Safe"
	_margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_margin)
	var shell: VBoxContainer = VBoxContainer.new()
	shell.name = "Shell"
	shell.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	shell.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_margin.add_child(shell)
	_top_bar = _build_top_bar()
	shell.add_child(_top_bar)
	var middle: Control = Control.new()
	middle.name = "Middle"
	middle.size_flags_vertical = Control.SIZE_EXPAND_FILL
	middle.mouse_filter = Control.MOUSE_FILTER_IGNORE
	shell.add_child(middle)
	_home = _build_home()
	_home.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	middle.add_child(_home)
	_page_holder = Control.new()
	_page_holder.name = "Pages"
	_page_holder.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_page_holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
	middle.add_child(_page_holder)
	_build_pages()
	tab_bar = UiTabBar.new(TABS)
	tab_bar.tab_pressed.connect(_on_tab_pressed)
	shell.add_child(tab_bar)
	for id: StringName in tab_bar.tabs:
		corner_buttons[id] = tab_bar.tabs[id]
	tab_bar.set_active(&"home")


## The top bar: the KALYEAH! logo at the left, who you are at the right.
func _build_top_bar() -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "TopBar"
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var logo: TextureRect = TextureRect.new()
	logo.name = "Logo"
	logo.texture = LOGO
	logo.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	logo.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	logo.custom_minimum_size = Vector2(LOGO_HEIGHT * float(LOGO.get_width()) / float(LOGO.get_height()), LOGO_HEIGHT)
	logo.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	logo.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(logo)
	var spacer: Control = Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	spacer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(spacer)
	profile_chip = UiProfileChip.new()
	profile_chip.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	profile_chip.pressed.connect(open_name_editor)
	row.add_child(profile_chip)
	return row


## The Play page: the hero carousel, the mode's name and phrase, the page dots and the actions.
func _build_home() -> VBoxContainer:
	var home: VBoxContainer = VBoxContainer.new()
	home.name = "Home"
	home.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	home.mouse_filter = Control.MOUSE_FILTER_IGNORE
	last_match = _build_last_match()
	home.add_child(last_match)
	var stage: Control = Control.new()
	stage.name = "Stage"
	stage.size_flags_vertical = Control.SIZE_EXPAND_FILL
	stage.custom_minimum_size = Vector2(0.0, HERO_CARD.y * 0.5)
	stage.mouse_filter = Control.MOUSE_FILTER_IGNORE
	home.add_child(stage)
	carousel = MenuCarousel.new()
	carousel.accessibility_name = "Game mode"
	carousel.name = "ModeCarousel"
	carousel.card_size = HERO_CARD
	carousel.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	stage.add_child(carousel)
	if not Session.menu_mode_chosen and not Net.can_host():
		# a browser cannot host or join a hotspot room: it opens on Practice instead of on a dead end
		Session.menu_mode = MODES.size() - 1
	carousel.setup("", MODES, Session.menu_mode)
	carousel.bob = AMBIENT_BOB
	stage.resized.connect(_fit_hero)
	home.add_child(_build_titles())
	home.add_child(_build_actions())
	return home


## The mode's name (big, outlined), its one phrase and a dot for each mode.
func _build_titles() -> Control:
	var titles: VBoxContainer = VBoxContainer.new()
	titles.name = "Titles"
	titles.alignment = BoxContainer.ALIGNMENT_CENTER
	titles.add_theme_constant_override("separation", 2)
	titles.mouse_filter = Control.MOUSE_FILTER_IGNORE
	mode_name_label = Label.new()
	mode_name_label.name = "ModeName"
	mode_name_label.theme_type_variation = &"DisplayLabel"
	mode_name_label.add_theme_font_size_override("font_size", 56)
	mode_name_label.add_theme_color_override("font_color", CREAM)
	mode_name_label.add_theme_color_override("font_outline_color", Color("060d22"))
	mode_name_label.add_theme_constant_override("outline_size", 10)
	mode_name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	titles.add_child(mode_name_label)
	mode_text_label = Label.new()
	mode_text_label.name = "ModeText"
	mode_text_label.add_theme_font_size_override("font_size", UiMetrics.DETAIL)
	mode_text_label.add_theme_color_override("font_color", Color("c9d6f2"))
	mode_text_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	mode_text_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	mode_text_label.add_theme_constant_override("line_spacing", KalyeahTheme.READING_LINE_SPACING)
	titles.add_child(mode_text_label)
	mode_chips = HBoxContainer.new()
	mode_chips.name = "ModeChips"
	mode_chips.alignment = BoxContainer.ALIGNMENT_CENTER
	mode_chips.add_theme_constant_override("separation", 10)
	mode_chips.mouse_filter = Control.MOUSE_FILTER_IGNORE
	titles.add_child(mode_chips)
	var dots: HBoxContainer = HBoxContainer.new()
	dots.name = "Dots"
	dots.alignment = BoxContainer.ALIGNMENT_CENTER
	dots.add_theme_constant_override("separation", 10)
	dots.custom_minimum_size = Vector2(0.0, 26.0)
	dots.mouse_filter = Control.MOUSE_FILTER_IGNORE
	for i: int in MODES.size():
		var dot: Panel = Panel.new()
		dot.custom_minimum_size = Vector2(12.0, 12.0)
		dot.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		dot.mouse_filter = Control.MOUSE_FILTER_IGNORE
		dots.add_child(dot)
		_dots.append(dot)
	titles.add_child(dots)
	return titles


## The main action in one row: Create room and Join room, or Play vs bots and Training.
func _build_actions() -> Control:
	var actions: HBoxContainer = HBoxContainer.new()
	actions.name = "Actions"
	actions.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	actions.mouse_filter = Control.MOUSE_FILTER_IGNORE
	primary_button = _action_button(actions, "PrimaryButton", "", 2.0)
	secondary_button = _action_button(actions, "SecondaryButton", "Join room", 1.0)
	return actions


func _action_button(parent: Control, button_name: String, text: String, weight: float) -> Button:
	var button: Button = MenuDialog.make_button(text, "gold", 38, Vector2(0.0, CTA_HEIGHT))
	button.name = button_name
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	button.size_flags_stretch_ratio = weight
	parent.add_child(button)
	return button


func _build_pages() -> void:
	codex_page = CodexPage.new()
	history_page = HistoryPage.new()
	settings_page = SettingsPage.new()
	gallery_page = UiGallery.new()
	howto_page = HowToPlayPage.new()
	pages = {&"codex": codex_page, &"history": history_page, &"settings": settings_page, &"gallery": gallery_page, &"howto": howto_page}
	for id: StringName in pages:
		var page: Control = pages[id]
		page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		page.visible = false
		_page_holder.add_child(page)
	codex_page.weapons_page.weapon_opened.connect(open_weapon_detail)
	codex_page.characters_page.character_opened.connect(open_character_detail)
	settings_page.profile_pressed.connect(open_name_editor)
	settings_page.quit_pressed.connect(open_quit_confirm)
	settings_page.gallery_pressed.connect(open_page.bind(&"gallery"))
	settings_page.howto_pressed.connect(open_page.bind(&"howto"))
	howto_page.walkthrough_requested.connect(start_walkthrough)
	history_page.play_pressed.connect(close_page)
	howto_page.back_pressed.connect(open_page.bind(&"settings"))


func _on_tab_pressed(id: StringName) -> void:
	if id == &"home":
		close_page()
	else:
		open_page(id)


## Opens one of the pages (codex = the book, history, settings) in the middle of the screen; it slides in from the
## side its tab is on.
func open_page(id: StringName) -> void:
	if not pages.has(id):
		return
	var way: int = signi(int(PAGE_RANK.get(id, 0)) - int(PAGE_RANK.get(current_page, 0)))
	current_page = id
	for other: StringName in pages:
		pages[other].visible = other == id
	if id == &"history":
		history_page.refresh()
	elif id == &"codex":
		codex_page.show_tab(codex_page.current)
	_home.visible = false
	_top_bar.visible = false
	# the gallery and How to play are parts of Settings: that tab stays lit and opens it again
	tab_bar.set_active(&"settings" if id == &"gallery" or id == &"howto" else id)
	_sync_ambient()
	_fade_in(pages[id], way)

## Back to the Play page.
func close_page() -> void:
	var way: int = signi(0 - int(PAGE_RANK.get(current_page, 0)))
	current_page = &""
	for id: StringName in pages:
		pages[id].visible = false
	_home.visible = true
	_top_bar.visible = true
	refresh_last_match()
	tab_bar.set_active(&"home")
	_sync_ambient()
	_fade_in(_home, way)

## A short slide and fade when the middle of the screen changes: `way` is -1 from the left, 1 from the right.
func _fade_in(content: Control, way: int = 0) -> void:
	if is_instance_valid(_page_tween):
		_page_tween.kill()
	content.modulate.a = 1.0
	content.position = Vector2.ZERO
	if Settings.reduced_motion or not is_inside_tree():
		return
	content.modulate.a = 0.0
	content.position = Vector2(float(way) * 56.0, 0.0 if way != 0 else 18.0)
	_page_tween = create_tween().set_parallel(true)
	_page_tween.tween_property(content, "modulate:a", 1.0, 0.2)
	_page_tween.tween_property(content, "position", Vector2.ZERO, 0.26).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)

## The facts of a mode as small chips, from the rules (players, best-of choices), never typed in here.
func mode_facts(id: String) -> Array[String]:
	var rules: GameRules = load(RULES_PATH) as GameRules
	if id == "practice":
		return ["Solo", "%d v %d against bots" % [PracticeMatch.BOT_ALLY_IDS.size() + 1, PracticeMatch.BOT_ENEMY_IDS.size()]] as Array[String]
	var choices: PackedStringArray = []
	for choice: int in rules.first_to_choices:
		choices.append(str(choice))
	var best: String = "First to %s" % (choices[0] if choices.size() == 1 else "%s or %s" % [", ".join(choices.slice(0, choices.size() - 1)), choices[choices.size() - 1]])
	var players: String = "2 to %d players" % (RoomService.MAX_TEAM_SIZE * 2)
	return [players, best if id == "hotspot" else "Join with a code"] as Array[String]


func _fill_chips(id: String) -> void:
	for child: Node in mode_chips.get_children():
		mode_chips.remove_child(child)
		child.queue_free()
	for fact: String in mode_facts(id):
		var chip: PanelContainer = UiKit.chip(fact, UiKit.CHIP_SLATE, Color.WHITE, 22)
		chip.mouse_filter = Control.MOUSE_FILTER_IGNORE
		mode_chips.add_child(chip)


## The strip at the top of Play: how the last battle went. Tap it for the whole history.
func _build_last_match() -> Button:
	var strip: Button = Button.new()
	strip.name = "LastMatch"
	strip.custom_minimum_size = Vector2(0.0, UiMetrics.TOUCH)
	strip.focus_mode = Control.FOCUS_ALL
	for state: String in ["normal", "hover"]:
		strip.add_theme_stylebox_override(state, UiSkin.plate("night", 0.0, 34))
	strip.add_theme_stylebox_override("pressed", UiSkin.plate("raised", 0.0, 34))
	strip.add_theme_stylebox_override("focus", UiSkin.focus_ring(34))
	MenuDialog.feel_press(strip)
	strip.pressed.connect(open_page.bind(&"history"))
	strip.visible = false
	return strip


## Fills the strip from the newest history entry (hidden when nothing has been played).
func refresh_last_match() -> void:
	for child: Node in last_match.get_children():
		last_match.remove_child(child)
		child.queue_free()
	var found: Array[Dictionary] = MatchHistory.entries()
	last_match.visible = not found.is_empty()
	if found.is_empty():
		return
	var entry: Dictionary = found[0]
	var won: bool = bool(entry.get("won", false))
	last_match.accessibility_name = "Last match: %s, %s, %d to %d" % ["Victory" if won else "Defeat", String(entry.get("mode", "")).capitalize(), int(entry.get("mine", 0)), int(entry.get("theirs", 0))]
	last_match.accessibility_description = "Opens your history"
	var row: HBoxContainer = HBoxContainer.new()
	row.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	row.offset_left = 18.0
	row.offset_right = -22.0
	row.add_theme_constant_override("separation", 14)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var badge: PanelContainer = UiKit.chip("Victory" if won else "Defeat", UiKit.CHIP_GREEN if won else UiKit.CHIP_RED, Color.WHITE, 22)
	badge.name = "Result"
	badge.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	badge.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(badge)
	var words: Label = UiKit.text("%s · %d - %d" % [String(entry.get("mode", "")).capitalize(), int(entry.get("mine", 0)), int(entry.get("theirs", 0))], UiMetrics.DETAIL, KalyeahTheme.TEXT, false)
	words.name = "Words"
	words.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	words.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	words.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(words)
	var when: Label = UiKit.text(MatchHistory.ago(int(entry.get("at", 0)), int(Time.get_unix_time_from_system())), UiMetrics.CAPTION, Color("9fb0d6"), false)
	when.name = "When"
	when.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	when.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(when)
	last_match.add_child(row)


## Sizes the hero cards to the stage (taller phones get a bigger card), then rebuilds them.
func _fit_hero() -> void:
	var stage: Control = carousel.get_parent() as Control
	if stage.size.y < 100.0:
		return
	# the stage as it would be without the chips decides whether they show (so showing them never flips it back)
	var bare: float = stage.size.y + (CHIPS_HEIGHT if mode_chips.visible else 0.0)
	mode_chips.visible = bare - CHIPS_HEIGHT >= CHIPS_MIN_STAGE
	var height: float = clampf(bare - (CHIPS_HEIGHT if mode_chips.visible else 0.0) - HERO_AIR, HERO_MIN_HEIGHT, HERO_MAX_HEIGHT)
	var width: float = minf(height * HERO_RATIO, stage.size.x - 120.0)
	var fitted: Vector2 = Vector2(roundf(width), roundf(width / HERO_RATIO))
	if fitted.distance_to(carousel.card_size) < 3.0:
		return
	carousel.card_size = fitted
	carousel.setup("", MODES, carousel.index)


## The chosen mode's name and phrase, the action buttons, the glow and the dots.
func _refresh_mode() -> void:
	var mode: Dictionary = MODES[Session.menu_mode]
	var rooms: bool = String(mode["id"]) != "practice"
	mode_name_label.text = String(mode["title"])
	mode_text_label.text = String(mode["text"])
	if String(mode["id"]) == "hotspot" and not Net.can_host():
		mode_text_label.text = "Needs the Android app."
	_fill_chips(String(mode["id"]))
	primary_button.text = "Create room" if rooms else "Play vs bots"
	secondary_button.text = "Join room" if rooms else "Training"
	secondary_button.visible = true
	KalyeahTheme.style_button(primary_button, "orange")
	KalyeahTheme.style_button(secondary_button, "grey")
	backdrop.set_accent(Color(String(mode["accent"])))
	for i: int in _dots.size():
		var dot: StyleBoxFlat = StyleBoxFlat.new()
		dot.set_corner_radius_all(8)
		dot.bg_color = Color(String(mode["accent"])) if i == Session.menu_mode else Color(1.0, 1.0, 1.0, 0.28)
		dot.anti_aliasing = true
		_dots[i].add_theme_stylebox_override("panel", dot)
		_dots[i].custom_minimum_size = Vector2(26.0 if i == Session.menu_mode else 12.0, 12.0)


func _on_mode_selected(index: int) -> void:
	Session.menu_mode = index
	Session.menu_mode_chosen = true
	_refresh_mode()


func _on_primary_pressed() -> void:
	match String(MODES[Session.menu_mode]["id"]):
		"practice":
			_on_practice_pressed(&"match")
		"online":
			_open_lobby(LobbyScreen.Intent.CREATE, false)
		_:
			open_hotspot_create()


func _on_secondary_pressed() -> void:
	if String(MODES[Session.menu_mode]["id"]) == "practice":
		_on_practice_pressed(&"training")
	elif String(MODES[Session.menu_mode]["id"]) == "hotspot":
		open_hotspot_join()
	else:
		_open_lobby(LobbyScreen.Intent.JOIN, false)


## Hotspot Create: a dialog checks the hotspot is on, then the room opens.
func open_hotspot_create() -> MenuDialog:
	var card: MenuDialog = HotspotDialogs.create_check(
		func() -> void: _open_lobby(LobbyScreen.Intent.CREATE, true),
		func() -> void:
			Session.lobby_preview = true
			_open_lobby(LobbyScreen.Intent.CREATE, true))
	_show_dialog(card)
	return card


## Hotspot Join: a dialog with a box for each character of the room code.
func open_hotspot_join() -> MenuDialog:
	var card: MenuDialog = HotspotDialogs.join(func(code: String) -> void:
		Session.lobby_code = code
		_open_lobby(LobbyScreen.Intent.JOIN, true))
	_show_dialog(card)
	return card


## Keeps everything clear of notches, rounded corners and the home bar.
func _apply_safe_area() -> void:
	if _margin == null:
		return
	var inset: Vector4 = safe_insets()
	_margin.add_theme_constant_override("margin_left", int(SIDE_MARGIN + inset.x))
	_margin.add_theme_constant_override("margin_top", int(TOP_MARGIN + inset.y))
	_margin.add_theme_constant_override("margin_right", int(SIDE_MARGIN + inset.z))
	_margin.add_theme_constant_override("margin_bottom", int(BOTTOM_MARGIN + inset.w))


## The unsafe screen edges (left, top, right, bottom) in UI pixels: the notch, the status bar,
## rounded corners and the home bar. Zero where the window has none.
func safe_insets() -> Vector4:
	var window: Vector2i = DisplayServer.window_get_size()
	if window.x <= 0 or window.y <= 0:
		return Vector4.ZERO
	var safe: Rect2i = DisplayServer.get_display_safe_area()
	var origin: Vector2i = DisplayServer.window_get_position()
	var to_ui: Vector2 = get_viewport().get_visible_rect().size / Vector2(window)
	var left: float = maxf(0.0, float(safe.position.x - origin.x))
	var top: float = maxf(0.0, float(safe.position.y - origin.y))
	var right: float = maxf(0.0, float(origin.x + window.x - (safe.position.x + safe.size.x)))
	var bottom: float = maxf(0.0, float(origin.y + window.y - (safe.position.y + safe.size.y)))
	return Vector4(minf(left * to_ui.x, 120.0), minf(top * to_ui.y, 120.0), minf(right * to_ui.x, 120.0), minf(bottom * to_ui.y, 120.0))


# ---- what the screen shows ---------------------------------------------------------------

## The hero cards fade in, then the buttons pop up in turn, and the main action breathes.
func _pop_in() -> void:
	if Settings.reduced_motion:
		return
	var parts: Array[Control] = [carousel, primary_button, secondary_button]
	for i: int in parts.size():
		var part: Control = parts[i]
		part.pivot_offset = part.size / 2.0
		part.scale = Vector2.ONE * 0.9
		part.modulate.a = 0.0
		var pop: Tween = create_tween().set_parallel(true)
		part.set_meta(&"entrance_tween", pop)
		pop.tween_property(part, "modulate:a", 1.0, 0.3).set_delay(0.12 * i)
		pop.tween_property(part, "scale", Vector2.ONE, 0.4).set_delay(0.12 * i).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	_start_pulse.call_deferred()


## The main action swells and settles slowly, drawing the eye to it.
func _start_pulse() -> void:
	if not is_inside_tree() or Settings.reduced_motion or not is_instance_valid(primary_button):
		return
	if is_instance_valid(_pulse):
		_pulse.kill()
	await get_tree().create_timer(1.2).timeout
	if not is_inside_tree() or Settings.reduced_motion or not is_instance_valid(primary_button):
		return
	primary_button.pivot_offset = primary_button.size / 2.0
	_pulse = create_tween().set_loops()
	_pulse.tween_property(primary_button, "scale", Vector2.ONE * 1.025, 0.9).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	_pulse.tween_property(primary_button, "scale", Vector2.ONE, 0.9).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)


func _on_accessibility_changed() -> void:
	if Settings.reduced_motion:
		if is_instance_valid(_pulse):
			_pulse.kill()
		if is_instance_valid(primary_button):
			primary_button.scale = Vector2.ONE
	else:
		_start_pulse()


# ---- actions ----------------------------------------------------------------------------

## `kind`: `match` (3 v 3 against bots) or `training` (the sandbox with dummies and a sparring partner).
func _on_practice_pressed(kind: StringName = &"training") -> void:
	if _loading:
		return
	Session.practice_kind = kind
	# the first practice match opens with the walkthrough
	if not Session.coach_seen:
		Session.coach_pending = true
	_loading = true
	_show_loading("Getting the street ready")
	ResourceLoader.load_threaded_request(PRACTICE_SCENE)
	set_process(true)


## The practice match is a big 3D scene: it loads in the background while the screen shows progress.
func _process(_delta: float) -> void:
	if not _loading:
		set_process(false)
		return
	var progress: Array = []
	var status: ResourceLoader.ThreadLoadStatus = ResourceLoader.load_threaded_get_status(PRACTICE_SCENE, progress)
	if _loading_dots != null:
		_loading_dots.text = ".".repeat(1 + int(Time.get_ticks_msec() / 300) % 3)
	if status == ResourceLoader.THREAD_LOAD_LOADED:
		_loading = false
		get_tree().change_scene_to_packed(ResourceLoader.load_threaded_get(PRACTICE_SCENE) as PackedScene)
	elif status == ResourceLoader.THREAD_LOAD_FAILED:
		_loading = false
		get_tree().change_scene_to_file(PRACTICE_SCENE)


func _show_loading(message: String) -> void:
	var cover: ColorRect = ColorRect.new()
	cover.name = "Loading"
	cover.z_index = MenuDialog.MODAL_Z  # over the mode cards too
	cover.color = Color(0.02, 0.05, 0.16, 0.0)
	cover.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	cover.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(cover)
	var center: CenterContainer = CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	center.mouse_filter = Control.MOUSE_FILTER_IGNORE
	cover.add_child(center)
	var stack: VBoxContainer = VBoxContainer.new()
	stack.add_theme_constant_override("separation", 6)
	center.add_child(stack)
	var text: Label = Label.new()
	text.text = message
	text.theme_type_variation = &"DisplayLabel"
	text.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	text.add_theme_font_size_override("font_size", 40)
	text.add_theme_color_override("font_color", CREAM)
	text.add_theme_constant_override("outline_size", 4)
	stack.add_child(text)
	_loading_dots = Label.new()
	_loading_dots.text = "."
	_loading_dots.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_loading_dots.add_theme_font_size_override("font_size", 56)
	_loading_dots.add_theme_color_override("font_color", Color.WHITE)
	stack.add_child(_loading_dots)
	if Settings.reduced_motion:
		cover.color.a = 0.88
	else:
		var fade: Tween = create_tween()
		fade.tween_property(cover, "color:a", 0.88, 0.25)


func _open_lobby(intent: LobbyScreen.Intent, hotspot: bool) -> void:
	Session.lobby_intent = intent
	Session.lobby_hotspot = hotspot
	if not (hotspot and Session.lobby_preview):
		Session.lobby_preview = false
	open_lobby_action.call(intent)


func _change_to_lobby(_intent: LobbyScreen.Intent) -> void:
	get_tree().change_scene_to_file(LOBBY_SCENE)


func _show_dialog(new_dialog: MenuDialog) -> void:
	if dialog != null and is_instance_valid(dialog) and dialog.is_open:
		dialog.close(false)
	dialog = new_dialog
	dialog.closed.connect(_sync_ambient)
	dialog.open(self)
	_sync_ambient()


## The idle motion of Home (the card's bob, the drifting lights) runs only while Home is what you see: not under a
## page and not under a sheet, where nothing needs redrawing.
func _sync_ambient() -> void:
	var covered: bool = current_page != &"" or (dialog != null and is_instance_valid(dialog) and dialog.is_open)
	backdrop.ambient = not covered
	carousel.bob = 0.0 if covered else AMBIENT_BOB

## The detail sheet of a kid from the Book.
func open_character_detail(character: CharacterDef) -> MenuDialog:
	var card: MenuDialog = CharactersPage.detail(character)
	_show_dialog(card)
	return card


## The detail sheet of a weapon from the Book.
func open_weapon_detail(weapon: WeaponDef) -> MenuDialog:
	var card: MenuDialog = WeaponsPage.detail(weapon)
	_show_dialog(card)
	return card


## Your profile: the name and the face (see `ProfileSheet`). The chip, the Settings card and a toast answer a save.
func open_name_editor() -> MenuDialog:
	var card: MenuDialog = ProfileSheet.build(_on_profile_saved)
	_show_dialog(card)
	return card


func _on_profile_saved() -> void:
	profile_chip.refresh()
	settings_page.refresh()
	UiToast.say(self, "Profile saved")


## Quit: asks first (a stray tap must not close the game).
func open_quit_confirm() -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Quit game?", 560.0)
	var stay: Button = MenuDialog.make_button("Cancel", "grey", 32)
	var leave: Button = MenuDialog.make_button("Quit", "red", 32)
	card.add_buttons([stay, leave])
	stay.pressed.connect(card.close)
	leave.pressed.connect(func() -> void:
		card.close(false)
		quit_action.call())
	_show_dialog(card)
	return card


## Close the game: the app quits; a browser tab has no "quit", so it leaves the page instead.
func _quit_game() -> void:
	if OS.has_feature("web"):
		JavaScriptBridge.eval("window.close(); setTimeout(function () { window.location.replace('about:blank'); }, 120);")
	else:
		get_tree().quit()


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel") and (dialog == null or not is_instance_valid(dialog) or not dialog.is_open):
		get_viewport().set_input_as_handled()
		_go_back()


## Back closes the open page; on the home page it asks to quit.
func _go_back() -> void:
	if current_page != &"":
		close_page()
	else:
		open_quit_confirm()


## The phone's back button: close what is open, or ask to quit.
func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_GO_BACK_REQUEST:
		if dialog != null and is_instance_valid(dialog) and dialog.is_open:
			dialog.close()
		else:
			_go_back()


## How to play, "Practice with the walkthrough": a practice match that opens with the four steps again.
func start_walkthrough() -> void:
	Session.coach_pending = true
	_on_practice_pressed()
