class_name TitleScreen
extends Control
## The main screen. A blue diamond background; your profile (face and name, tap to rename) and the
## settings button on top; the KALYEAH wordmark; the map model (the play area alone, standing
## still); then four buttons in a 2x2 grid at the bottom: PRACTICE (offline), HOTSPOT (a game on
## a phone hotspot), ONLINE (Create / Join room on the game server) and QUIT. Settings, rename,
## hotspot, online and quit open as dialogs. A dedicated server build goes straight to the server.

const SERVER_SCENE: String = "res://server/main.tscn"
const LOBBY_SCENE: String = "res://scenes/ui/lobby.tscn"
const PRACTICE_SCENE: String = "res://scenes/match/practice.tscn"
const BACKGROUND: Texture2D = preload("res://assets/ui/main_bg.webp")
const GEAR: Texture2D = preload("res://assets/icons/gear.png")
const CROP_SHADER: Shader = preload("res://shaders/minimap_crop.gdshader")

const SIDE_MARGIN: float = 34.0
const TOP_MARGIN: float = 26.0
const BOTTOM_MARGIN: float = 22.0
const TAP_TARGET: float = 88.0
const AVATAR: float = 76.0
const GOLD: Color = Color(1.0, 0.84, 0.2)
const WORDMARK_OUTLINE: Color = Color(0.2, 0.1, 0.07)
const CREAM: Color = Color(1.0, 0.95, 0.82)

## The screen asks for the game to close (replaced in tests).
var quit_action: Callable = _quit_game
## The screen leaves for the lobby (replaced in tests).
var open_lobby_action: Callable = _change_to_lobby

var profile_button: Button
var settings_button: Button
var practice_button: Button
var hotspot_button: Button
var online_button: Button
var quit_button: Button
var diorama: MenuDiorama
var dialog: MenuDialog

var _name_label: Label
var _avatar_texture: TextureRect
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
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_build_background()
	_build_layout()
	_refresh_profile()
	profile_button.pressed.connect(open_name_editor)
	settings_button.pressed.connect(open_settings)
	practice_button.pressed.connect(_on_practice_pressed)
	hotspot_button.pressed.connect(open_hotspot)
	online_button.pressed.connect(open_online)
	quit_button.pressed.connect(open_quit_confirm)
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


func _build_layout() -> void:
	_margin = MarginContainer.new()
	_margin.name = "Safe"
	_margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_margin)
	_apply_safe_area()
	var column: VBoxContainer = VBoxContainer.new()
	column.name = "Column"
	column.add_theme_constant_override("separation", 14)
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_margin.add_child(column)
	column.add_child(_build_top_bar())
	column.add_child(_build_wordmark())
	column.add_child(_build_map_model())
	column.add_child(_build_buttons())


## The map model, filling the space between the wordmark and the buttons (nothing moves).
func _build_map_model() -> Control:
	diorama = MenuDiorama.new()
	diorama.name = "MapModel"
	diorama.size_flags_vertical = Control.SIZE_EXPAND_FILL
	diorama.custom_minimum_size = Vector2(0.0, 300.0)
	return diorama


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
	return Hig.safe_insets(get_viewport())


func _build_top_bar() -> Control:
	var bar: HBoxContainer = HBoxContainer.new()
	bar.name = "TopBar"
	bar.add_theme_constant_override("separation", 14)
	bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	profile_button = Button.new()
	profile_button.name = "Profile"
	profile_button.custom_minimum_size = Vector2(0.0, TAP_TARGET + 8.0)
	profile_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	profile_button.clip_text = true
	var pill: StyleBoxTexture = KalyeahTheme.painted_pill()
	for state: String in ["normal", "hover", "pressed", "hover_pressed", "focus"]:
		profile_button.add_theme_stylebox_override(state, pill)
	MenuDialog.feel_press(profile_button)
	bar.add_child(profile_button)
	var inner: HBoxContainer = HBoxContainer.new()
	inner.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	inner.offset_left = 14.0
	inner.offset_right = -14.0
	inner.add_theme_constant_override("separation", 14)
	inner.mouse_filter = Control.MOUSE_FILTER_IGNORE
	profile_button.add_child(inner)
	inner.add_child(_build_avatar())
	var texts: VBoxContainer = VBoxContainer.new()
	texts.alignment = BoxContainer.ALIGNMENT_CENTER
	texts.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	texts.add_theme_constant_override("separation", -2)
	texts.mouse_filter = Control.MOUSE_FILTER_IGNORE
	inner.add_child(texts)
	_name_label = Label.new()
	_name_label.add_theme_font_size_override("font_size", 34)
	_name_label.add_theme_color_override("font_color", Color.WHITE)
	_name_label.clip_text = true
	_name_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	_name_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	texts.add_child(_name_label)
	var hint: Label = Label.new()
	hint.text = "Tap to change your name"
	hint.add_theme_font_size_override("font_size", Hig.CAPTION)
	hint.add_theme_color_override("font_color", Color(0.8, 0.88, 1.0, 0.8))
	hint.add_theme_constant_override("outline_size", 0)
	hint.mouse_filter = Control.MOUSE_FILTER_IGNORE
	texts.add_child(hint)
	settings_button = DialogButtons.icon_button(GEAR, "blue", TAP_TARGET)
	settings_button.name = "SettingsButton"
	bar.add_child(settings_button)
	return bar


## The player's character face in a round gold ring.
func _build_avatar() -> Control:
	var holder: Control = Control.new()
	holder.custom_minimum_size = Vector2(AVATAR, AVATAR)
	holder.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var ring: Panel = Panel.new()
	ring.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var style: StyleBoxFlat = StyleBoxFlat.new()
	style.bg_color = Color(0.08, 0.1, 0.22)
	style.set_corner_radius_all(int(AVATAR))
	style.set_border_width_all(4)
	style.border_color = GOLD
	style.anti_aliasing = true
	ring.add_theme_stylebox_override("panel", style)
	ring.mouse_filter = Control.MOUSE_FILTER_IGNORE
	holder.add_child(ring)
	_avatar_texture = TextureRect.new()
	_avatar_texture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_avatar_texture.offset_left = 5.0
	_avatar_texture.offset_top = 5.0
	_avatar_texture.offset_right = -5.0
	_avatar_texture.offset_bottom = -5.0
	_avatar_texture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_avatar_texture.stretch_mode = TextureRect.STRETCH_SCALE
	_avatar_texture.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	_avatar_texture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var crop: ShaderMaterial = ShaderMaterial.new()
	crop.shader = CROP_SHADER
	crop.set_shader_parameter("rect_size", Vector2(AVATAR - 10.0, AVATAR - 10.0))
	crop.set_shader_parameter("radius", (AVATAR - 10.0) / 2.0)
	_avatar_texture.material = crop
	holder.add_child(_avatar_texture)
	return holder


## KALYEAH! as a cardboard-sign wordmark (KAL white, YEAH! gold) with a ribbon tagline.
func _build_wordmark() -> Control:
	var box: VBoxContainer = VBoxContainer.new()
	box.name = "Wordmark"
	box.add_theme_constant_override("separation", 4)
	box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var word: HBoxContainer = HBoxContainer.new()
	word.alignment = BoxContainer.ALIGNMENT_CENTER
	word.add_theme_constant_override("separation", 6)
	word.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.add_child(word)
	for part: Array in [["KAL", Color.WHITE], ["YEAH!", GOLD]]:
		var label: Label = Label.new()
		label.text = part[0] as String
		label.add_theme_font_size_override("font_size", 104)
		label.add_theme_color_override("font_color", part[1] as Color)
		label.add_theme_color_override("font_outline_color", WORDMARK_OUTLINE)
		label.add_theme_constant_override("outline_size", 24)
		label.add_theme_color_override("font_shadow_color", Color(0.0, 0.0, 0.05, 0.45))
		label.add_theme_constant_override("shadow_offset_y", 7)
		label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		word.add_child(label)
	var ribbon: CenterContainer = CenterContainer.new()
	ribbon.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var tagline: Label = Label.new()
	tagline.theme_type_variation = &"HudPill"
	tagline.text = "MGA LARONG PINOY SA KALYE"
	tagline.add_theme_font_size_override("font_size", Hig.FOOTNOTE)
	tagline.add_theme_color_override("font_color", CREAM)
	tagline.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ribbon.add_child(tagline)
	box.add_child(ribbon)
	return box


## The four buttons in a 2x2 grid at the bottom, all the same size.
func _build_buttons() -> Control:
	var grid: GridContainer = GridContainer.new()
	grid.name = "Buttons"
	grid.columns = 2
	grid.add_theme_constant_override("h_separation", 18)
	grid.add_theme_constant_override("v_separation", 18)
	grid.mouse_filter = Control.MOUSE_FILTER_IGNORE
	practice_button = _grid_button(grid, "PracticeButton", "PRACTICE", "orange")
	hotspot_button = _grid_button(grid, "HotspotButton", "HOTSPOT", "green")
	online_button = _grid_button(grid, "OnlineButton", "ONLINE", "blue")
	quit_button = _grid_button(grid, "QuitButton", "QUIT", "red")
	return grid


func _grid_button(grid: GridContainer, button_name: String, text: String, color: String) -> Button:
	var button: Button = MenuDialog.make_button(text, color, 42, Vector2(0.0, 124.0))
	button.name = button_name
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	grid.add_child(button)
	return button


# ---- what the screen shows ---------------------------------------------------------------

func _refresh_profile() -> void:
	_name_label.text = Session.player_name
	var face: Texture2D = KidModel.minimap_face(StringName(Session.avatar_id))
	_avatar_texture.texture = face if face != null else KidModel.portrait(StringName(Session.avatar_id))


## The buttons pop up in turn when the screen opens.
func _pop_in() -> void:
	var buttons: Array[Control] = [practice_button, hotspot_button, online_button, quit_button]
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
	text.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	text.add_theme_font_size_override("font_size", 40)
	text.add_theme_color_override("font_color", GOLD)
	text.add_theme_constant_override("outline_size", 12)
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
	var hint: Label = card.add_hint("Up to %d letters. Your friends see this name." % Session.MAX_NAME_LENGTH)
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
		_refresh_profile()
		card.close()
	save.pressed.connect(apply)
	field.text_submitted.connect(func(_text: String) -> void: apply.call())
	cancel.pressed.connect(card.close)
	_show_dialog(card)
	field.grab_focus.call_deferred()
	return card


## ONLINE: create a room or join one on the game server.
func open_online() -> MenuDialog:
	return _open_play_dialog("ONLINE", false, "Play with friends on the game server.")


## HOTSPOT: play with friends on the same phone hotspot or Wi-Fi, on a game server running there.
func open_hotspot() -> MenuDialog:
	return _open_play_dialog("HOTSPOT", true, "Turn on a hotspot, run the game server on it, and type that device's address.")


## The Create / Join choice. A hotspot also asks for the address of the server on the hotspot.
func _open_play_dialog(title: String, hotspot: bool, hint: String) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create(title)
	card.add_hint(hint)
	var address: LineEdit = null
	if hotspot:
		address = LineEdit.new()
		address.name = "AddressField"
		address.text = Settings.hotspot_url
		address.placeholder_text = "192.168.43.1:9080"
		address.custom_minimum_size = Vector2(0.0, Hig.MIN_TAP)
		address.add_theme_font_size_override("font_size", 28)
		card.body.add_child(address)
	var create: Button = MenuDialog.make_button("CREATE ROOM", "blue", 32)
	var join: Button = MenuDialog.make_button("JOIN ROOM", "green", 32)
	create.name = "CreateRoomButton"
	join.name = "JoinRoomButton"
	card.add_buttons([create, join])
	var cancel: Button = MenuDialog.make_button("CANCEL", "grey", 28)
	card.add_buttons([cancel])
	cancel.pressed.connect(card.close)
	var go: Callable = func(intent: LobbyScreen.Intent) -> void:
		if hotspot:
			Settings.save_hotspot_url(address.text)
		_open_lobby(intent, hotspot)
	create.pressed.connect(go.bind(LobbyScreen.Intent.CREATE))
	join.pressed.connect(go.bind(LobbyScreen.Intent.JOIN))
	_show_dialog(card)
	return card


## Settings: sound volume, vibration (phones) and the online server address.
func open_settings() -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("SETTINGS")
	var sound: HBoxContainer = _settings_row(card, "Sound")
	var slider: HSlider = DialogButtons.slider(Settings.volume)
	slider.name = "VolumeSlider"
	sound.add_child(slider)
	var percent: Label = Label.new()
	percent.custom_minimum_size = Vector2(84.0, 0.0)
	percent.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	percent.add_theme_font_size_override("font_size", 28)
	percent.text = "%d%%" % roundi(Settings.volume * 100.0)
	sound.add_child(percent)
	slider.value_changed.connect(func(value: float) -> void:
		Settings.set_volume(value)
		percent.text = "%d%%" % roundi(value * 100.0))
	slider.drag_ended.connect(func(_changed: bool) -> void: Settings.play_ui())
	if Settings.can_vibrate():
		var buzz: HBoxContainer = _settings_row(card, "Vibration")
		var toggle: Button = MenuDialog.make_button("ON" if Settings.vibration else "OFF", "green" if Settings.vibration else "grey", 28, Vector2(150.0, Hig.MIN_TAP))
		toggle.name = "VibrationToggle"
		toggle.pressed.connect(func() -> void:
			Settings.set_vibration(not Settings.vibration)
			toggle.text = "ON" if Settings.vibration else "OFF"
			KalyeahTheme.style_button(toggle, "green" if Settings.vibration else "grey"))
		buzz.add_child(toggle)
	card.add_label("Online server", 26, Color.WHITE, HORIZONTAL_ALIGNMENT_LEFT)
	var server: LineEdit = LineEdit.new()
	server.name = "ServerField"
	server.text = Settings.server_url
	server.placeholder_text = "wss://your-server"
	server.custom_minimum_size = Vector2(0.0, Hig.MIN_TAP)
	server.add_theme_font_size_override("font_size", 28)
	card.body.add_child(server)
	card.add_hint("Used by ONLINE. Leave it as it is unless you run your own server.")
	var done: Button = MenuDialog.make_button("DONE", "green", 34)
	card.add_buttons([done])
	done.pressed.connect(card.close)
	card.closed.connect(func() -> void:
		var typed: String = server.text.strip_edges()
		if typed != Settings.server_url:
			Settings.save_server_url(typed))
	_show_dialog(card)
	return card


func _settings_row(card: MenuDialog, label_text: String) -> HBoxContainer:
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", 14)
	var label: Label = Label.new()
	label.text = label_text
	label.custom_minimum_size = Vector2(170.0, 0.0)
	label.add_theme_font_size_override("font_size", 28)
	row.add_child(label)
	card.body.add_child(row)
	return row


## Quit: asks first (a stray tap must not close the game).
func open_quit_confirm() -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("LEAVE KALYEAH?", 560.0)
	card.add_label("Are you sure you want to quit?", 28)
	var stay: Button = MenuDialog.make_button("STAY", "green", 32)
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
		open_quit_confirm()


## The phone's back button: close what is open, or ask to quit.
func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_GO_BACK_REQUEST:
		if dialog != null and is_instance_valid(dialog) and dialog.is_open:
			dialog.close()
		else:
			open_quit_confirm()
