extends GutTest
## What makes the project an Android app (docs/ROADMAP.md "M6"): the export preset, the project settings the phone depends on,
## and the back button. The built APK itself is checked by .github/workflows/android.yml.


func _presets() -> Dictionary[String, Dictionary]:
	var cfg: ConfigFile = ConfigFile.new()
	assert_eq(cfg.load("res://export_presets.cfg"), OK)
	var found: Dictionary[String, Dictionary] = {}
	for section: String in cfg.get_sections():
		if not section.ends_with(".options") and cfg.has_section_key(section, "name"):
			var options: Dictionary = {}
			for key: String in cfg.get_section_keys(section + ".options"):
				options[key] = cfg.get_value(section + ".options", key)
			found[String(cfg.get_value(section, "name"))] = options
	return found


func _exclude_patterns(preset_name: String) -> PackedStringArray:
	var cfg: ConfigFile = ConfigFile.new()
	assert_eq(cfg.load("res://export_presets.cfg"), OK)
	for section: String in cfg.get_sections():
		if not section.ends_with(".options") and cfg.get_value(section, "name", "") == preset_name:
			var patterns: PackedStringArray = []
			for pattern: String in String(cfg.get_value(section, "exclude_filter", "")).split(","):
				patterns.append(pattern.strip_edges())
			return patterns
	return []


func _is_excluded(path: String, patterns: PackedStringArray) -> bool:
	for pattern: String in patterns:
		if not pattern.is_empty() and path.trim_prefix("res://").match(pattern):
			return true
	return false


## Every file the export would pack under res://, as the preset's exclude_filter decides.
func _packed_files(preset_name: String, dir: String = "res://") -> PackedStringArray:
	var patterns: PackedStringArray = _exclude_patterns(preset_name)
	var found: PackedStringArray = []
	for file: String in DirAccess.get_files_at(dir):
		if not _is_excluded(dir.path_join(file), patterns):
			found.append(dir.path_join(file))
	for sub: String in DirAccess.get_directories_at(dir):
		if not sub.begins_with(".") and not _is_excluded(dir.path_join(sub) + "/", patterns):
			found.append_array(_packed_files(preset_name, dir.path_join(sub)))
	return found


func test_the_only_presets_are_the_android_app_and_the_headless_server() -> void:
	var names: Array = _presets().keys()
	names.sort()
	assert_eq(names, ["Android", "Server"])


func test_the_android_preset_is_one_portrait_arm64_app_with_only_the_permissions_it_uses() -> void:
	var android: Dictionary = _presets()["Android"]
	assert_eq(android["package/unique_name"], "com.kalyeah.game")
	assert_true(android["architectures/arm64-v8a"])
	for other: String in ["armeabi-v7a", "x86", "x86_64"]:
		assert_false(android["architectures/%s" % other], other)
	assert_true(android["screen/immersive_mode"])
	assert_true(android["permissions/internet"], "Online and Hotspot open sockets")
	assert_true(android["permissions/vibrate"], "Input.vibrate_handheld does nothing without it")
	for key: String in android:
		if key.begins_with("permissions/") and not key in ["permissions/internet", "permissions/vibrate"]:
			assert_false(android[key], "%s is not needed (the hotspot only opens sockets and lists addresses)" % key)
	assert_false(android.has("screen/orientation"), "not an option in Godot 4: the project setting below decides")
	assert_eq(ProjectSettings.get_setting("display/window/handheld/orientation"), DisplayServer.SCREEN_PORTRAIT)


func test_every_launcher_icon_the_preset_names_exists() -> void:
	var android: Dictionary = _presets()["Android"]
	for key: String in android:
		if key.begins_with("launcher_icons/") and not String(android[key]).is_empty():
			assert_true(ResourceLoader.exists(android[key]), "%s is %s" % [key, android[key]])


func test_the_phone_gives_controls_their_touches_as_clicks_and_the_back_button_does_not_quit() -> void:
	assert_true(Input.is_emulating_mouse_from_touch(), "buttons, scrolls and carousels are driven by the clicks a touch makes")
	assert_false(ProjectSettings.get_setting("application/config/quit_on_go_back"), "each screen decides what back does")


func test_back_on_the_home_screen_asks_to_quit_instead_of_quitting() -> void:
	var screen: TitleScreen = autofree((load("res://scenes/ui/title_screen.tscn") as PackedScene).instantiate()) as TitleScreen
	add_child(screen)
	get_tree().root.propagate_notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_process_frames(2)
	var question: MenuDialog = screen.dialog
	assert_not_null(question)
	assert_true(question.is_open, "Quit game?")
	get_tree().root.propagate_notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_process_frames(2)
	assert_false(is_instance_valid(question) and question.is_open, "back again closes the question")
	assert_true(screen.is_inside_tree(), "and the app is still here")


func test_back_closes_an_open_page_and_then_the_dialog_on_top_of_it_first() -> void:
	var screen: TitleScreen = autofree((load("res://scenes/ui/title_screen.tscn") as PackedScene).instantiate()) as TitleScreen
	add_child(screen)
	screen.open_page(&"settings")
	var question: MenuDialog = screen.open_quit_confirm()
	get_tree().root.propagate_notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_process_frames(2)
	assert_false(is_instance_valid(question) and question.is_open, "the dialog goes first")
	assert_eq(screen.current_page, &"settings", "the page stays under it")
	get_tree().root.propagate_notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_process_frames(2)
	assert_eq(screen.current_page, &"", "then the page closes")


func test_back_in_a_match_opens_its_settings_and_a_second_back_closes_them() -> void:
	var practice: PracticeMatch = autofree((load("res://scenes/match/practice.tscn") as PackedScene).instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	practice.skip_intro()
	get_tree().root.propagate_notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_process_frames(2)
	assert_true(practice.hud.menu.visible, "Resume or Leave, never a silent quit")
	get_tree().root.propagate_notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_process_frames(2)
	assert_false(practice.hud.menu.visible)


func test_back_during_the_opening_pick_is_the_pick_screens_back_not_the_match_settings() -> void:
	var practice: PracticeMatch = autofree((load("res://scenes/match/practice.tscn") as PackedScene).instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	assert_true(practice.is_picking())
	var pick: WeaponPickScreen = practice.get_node("%PickScreen") as WeaponPickScreen
	pick.exit_requested.disconnect(practice._on_exit_pressed)  # it would leave for the title screen
	watch_signals(pick)
	get_tree().root.propagate_notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_process_frames(2)
	assert_signal_emitted(pick, "exit_requested", "Practice's pick leaves at once, like its Back button")
	assert_false(practice.hud.menu.visible, "the pick screen has the press")


func test_back_in_the_lobby_closes_its_dialog_first_and_does_not_leave() -> void:
	Session.lobby_hotspot = false
	Session.lobby_intent = 1
	var lobby: LobbyScreen = autofree((load("res://scenes/ui/lobby.tscn") as PackedScene).instantiate()) as LobbyScreen
	add_child(lobby)
	await wait_process_frames(2)
	lobby._open_settings()
	var dialogs: Array[Node] = lobby.find_children("*", "MenuDialog", true, false)
	assert_eq(dialogs.size(), 1)
	var settings: MenuDialog = dialogs[0] as MenuDialog
	assert_true(settings.is_open)
	get_tree().root.propagate_notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_process_frames(2)
	assert_false(is_instance_valid(settings) and settings.is_open, "the dialog closed")
	assert_true(lobby.is_inside_tree(), "the lobby is still there")


func test_the_export_leaves_out_the_test_framework_the_tools_and_the_docs() -> void:
	for preset_name: String in ["Android", "Server"]:
		var patterns: PackedStringArray = _exclude_patterns(preset_name)
		for left_out: String in ["addons/*", "tests/*", "tools/*", "docs/*"]:
			assert_true(left_out in patterns, "%s leaves out %s" % [preset_name, left_out])
		# GUT's stub scene gives a node a script that is no script: the export logged "Cannot set object script" for it.
		assert_true(_is_excluded("res://addons/gut/gut_loader_the_scene.tscn", patterns), preset_name)
		assert_true(_is_excluded("res://tools/art/src/skill_art_17.webp", patterns), "%s: 9 MB of source art" % preset_name)
		assert_false(_is_excluded("res://server/main.tscn", patterns), "the server scene is the Server preset's main")
		assert_false(_is_excluded("res://scenes/match/practice.tscn", patterns), preset_name)


func test_every_scene_and_resource_the_android_export_packs_loads_and_instantiates() -> void:
	var checked: PackedStringArray = []
	for path: String in _packed_files("Android"):
		if path.get_extension() not in ["tscn", "scn", "tres", "res", "glb"]:
			continue
		var loaded: Resource = load(path)
		assert_not_null(loaded, path)
		if loaded is PackedScene:
			var instance: Node = (loaded as PackedScene).instantiate()
			assert_not_null(instance, path)
			if instance != null:
				instance.free()
		checked.append(path)
	for expected: String in ["res://scenes/match/practice.tscn", "res://data/rules/game_rules.tres", "res://assets/models/props/post_blue.glb"]:
		assert_true(expected in checked, "%s is among what was checked" % expected)


func test_every_asset_a_script_scene_or_data_file_names_exists() -> void:
	var pattern: RegEx = RegEx.create_from_string("res://assets/[A-Za-z0-9_/.\\-]+\\.[a-z0-9]+")
	var named: Dictionary[String, String] = {}
	for path: String in _packed_files("Android"):
		if path.get_extension() not in ["gd", "tscn", "tres"]:
			continue
		for found: RegExMatch in pattern.search_all(FileAccess.get_file_as_string(path)):
			named[found.get_string()] = path
	assert_true(named.has("res://assets/ui/main_bg.webp"), "the scan finds the assets the code names")
	for asset: String in named:
		assert_true(ResourceLoader.exists(asset), "%s (named in %s) is missing" % [asset, named[asset]])
