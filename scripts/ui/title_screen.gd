class_name TitleScreen
extends Control
## The main screen. The home page: a top row with the book (left), the KALYEAH! logo (middle) and
## the history and settings buttons (right); a horizontal carousel of large 3:4 mode cards (Hotspot,
## Online, Practice) with every card always visible, stacked behind the chosen one; and the chosen
## mode's name, one phrase and action buttons low on the screen. A page opens over the home page with
## a Back button (so does Android Back). Settings holds the player's name and the infrequent quit
## action, while Android Back on the home page asks to quit.

const SERVER_SCENE: String = "res://server/main.tscn"
const LOBBY_SCENE: String = "res://scenes/ui/lobby.tscn"
const PRACTICE_SCENE: String = "res://scenes/match/practice.tscn"
const BACKGROUND: Texture2D = preload("res://assets/ui/main_bg.webp")

## The game modes, first is the default (owner: hotspot first, not online).
const MODES: Array[Dictionary] = [
	{"id": "hotspot", "title": "HOTSPOT", "color": "blue",
		"text": "Play with your friends offline."},
	{"id": "online", "title": "ONLINE", "color": "orange",
		"text": "Invite friends online."},
	{"id": "practice", "title": "PRACTICE", "color": "green",
		"text": "Train alone against dummies."},
]
## The KALYEAH! logo (the owner's cardboard sign) at the centre top of the home page.
const LOGO: Texture2D = preload("res://assets/ui/kalyeah_logo.png")
const LOGO_WIDTH: float = 300.0
## How far the logo and the corner buttons sit down from the top.
const TOP_ROW_DROP: float = 26.0
const CORNER_BUTTON: float = 84.0
## The corner buttons: the book on the left, history and settings on the right.
const CORNERS: Array[Dictionary] = [
	{"id": &"codex", "icon": &"book", "side": &"left"},
	{"id": &"history", "icon": &"history", "side": &"right"},
	{"id": &"settings", "icon": &"settings", "side": &"right"},
]
## Room left under the action buttons so they sit low on the screen, but not at its very bottom.
const ACTIONS_LIFT: float = 84.0

const SIDE_MARGIN: float = 34.0
const TOP_MARGIN: float = 26.0
const BOTTOM_MARGIN: float = 22.0
const CREAM: Color = KalyeahTheme.CREAM

## The screen asks for the game to close (replaced in tests).
var quit_action: Callable = _quit_game
## The screen leaves for the lobby (replaced in tests).
var open_lobby_action: Callable = _change_to_lobby

var logo: TextureRect
var carousel: MenuCarousel
var mode_name_label: Label
var mode_text_label: Label
var primary_button: Button
var secondary_button: Button
var quit_button: Button
var dialog: MenuDialog
var corner_buttons: Dictionary[StringName, Button] = {}
var pages: Dictionary[StringName, Control] = {}
var current_page: StringName = &""
var back_button: Button
var codex_page: CodexPage
var characters_page: CharactersPage
var weapons_page: WeaponsPage
var history_page: HistoryPage
var settings_page: SettingsPage

var _loading: bool = false
var _loading_dots: Label


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
	_build_background()
	_build_layout()
	_apply_safe_area()
	_refresh_mode()
	carousel.selected.connect(_on_mode_selected)
	primary_button.pressed.connect(_on_primary_pressed)
	secondary_button.pressed.connect(_on_secondary_pressed)
	get_viewport().size_changed.connect(_apply_safe_area)
	set_process(false)
	_pop_in.call_deferred()


# ---- layout ---------------------------------------------------------------------------

## The blue diamond picture covers the whole screen (cropped, never stretched), softly vignetted.
func _build_background() -> void:
	var picture: TextureRect = TextureRect.new()
	picture.name = "Background"
	picture.texture = BACKGROUND
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(picture)
	var vignette: GradientTexture2D = GradientTexture2D.new()
	vignette.fill = GradientTexture2D.FILL_RADIAL
	vignette.fill_from = Vector2(0.5, 0.46)
	vignette.fill_to = Vector2(1.0, 1.0)
	var gradient: Gradient = Gradient.new()
	gradient.offsets = PackedFloat32Array([0.45, 1.0])
	gradient.colors = PackedColorArray([Color(0.0, 0.02, 0.1, 0.0), Color(0.0, 0.02, 0.1, 0.55)])
	vignette.gradient = gradient
	var shade: TextureRect = TextureRect.new()
	shade.name = "Vignette"
	shade.texture = vignette
	shade.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(shade)


var _margin: MarginContainer
var _home: VBoxContainer
var _page_layer: VBoxContainer
var _page_holder: Control


func _build_layout() -> void:
	_margin = MarginContainer.new()
	_margin.name = "Safe"
	_margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_margin)
	_apply_safe_area()
	_home = _build_home()
	_margin.add_child(_home)
	_page_layer = _build_page_layer()
	_margin.add_child(_page_layer)


## The home page, centred: the mode carousel, the chosen mode's details and actions, then the
## section of page buttons.
func _build_home() -> VBoxContainer:
	var home: VBoxContainer = VBoxContainer.new()
	home.name = "Home"
	home.add_theme_constant_override("separation", 6)
	home.mouse_filter = Control.MOUSE_FILTER_IGNORE
	home.add_child(_build_top_row())
	# the cards fill the space between the top row and the action buttons, centred in it
	carousel = MenuCarousel.new()
	carousel.name = "ModeCarousel"
	carousel.custom_minimum_size = Vector2(0.0, ChoiceCard.CARD_SIZE.y)
	carousel.size_flags_horizontal = Control.SIZE_FILL
	carousel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	home.add_child(carousel)
	carousel.setup("", MODES, Session.menu_mode)
	home.add_child(_build_details())
	# a little room under the buttons: low on the screen, but not at the very bottom
	var lift: Control = Control.new()
	lift.custom_minimum_size = Vector2(0.0, ACTIONS_LIFT)
	lift.mouse_filter = Control.MOUSE_FILTER_IGNORE
	home.add_child(lift)
	return home


## The top row: the book at the left, the KALYEAH! logo in the middle, history and settings at the
## right, all on one line (the logo level with the buttons).
func _build_top_row() -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "TopRow"
	row.add_theme_constant_override("separation", 8)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var left: HBoxContainer = HBoxContainer.new()
	left.name = "Left"
	left.custom_minimum_size = Vector2(CORNER_BUTTON * 2.0 + 12.0, 0.0)
	left.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var right: HBoxContainer = HBoxContainer.new()
	right.name = "Right"
	right.custom_minimum_size = Vector2(CORNER_BUTTON * 2.0 + 12.0, 0.0)
	right.alignment = BoxContainer.ALIGNMENT_END
	right.add_theme_constant_override("separation", 12)
	right.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(left)
	logo = TextureRect.new()
	logo.name = "Logo"
	logo.texture = LOGO
	logo.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	logo.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	# it takes the width left between the corner buttons (never wider than LOGO_WIDTH) and keeps its shape
	logo.custom_minimum_size = Vector2(0.0, LOGO_WIDTH * float(LOGO.get_height()) / float(LOGO.get_width()))
	logo.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	logo.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	logo.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(logo)
	row.add_child(right)
	for corner: Dictionary in CORNERS:
		var id: StringName = corner["id"]
		var button: Button = MenuDialog.make_button("", "blue", 20, Vector2(CORNER_BUTTON, CORNER_BUTTON))
		button.name = "%sButton" % String(id).capitalize()
		button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		var centre: CenterContainer = CenterContainer.new()
		centre.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		centre.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var icon: NavIcon = NavIcon.new(corner["icon"] as StringName)
		icon.color = CREAM
		centre.add_child(icon)
		button.add_child(centre)
		button.pressed.connect(open_page.bind(id))
		(left if corner["side"] == &"left" else right).add_child(button)
		corner_buttons[id] = button
	# the row sits a little down from the top of the screen
	var holder: VBoxContainer = VBoxContainer.new()
	holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var drop: Control = Control.new()
	drop.custom_minimum_size = Vector2(0.0, TOP_ROW_DROP)
	drop.mouse_filter = Control.MOUSE_FILTER_IGNORE
	holder.add_child(drop)
	holder.add_child(row)
	return holder


func _build_details() -> Control:
	var details: VBoxContainer = VBoxContainer.new()
	details.name = "Details"
	details.add_theme_constant_override("separation", 8)
	details.mouse_filter = Control.MOUSE_FILTER_IGNORE
	mode_name_label = Label.new()
	mode_name_label.name = "ModeName"
	mode_name_label.theme_type_variation = &"DisplayLabel"
	mode_name_label.add_theme_font_size_override("font_size", 40)
	mode_name_label.add_theme_color_override("font_color", CREAM)
	mode_name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	details.add_child(mode_name_label)
	mode_text_label = Label.new()
	mode_text_label.name = "ModeText"
	mode_text_label.add_theme_font_size_override("font_size", 22)
	mode_text_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	mode_text_label.clip_text = true
	mode_text_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	details.add_child(mode_text_label)
	var actions: HBoxContainer = HBoxContainer.new()
	actions.name = "Actions"
	actions.add_theme_constant_override("separation", 14)
	actions.mouse_filter = Control.MOUSE_FILTER_IGNORE
	details.add_child(actions)
	primary_button = _action_button(actions, "PrimaryButton", "", "blue")
	secondary_button = _action_button(actions, "SecondaryButton", "JOIN", "green")
	return details


## The pages open here, over the home page, under a Back button.
func _build_page_layer() -> VBoxContainer:
	var layer: VBoxContainer = VBoxContainer.new()
	layer.name = "PageLayer"
	layer.visible = false
	layer.add_theme_constant_override("separation", 12)
	layer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	back_button = MenuDialog.make_button("< BACK", "grey", 26, Vector2(0.0, 76.0))
	back_button.name = "BackButton"
	back_button.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	back_button.custom_minimum_size = Vector2(190.0, 76.0)
	back_button.pressed.connect(close_page)
	layer.add_child(back_button)
	_page_holder = Control.new()
	_page_holder.name = "Pages"
	_page_holder.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_page_holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
	layer.add_child(_page_holder)
	codex_page = CodexPage.new()
	history_page = HistoryPage.new()
	settings_page = SettingsPage.new()
	pages = {&"codex": codex_page, &"history": history_page, &"settings": settings_page}
	for id: StringName in pages:
		var page: Control = pages[id]
		page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		page.visible = false
		_page_holder.add_child(page)
	settings_page.profile_pressed.connect(open_name_editor)
	settings_page.quit_pressed.connect(open_quit_confirm)
	# the book's two tabs are built when it enters the tree: expose them once it has
	codex_page.ready.connect(func() -> void:
		characters_page = codex_page.characters_page
		weapons_page = codex_page.weapons_page)
	return layer


## Opens one of the pages (codex = the book, history, settings) over the home page.
func open_page(id: StringName) -> void:
	if not pages.has(id):
		return
	current_page = id
	for other: StringName in pages:
		pages[other].visible = other == id
	if id == &"history":
		history_page.refresh()
	elif id == &"codex":
		codex_page.show_tab(codex_page.current)
	_home.visible = false
	_page_layer.visible = true


## Back to the home page.
func close_page() -> void:
	current_page = &""
	_page_layer.visible = false
	_home.visible = true


func _action_button(parent: Control, button_name: String, text: String, color: String) -> Button:
	var button: Button = MenuDialog.make_button(text, color, 32, Vector2(0.0, 88.0))
	button.name = button_name
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	parent.add_child(button)
	return button


## The chosen mode's name, one line of text and buttons: Hotspot and Online create or join a room
## (side by side), Practice just starts.
func _refresh_mode() -> void:
	var mode: Dictionary = MODES[Session.menu_mode]
	var rooms: bool = String(mode["id"]) != "practice"
	mode_name_label.text = String(mode["title"])
	mode_text_label.text = String(mode["text"])
	primary_button.text = "CREATE" if rooms else "START"
	secondary_button.visible = rooms
	KalyeahTheme.style_button(primary_button, "blue" if rooms else "orange")


func _on_mode_selected(index: int) -> void:
	Session.menu_mode = index
	_refresh_mode()


func _on_primary_pressed() -> void:
	match String(MODES[Session.menu_mode]["id"]):
		"practice":
			_on_practice_pressed()
		"online":
			_open_lobby(LobbyScreen.Intent.CREATE, false)
		_:
			_open_lobby(LobbyScreen.Intent.CREATE, true)


func _on_secondary_pressed() -> void:
	_open_lobby(LobbyScreen.Intent.JOIN, String(MODES[Session.menu_mode]["id"]) == "hotspot")


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

## The buttons pop up in turn when the screen opens.
func _pop_in() -> void:
	var buttons: Array[Control] = [primary_button, secondary_button]
	for id: StringName in corner_buttons:
		buttons.append(corner_buttons[id])
	for i: int in buttons.size():
		var button: Control = buttons[i]
		button.pivot_offset = button.size / 2.0
		button.scale = Vector2.ONE * 0.85
		button.modulate.a = 0.0
		var pop: Tween = create_tween().set_parallel(true)
		pop.tween_property(button, "modulate:a", 1.0, 0.25).set_delay(0.1 * i)
		pop.tween_property(button, "scale", Vector2.ONE, 0.35).set_delay(0.1 * i).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)


# ---- actions ----------------------------------------------------------------------------

func _on_practice_pressed() -> void:
	if _loading:
		return
	_loading = true
	_show_loading("GETTING THE STREET READY")
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
	dialog.open(self)


## Rename: a text box (12 letters at most) with Cancel / Save.
func open_name_editor() -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("YOUR NAME")
	var field: LineEdit = LineEdit.new()
	field.name = "NameField"
	field.text = Session.player_name
	field.placeholder_text = "Your name"
	field.max_length = Session.MAX_NAME_LENGTH
	field.alignment = HORIZONTAL_ALIGNMENT_CENTER
	field.custom_minimum_size = Vector2(0.0, 84.0)
	field.add_theme_font_size_override("font_size", 36)
	card.body.add_child(field)
	var hint: Label = card.add_hint("%d characters max." % Session.MAX_NAME_LENGTH)
	var cancel: Button = MenuDialog.make_button("CANCEL", "grey", 30)
	var save: Button = MenuDialog.make_button("SAVE", "green", 30)
	card.add_buttons([cancel, save])
	var apply: Callable = func() -> void:
		var clean: String = Session.clean_name(field.text)
		if clean.is_empty():
			hint.text = "Your name can't be empty."
			hint.add_theme_color_override("font_color", Color(1.0, 0.55, 0.5))
			return
		Session.player_name = clean
		Session.save()
		card.close()
	save.pressed.connect(apply)
	field.text_submitted.connect(func(_text: String) -> void: apply.call())
	cancel.pressed.connect(card.close)
	_show_dialog(card)
	field.grab_focus.call_deferred()
	return card


## Quit: asks first (a stray tap must not close the game).
func open_quit_confirm() -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("QUIT GAME?", 560.0)
	var stay: Button = MenuDialog.make_button("CANCEL", "grey", 32)
	var leave: Button = MenuDialog.make_button("QUIT", "red", 32)
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
