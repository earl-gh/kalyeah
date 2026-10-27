class_name TitleScreen
extends Control
## The main screen, built as a game lobby: a living backdrop, a top bar with the game's logo, the mode carousel as the hero (the chosen card framed in light, floating a little), the
## mode's name, one phrase and the main action under it, and a tab bar at the bottom (Play, Book,
## History, Settings). The tabs swap the middle of the screen; the pages take the whole height (the logo
## shows on Play only, each page has its own title and Settings holds the profile).
## Android Back closes a tab back to Play, and on Play asks to quit.

const SERVER_SCENE: String = "res://server/main.tscn"
const LOBBY_SCENE: String = "res://scenes/ui/lobby.tscn"
const PRACTICE_SCENE: String = "res://scenes/match/practice.tscn"
const LOGO: Texture2D = preload("res://assets/ui/kalyeah_logo.png")
const TIPS_ICON: Texture2D = preload("res://assets/ui/tips_icon.png")
const MAIL_ICON: Texture2D = preload("res://assets/ui/mail_icon.png")

## The game modes in card order: Online, Local in the middle (the default, `Session.menu_mode` 1), Practice (owner).
## `accent` tints the frame of the card and the glow behind it.
const MODES: Array[Dictionary] = [
	{"id": "online", "bare": true, "title": "Online", "color": "orange", "accent": "45dd78", "power": 7450,
		"text": "Invite friends with a room code!"},
	{"id": "hotspot", "bare": true, "title": "Local", "color": "blue", "accent": "4aa0ff", "power": 9999,
		"text": "Battle your friends nearby!"},
	{"id": "practice", "bare": true, "title": "Practice", "color": "green", "accent": "a45cf0", "power": 1200,
		"text": "Take on 3 bots, solo!"},
]
## The bottom tabs, left to right: Street (what each game is and how it plays), Cards (the Book of kids and weapons), Play (the home
## page), History (your last battles) and Settings.
const TABS: Array[Dictionary] = [
	{"id": &"street", "label": "Street", "icon": &"street"},
	{"id": &"codex", "label": "Cards", "icon": &"cards"},
	{"id": &"home", "label": "Play", "icon": &"play"},
	{"id": &"history", "label": "History", "icon": &"history"},
	{"id": &"settings", "label": "Settings", "icon": &"settings"},
]
## The pages that belong to Settings: its tab stays lit while one is open.
const SETTINGS_PAGES: Array[StringName] = [&"howto"]
## The hero cards (1.75:2.5, owner) and how high the stage is.
const HERO_CARD: Vector2 = Vector2(400.0, 571.0)
## The hero grows with the stage (phones taller than 16:9 have the room): 1.75:2.5, between these heights, with
## this much air round it for the glow and the bob.
const HERO_RATIO: float = 0.7
## The mode models (owner: the dioramas) are shown bare, in a shell as wide as the picture is (768 x 717 PNG files): the models of
## `MODES` with `bare` are this shape instead of the paper card's.
const MODEL_RATIO: float = 768.0 / 717.0
## How much of the stage's width is left free beside a mode model.
const MODEL_SIDE_ROOM: float = 40.0
## How big a mode model is next to the room the stage gives it (owner: 0.64).
const MODEL_SCALE: float = 0.64
## The mode models behind the chosen one stand this share of a model's height higher than it, and sit this many times further out to each
## side than a card carousel's cards do, which leaves a gap between the two behind it (owner).
const MODEL_BACK_RISE: float = 0.25
const MODEL_SPREAD_SCALE: float = 1.73
## The mode models behind the chosen one are 0.75 of its size (owner).
const MODEL_BACK_SCALE: float = 0.75
const HERO_MIN_HEIGHT: float = 440.0
const HERO_MAX_HEIGHT: float = 780.0
const HERO_AIR: float = 36.0
## The cards sit this far above the bottom of their stage, so the name, its phrase and the buttons stay close under them.
const HERO_BOTTOM_GAP: float = 34.0
## How far the backlight reaches round the chosen card (times the card's height).
const BACKLIGHT_REACH: float = 1.15
## The black outline round the mode's one-line phrase.
const MODE_TEXT_OUTLINE: int = 6
## The share of the room the hero card takes (owner: smaller, so the floating card floats with space round it).
const HERO_SCALE: float = 0.86
## The button row's slot takes this share of the free height next to the card's stage (1.0).
const ACTIONS_SLOT_SHARE: float = 0.35
## The gap under the mode name (negative so the phrase is pulled nearer to the name).
const TITLES_SEPARATION: int = -10
## The mode name is drawn over the cards it hangs on (the carousel stacks its cards by z index).
const TITLE_Z: int = 20
## The name tag: the air kept above the blurred models behind it (they bob a little and their edges are soft), the smallest it shrinks to
## on a short phone, and how far its lanyard runs above the top of the screen.
const TAG_AIR: float = 10.0
const TAG_MIN_SCALE: float = 0.6
## How far below the middle of its band the tag hangs (owner: lower).
const TAG_DROP: float = 16.0
const TAG_OVERSHOOT: float = 60.0
## The phrase and the buttons are drawn above the backdrop's lights (`UiBackdrop.LIGHT_Z`) and below the chosen mode model.
const TEXT_Z: int = 8
## How far the name is lifted so its letters (not its line box) are centred on the card's bottom edge.
const TITLE_LIFT: float = 12.0
const AMBIENT_BOB: float = 12.0
## The mode chips need a stage at least this tall to leave the hero a good size; on a shorter screen they step aside.
const CHIPS_HEIGHT: float = 42.0
const CHIPS_MIN_STAGE: float = 760.0
## Which way a page slides in: the pages left to right as their tabs are.
const PAGE_RANK: Dictionary[StringName, int] = {&"street": -2, &"codex": -1, &"": 0, &"history": 1, &"settings": 2, &"howto": 3}
const RULES_PATH: String = "res://data/rules/game_rules.tres"
const LOGO_HEIGHT: float = 92.0
## How far the logo is dropped from the top edge of the screen.
const LOGO_DROP: float = 62.0
## How far the tips icon stands in from the left margin of the screen, on top of it (owner: more room at the left).
const TIPS_INSET: float = 22.0
## The two top icons (the calendar and the envelope) are baked so their gold rings are the same size and centred on their picture, so
## they match each other and centre on the logo: this is the picture's size on screen (the ring is 0.886 of it).
const TOP_ICON_SIZE: float = 96.0
const SIDE_MARGIN: float = 28.0
const TOP_MARGIN: float = 20.0
const CTA_HEIGHT: float = 117.0
## The actions are a centred pair, the same size: as wide as the longer word needs plus this padding on each side, and no narrower than
## `CTA_MIN_WIDTH`.
const CTA_MIN_WIDTH: float = 240.0
## The buttons' lettering: 1.25 x `UiMetrics.BODY` (owner).
const CTA_FONT: int = UiMetrics.HERO_BUTTON
const CTA_PADDING: float = 56.0
## The main action's breath: it waits `PULSE_DELAY` seconds, then swells to `PULSE_SCALE` and settles, each way in
## `PULSE_HALF` seconds.
const PULSE_DELAY: float = 1.2
const PULSE_SCALE: float = 1.025
const PULSE_HALF: float = 0.9
const CREAM: Color = KalyeahTheme.CREAM

## The screen asks for the game to close (replaced in tests).
var quit_action: Callable = _quit_game
## The screen leaves for the lobby (replaced in tests).
var open_lobby_action: Callable = _change_to_lobby

var backdrop: UiBackdrop
var carousel: MenuCarousel
var name_tag: NameTag
## The mode models (the carousel's items): Online, Local and Practice, in order.
var mode_models: Array[Button]:
	get:
		return carousel.cards
var tab_bar: UiTabBar
var mode_name_label: Label
var mode_text_label: Label
var primary_button: Button
var secondary_button: Button
var dialog: MenuDialog
## The tab buttons by page id (street, home, settings).
var corner_buttons: Dictionary[StringName, Button] = {}
var pages: Dictionary[StringName, Control] = {}
var current_page: StringName = &""
var street_page: StreetPage
var codex_page: CodexPage
var characters_page: CharactersPage:
	get: return codex_page.characters_page
var weapons_page: WeaponsPage:
	get: return codex_page.weapons_page
var history_page: HistoryPage
var settings_page: SettingsPage
var howto_page: HowToPlayPage

var _loading: bool = false
var _loading_dots: Label
var _margin: MarginContainer
var _home: VBoxContainer
var _top_bar: Control
var tips_button: Button
var mail_button: Button
var _title_holder: Control
var mode_chips: HBoxContainer
var _page_holder: Control
var _page_tween: Tween
var _pulse: Tween
## Bumped by every start and stop of the breath: a start still waiting out its delay gives up if another came after it.
var _pulse_generation: int = 0


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
	carousel.selected.connect(_on_mode_selected)
	carousel.laid_out.connect(_aim_backlight)
	_aim_backlight.call_deferred()
	primary_button.pressed.connect(_on_primary_pressed)
	# the press and the breath both scale the main action: the breath rests while a thumb is on it
	primary_button.button_down.connect(_stop_pulse)
	primary_button.button_up.connect(_start_pulse)
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
	# the bottom bar is not inside the margins: it runs edge to edge and down to the bottom of the screen
	tab_bar = UiTabBar.new(TABS)
	tab_bar.tab_pressed.connect(_on_tab_pressed)
	add_child(tab_bar)
	for id: StringName in tab_bar.tabs:
		corner_buttons[id] = tab_bar.tabs[id]
	tab_bar.set_active(&"home")


## The top bar: the KALYEAH! logo, centred and dropped a little from the top edge. Your profile lives in Settings.
func _build_top_bar() -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "TopBar"
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.custom_minimum_size = Vector2(0.0, LOGO_HEIGHT + LOGO_DROP)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	# the tips icon at the left, level with the logo; an empty cell of the same share at the right keeps the logo in the middle
	var left: Control = Control.new()
	left.name = "TopLeft"
	left.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	left.mouse_filter = Control.MOUSE_FILTER_IGNORE
	left.size_flags_vertical = Control.SIZE_SHRINK_END
	left.custom_minimum_size = Vector2(0.0, LOGO_HEIGHT)
	row.add_child(left)
	tips_button = _icon_button("TipsButton", "Game of the month", TIPS_ICON, _open_tips)
	tips_button.position = Vector2(TIPS_INSET, (LOGO_HEIGHT - TOP_ICON_SIZE) / 2.0)
	left.add_child(tips_button)
	var logo: TextureRect = TextureRect.new()
	logo.name = "Logo"
	logo.texture = LOGO
	logo.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	logo.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	logo.custom_minimum_size = Vector2(LOGO_HEIGHT * float(LOGO.get_width()) / float(LOGO.get_height()), LOGO_HEIGHT)
	logo.size_flags_vertical = Control.SIZE_SHRINK_END
	logo.mouse_filter = Control.MOUSE_FILTER_IGNORE
	# the logo is in front of every part of the name tag's lanyard, which hangs from the top of the screen behind it
	logo.z_as_relative = false
	logo.z_index = NameTag.LOGO_Z
	row.add_child(logo)
	var right: Control = Control.new()
	right.name = "TopRight"
	right.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right.mouse_filter = Control.MOUSE_FILTER_IGNORE
	right.size_flags_vertical = Control.SIZE_SHRINK_END
	right.custom_minimum_size = Vector2(0.0, LOGO_HEIGHT)
	mail_button = _icon_button("MailButton", "Donate", MAIL_ICON, _open_mail)
	right.add_child(mail_button)
	mail_button.set_anchors_and_offsets_preset(Control.PRESET_CENTER_RIGHT)
	mail_button.offset_left = -(TOP_ICON_SIZE + TIPS_INSET)
	mail_button.offset_right = -TIPS_INSET
	mail_button.offset_top = -TOP_ICON_SIZE / 2.0
	mail_button.offset_bottom = TOP_ICON_SIZE / 2.0
	row.add_child(right)
	return row


## A round picture button of the top row (the tips icon and the mail icon): `TOP_ICON_SIZE` square, no button face.
func _icon_button(button_name: String, say: String, texture: Texture2D, action: Callable) -> Button:
	var button: Button = Button.new()
	button.name = button_name
	button.accessibility_name = say
	button.flat = true
	button.focus_mode = Control.FOCUS_ALL
	button.custom_minimum_size = Vector2.ONE * TOP_ICON_SIZE
	button.size = Vector2.ONE * TOP_ICON_SIZE
	for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
		button.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	button.add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.RADIUS_L))
	var picture: TextureRect = TextureRect.new()
	picture.name = "Picture"
	picture.texture = texture
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	button.add_child(picture)
	UiKit.feel_press(button)
	button.pressed.connect(action)
	return button


func _open_mail() -> void:
	open_donate()


## The calendar icon opens the Game of the month: one Filipino street game a month, taught in full.
func _open_tips() -> void:
	open_game_of_the_month()


func open_game_of_the_month(month: int = 0) -> MenuDialog:
	var card: MenuDialog = GameOfMonthSheet.build(month)
	_show_dialog(card)
	return card


## The envelope opens the Donate sheet.
func open_donate() -> MenuDialog:
	var card: MenuDialog = DonateSheet.build()
	_show_dialog(card)
	return card


## The Play page: the hero carousel, the mode's name and phrase and the actions.
func _build_home() -> VBoxContainer:
	var home: VBoxContainer = VBoxContainer.new()
	home.name = "Home"
	home.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	home.mouse_filter = Control.MOUSE_FILTER_IGNORE
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
	carousel.bottom_gap = HERO_BOTTOM_GAP
	carousel.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	stage.add_child(carousel)
	carousel.back_rise = MODEL_BACK_RISE
	carousel.spread_scale = MODEL_SPREAD_SCALE
	carousel.wrap = true
	carousel.scale_loss = 1.0 - MODEL_BACK_SCALE
	carousel.setup("", MODES, Session.menu_mode)
	carousel.bob = AMBIENT_BOB
	stage.resized.connect(_fit_hero)
	stage.resized.connect(_place_title)
	stage.resized.connect(_aim_backlight)
	stage.resized.connect(_place_name_tag.call_deferred)
	name_tag = NameTag.new()
	name_tag.tapped.connect(open_name_editor)
	stage.add_child(name_tag)
	home.add_child(_build_titles())
	# the button row is centred in the room between the phrase and the bar
	var slot: CenterContainer = CenterContainer.new()
	slot.name = "ActionsSlot"
	slot.z_index = TEXT_Z
	slot.size_flags_vertical = Control.SIZE_EXPAND_FILL
	slot.size_flags_stretch_ratio = ACTIONS_SLOT_SHARE
	slot.custom_minimum_size = Vector2(0.0, CTA_HEIGHT)
	slot.mouse_filter = Control.MOUSE_FILTER_IGNORE
	slot.add_child(_build_actions())
	home.add_child(slot)
	return home


## The mode's name (big, outlined), its one phrase and a dot for each mode.
func _build_titles() -> Control:
	var titles: VBoxContainer = VBoxContainer.new()
	titles.name = "Titles"
	titles.z_index = TEXT_Z
	titles.alignment = BoxContainer.ALIGNMENT_CENTER
	# the phrase sits close under the name (negative: it is pulled up towards it)
	titles.add_theme_constant_override("separation", TITLES_SEPARATION)
	titles.mouse_filter = Control.MOUSE_FILTER_IGNORE
	mode_name_label = Label.new()
	mode_name_label.name = "ModeName"
	UiKit.letter(mode_name_label, UiMetrics.DISPLAY, Color.WHITE, Color.BLACK)
	mode_name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	# the name straddles the bottom edge of the chosen card: this holder sits where the stage ends and the name hangs
	# off it upwards by the card's air plus half its own height (`_place_title`)
	mode_name_label.z_index = TITLE_Z
	_title_holder = Control.new()
	_title_holder.name = "TitleHolder"
	_title_holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_title_holder.add_child(mode_name_label)
	titles.add_child(_title_holder)
	mode_text_label = Label.new()
	mode_text_label.name = "ModeText"
	mode_text_label.add_theme_font_size_override("font_size", UiMetrics.DETAIL)
	# white letters with a black outline, like the name over it (owner)
	mode_text_label.add_theme_color_override("font_color", Color.WHITE)
	mode_text_label.add_theme_color_override("font_outline_color", Color.BLACK)
	mode_text_label.add_theme_constant_override("outline_size", MODE_TEXT_OUTLINE)
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
	return titles


## The main action in one row: Create room and Join room, or Play vs bots and Training.
func _build_actions() -> Control:
	var actions: HBoxContainer = HBoxContainer.new()
	actions.name = "Actions"
	actions.alignment = BoxContainer.ALIGNMENT_CENTER
	actions.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	actions.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	actions.mouse_filter = Control.MOUSE_FILTER_IGNORE
	primary_button = _action_button(actions, "PrimaryButton", "")
	secondary_button = _action_button(actions, "SecondaryButton", "Join room")
	return actions


func _action_button(parent: Control, button_name: String, text: String) -> Button:
	var button: Button = MenuDialog.make_button(text, "gold", CTA_FONT, Vector2(CTA_MIN_WIDTH, CTA_HEIGHT))
	button.name = button_name
	parent.add_child(button)
	return button


## Both buttons are as wide as the longer of their words needs (and no narrower than CTA_MIN_WIDTH), so the pair stays equal.
func _fit_buttons() -> void:
	var font: Font = primary_button.get_theme_font("font")
	var widest: float = CTA_MIN_WIDTH
	for button: Button in [primary_button, secondary_button]:
		var words: float = font.get_string_size(button.text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, CTA_FONT).x
		widest = maxf(widest, words + CTA_PADDING * 2.0)
	for button: Button in [primary_button, secondary_button]:
		button.custom_minimum_size.x = widest


func _build_pages() -> void:
	street_page = StreetPage.new()
	codex_page = CodexPage.new()
	history_page = HistoryPage.new()
	settings_page = SettingsPage.new()
	howto_page = HowToPlayPage.new()
	pages = {&"street": street_page, &"codex": codex_page, &"history": history_page, &"settings": settings_page, &"howto": howto_page}
	for id: StringName in pages:
		var page: Control = pages[id]
		page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		page.visible = false
		_page_holder.add_child(page)
	codex_page.weapons_page.weapon_opened.connect(open_weapon_detail)
	codex_page.characters_page.character_opened.connect(open_character_detail)
	settings_page.profile_pressed.connect(open_name_editor)
	settings_page.quit_pressed.connect(open_quit_confirm)
	settings_page.howto_pressed.connect(open_page.bind(&"howto"))
	howto_page.walkthrough_requested.connect(start_walkthrough)
	history_page.play_pressed.connect(close_page)
	howto_page.back_pressed.connect(open_page.bind(&"settings"))


func _on_tab_pressed(id: StringName) -> void:
	if id == &"home":
		close_page()
	else:
		open_page(id)


## Opens one of the pages (street, codex = the book, history, settings) in the middle of the screen; it slides in from the
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
	# How to play is reached from Settings: that tab stays lit
	var lit: StringName = &"settings" if id in SETTINGS_PAGES else id
	tab_bar.set_active(lit)
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
		var chip: PanelContainer = UiKit.chip(fact, UiKit.CHIP_SLATE)
		chip.mouse_filter = Control.MOUSE_FILTER_IGNORE
		mode_chips.add_child(chip)


## Whether the mode models are bare dioramas (no paper card).
static func models_are_bare() -> bool:
	return bool(MODES[0].get("bare", false))


## The shape of a mode model: width over height.
static func model_ratio() -> float:
	return MODEL_RATIO if models_are_bare() else HERO_RATIO


## Sizes the hero cards to the stage (taller phones get a bigger card), then rebuilds them.
func _fit_hero() -> void:
	var stage: Control = carousel.get_parent() as Control
	if stage.size.y < 100.0:
		return
	var total: float = stage.size.y
	# the stage as it would be without the chips decides whether they show (so showing them never flips it back)
	var bare: float = total + (CHIPS_HEIGHT if mode_chips.visible else 0.0)
	mode_chips.visible = bare - CHIPS_HEIGHT >= CHIPS_MIN_STAGE
	var height: float = clampf(bare - (CHIPS_HEIGHT if mode_chips.visible else 0.0) - HERO_AIR, HERO_MIN_HEIGHT, HERO_MAX_HEIGHT)
	var diorama: bool = models_are_bare()
	var ratio: float = MODEL_RATIO if diorama else HERO_RATIO
	var width: float = minf(height * ratio, stage.size.x - (MODEL_SIDE_ROOM if diorama else 120.0)) * (MODEL_SCALE if diorama else HERO_SCALE)
	var fitted: Vector2 = Vector2(roundf(width), roundf(width / ratio))
	if fitted.distance_to(carousel.card_size) < 3.0:
		return
	carousel.card_size = fitted
	carousel.setup("", MODES, carousel.index)
	_place_title()



## Hangs the name tag between the bottom of the logo and the top of the chosen model, a little below the middle of that band (owner), and
## never so low that it touches the chosen model. It shrinks on a short phone. The blurred models behind it pass under the card.
func _place_name_tag() -> void:
	if name_tag == null or carousel == null or _top_bar == null:
		return
	var stage: Control = carousel.get_parent() as Control
	var logo: Control = _top_bar.find_child("Logo", true, false) as Control
	var band_top: float = logo.get_global_rect().end.y - stage.get_global_rect().position.y
	var band_bottom: float = carousel.front_top()
	# the middle of the band, then lower (owner): the card stands closer to the chosen model, and the blurred models behind pass under it
	var middle: float = (band_top + band_bottom) / 2.0 + TAG_DROP
	var fitted: float = clampf(minf((band_bottom - band_top - 2.0 * TAG_AIR - TAG_DROP) / NameTag.CARD_HEIGHT, (band_bottom - TAG_AIR - middle) / (NameTag.CARD_HEIGHT / 2.0)), TAG_MIN_SCALE, 1.0)
	var card_top: float = middle - NameTag.CARD_HEIGHT * fitted / 2.0
	var hole: Vector2 = Vector2(stage.size.x / 2.0, card_top + NameTag.SLOT_MIDDLE * fitted)
	name_tag.hang(hole, -stage.get_global_rect().position.y - TAG_OVERSHOOT, fitted)


## Points the backdrop's backlight at the chosen card: the bright middle sits right behind it.
func _aim_backlight() -> void:
	if backdrop == null or carousel == null or carousel.cards.is_empty():
		return
	var card: Button = carousel.cards[carousel.index]
	var centre: Vector2 = backdrop.get_global_transform().affine_inverse() * card.get_global_rect().get_center()
	backdrop.aim_backlight(centre, card.size.y * BACKLIGHT_REACH)


## Hangs the mode's name on the bottom edge of the chosen card: its centre is on that edge.
func _place_title() -> void:
	if _title_holder == null or carousel == null:
		return
	var air: float = HERO_BOTTOM_GAP
	var height: float = mode_name_label.get_combined_minimum_size().y
	mode_name_label.set_anchors_preset(Control.PRESET_TOP_WIDE)
	# the letters sit a little low in their line, so lift the line to put the letters themselves on the edge
	var lift: float = TITLE_LIFT + air
	mode_name_label.offset_top = -lift - height / 2.0
	mode_name_label.offset_bottom = -lift + height / 2.0


## The chosen mode's name and phrase, the action buttons and the backdrop's tint.
func _refresh_mode() -> void:
	var mode: Dictionary = MODES[Session.menu_mode]
	var rooms: bool = String(mode["id"]) != "practice"
	mode_name_label.text = String(mode["title"])
	mode_text_label.text = String(mode["text"])
	_fill_chips(String(mode["id"]))
	primary_button.text = "Create" if rooms else "Play"
	secondary_button.text = "Join"
	# Practice has the one button, Play (no Join, no Training)
	secondary_button.visible = rooms
	KalyeahTheme.style_button(primary_button, "orange")
	KalyeahTheme.style_button(secondary_button, "blue")
	_fit_buttons()


func _on_mode_selected(index: int) -> void:
	Session.menu_mode = index
	_refresh_mode()


func _on_primary_pressed() -> void:
	match String(MODES[Session.menu_mode]["id"]):
		"practice":
			open_practice_confirm()
		"online":
			_open_lobby(LobbyScreen.Intent.CREATE, false)
		_:
			open_hotspot_create()


func _on_secondary_pressed() -> void:
	if String(MODES[Session.menu_mode]["id"]) == "hotspot":
		open_hotspot_join()
	else:
		_open_lobby(LobbyScreen.Intent.JOIN, false)


## Practice asks first (a stray tap must not start a match); Play then shows the loading screen.
func open_practice_confirm() -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Practice", MenuDialog.WIDTH_SMALL)
	card.add_label("3 v 3 against bots. Ready to rumble?", MenuDialog.TEXT_SIZE, MenuDialog.TEXT_COLOR, HORIZONTAL_ALIGNMENT_CENTER)
	var cancel: Button = MenuDialog.make_button("Cancel", "grey", UiMetrics.BODY)
	var play: Button = MenuDialog.make_button("Play", "gold", UiMetrics.BODY)
	card.add_buttons([cancel, play])
	cancel.pressed.connect(card.close)
	play.pressed.connect(func() -> void:
		card.close(false)
		_on_practice_pressed(&"match"))
	_show_dialog(card)
	return card


## Hotspot Create: a dialog checks the hotspot is on, then the room opens.
func open_hotspot_create() -> MenuDialog:
	var card: MenuDialog = HotspotDialogs.create_check(func() -> void: _open_lobby(LobbyScreen.Intent.CREATE, true))
	_show_dialog(card)
	return card


## Hotspot Join: a dialog checks the Wi-Fi and finds the host's room by itself (no code, no address), then the lobby joins it.
func open_hotspot_join(interfaces: Callable = Callable(), plan_for: Callable = Callable()) -> MenuDialog:
	var card: MenuDialog = HotspotDialogs.join_check(func(url: String) -> void:
		Session.lobby_url = url
		_open_lobby(LobbyScreen.Intent.JOIN, true), interfaces, plan_for)
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
	# the page and the home stop above the bar, which is flush with the bottom edge (and reaches under the home bar)
	_margin.add_theme_constant_override("margin_bottom", int(UiTabBar.HEIGHT + inset.w + UiMetrics.COMPACT_GAP))
	tab_bar.set_anchors_preset(Control.PRESET_BOTTOM_WIDE)
	tab_bar.offset_left = 0.0
	tab_bar.offset_right = 0.0
	tab_bar.offset_bottom = 0.0
	tab_bar.offset_top = -(UiTabBar.HEIGHT + inset.w)


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


## The main action swells and settles slowly, drawing the eye to it. Only the latest call starts a breath, so quick taps
## or a quick Reduce motion toggle never leave two breaths fighting over the button's scale.
func _start_pulse() -> void:
	_stop_pulse()
	if not is_inside_tree() or Settings.reduced_motion or not is_instance_valid(primary_button):
		return
	var generation: int = _pulse_generation
	await get_tree().create_timer(PULSE_DELAY).timeout
	if generation != _pulse_generation or not is_inside_tree() or Settings.reduced_motion or not is_instance_valid(primary_button):
		return
	primary_button.pivot_offset = primary_button.size / 2.0
	_pulse = create_tween().set_loops()
	_pulse.tween_property(primary_button, "scale", Vector2.ONE * PULSE_SCALE, PULSE_HALF).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	_pulse.tween_property(primary_button, "scale", Vector2.ONE, PULSE_HALF).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)


## Stops the breath, and any start still waiting to begin one.
func _stop_pulse() -> void:
	_pulse_generation += 1
	if is_instance_valid(_pulse):
		_pulse.kill()


func _on_accessibility_changed() -> void:
	if Settings.reduced_motion:
		_stop_pulse()
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
	text.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	UiKit.letter(text, UiMetrics.HEADLINE, CREAM)
	stack.add_child(text)
	_loading_dots = Label.new()
	_loading_dots.text = "."
	_loading_dots.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_loading_dots.add_theme_font_size_override("font_size", UiMetrics.DISPLAY)
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
	backdrop.lights_on = current_page == &""
	carousel.bob = 0.0 if covered else AMBIENT_BOB
	name_tag.swing = not covered

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


## Your profile: the name and the face (see `ProfileSheet`). The Settings card and a toast answer a save.
func open_name_editor() -> MenuDialog:
	var card: MenuDialog = ProfileSheet.build(_on_profile_saved)
	_show_dialog(card)
	return card


func _on_profile_saved() -> void:
	settings_page.refresh()
	name_tag.set_player_name(Session.player_name)
	_place_name_tag()
	UiToast.say(self, "Profile saved!")


## Quit: asks first (a stray tap must not close the game).
func open_quit_confirm() -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Quit game?", MenuDialog.WIDTH_SMALL)
	var stay: Button = MenuDialog.make_button("Cancel", "grey", UiMetrics.BODY)
	var leave: Button = MenuDialog.make_button("Quit", "red", UiMetrics.BODY)
	card.add_buttons([stay, leave])
	stay.pressed.connect(card.close)
	leave.pressed.connect(func() -> void:
		card.close(false)
		quit_action.call())
	_show_dialog(card)
	return card


## Close the game.
func _quit_game() -> void:
	get_tree().quit()


## Back closes the open page; on the home page it asks to quit.
func _go_back() -> void:
	if current_page != &"":
		close_page()
	else:
		open_quit_confirm()


## The phone's back button: an open dialog closes itself; otherwise close the page, or ask to quit. It acts after the
## notification has walked the tree (a dialog cannot be added to a node that is still handing it out).
func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_GO_BACK_REQUEST:
		_back_pressed.call_deferred()


func _back_pressed() -> void:
	if dialog == null or not is_instance_valid(dialog) or not dialog.is_open:
		_go_back()


## How to play, "Try the walkthrough!": a practice match that opens with the four steps again.
func start_walkthrough() -> void:
	Session.coach_pending = true
	_on_practice_pressed()
