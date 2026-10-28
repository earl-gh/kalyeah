extends GutTest

const BOOT_SCENE: PackedScene = preload("res://scenes/ui/boot_screen.tscn")


func _boot() -> BootScreen:
	var root: Control = autofree(Control.new()) as Control
	root.size = Vector2(720.0, 1280.0)
	add_child(root)
	var boot: BootScreen = BOOT_SCENE.instantiate() as BootScreen
	root.add_child(boot)
	boot.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	boot.set_process(false)
	return boot


func test_the_fill_runs_from_the_left_end_to_the_right_end() -> void:
	assert_almost_eq(BootScreen.fill_width(0.0, 1000.0), 0.0, 0.01)
	assert_almost_eq(BootScreen.fill_width(1.0, 1000.0), 1000.0, 0.01)
	assert_gt(BootScreen.fill_width(0.6, 1000.0), BootScreen.fill_width(0.3, 1000.0))


func test_a_matte_black_layer_is_underneath_with_a_matte_black_base_and_no_white_bar() -> void:
	var boot: BootScreen = _boot()
	boot._layout()
	var black: ColorRect = boot.find_child("Black", false, false) as ColorRect
	assert_eq(black.color, BootScreen.BASE_COLOR, "matte black underneath everything")
	assert_ne(black.color, Color.BLACK, "not pure black")
	assert_lt(black.color.v, 0.15, "still very dark")
	assert_lt(black.get_index(), boot._art.get_index(), "under the art")
	assert_lt(black.get_index(), boot._base.get_index(), "and under the base")
	var band_top: float = 1280.0 - boot.band_height()
	assert_almost_eq(boot._art.position.y + boot._art.size.y, band_top, 0.5, "the art stops where the base starts")
	assert_eq(boot._base.color, BootScreen.BASE_COLOR, "the base is matte black")
	assert_almost_eq(boot._base.get_global_rect().size.x, 720.0, 0.5, "edge to edge")
	assert_null(boot.get("_track_rect"), "no white bar")
	for texture: Node in boot.find_children("*", "TextureRect", true, false):
		assert_ne((texture as TextureRect).texture, load("res://assets/ui/boot/boot_bar_track.png"), "the white track is not drawn")
	boot.queue_free()


func test_the_base_is_short_and_the_cardboard_keeps_its_proportions_at_the_bases_scale_and_the_base_crops_it() -> void:
	var boot: BootScreen = _boot()
	boot.progress = 1.0
	boot._layout()
	var scale_now: float = boot._fill_rect.size.y / BootScreen.FILL_REGION.size.y
	assert_almost_eq(boot._fill_rect.size.x / BootScreen.FILL_REGION.size.x, scale_now, 0.001, "no stretching: the same scale both ways")
	assert_gte(boot._fill_rect.size.y, boot._base.size.y - 0.5, "at least as tall as the base (it covers it)")
	assert_lt(boot._base.size.y, 80.0, "a short base")
	assert_gte(boot._fill_rect.size.x, boot._base.size.x, "wider than the base, so the base crops it")
	assert_eq(boot._fill_clip.size, boot._base.size, "full at 100%")
	boot.progress = 0.25
	boot._apply_progress()
	assert_almost_eq(boot._fill_clip.size.x, 180.0, 0.5)
	assert_eq(boot._fill_clip.size.y, boot._base.size.y)
	var picture: Texture2D = load("res://assets/ui/boot/boot_bar_fill.png") as Texture2D
	assert_gt(BootScreen.FILL_REGION.position.x, 25.0, "the rounded left end is cropped away")
	assert_lt(BootScreen.FILL_REGION.end.x, float(picture.get_width()) - 25.0, "and the right end")
	boot.queue_free()


func test_the_percentage_is_clash_text_in_the_middle_of_the_base_instead_of_the_k() -> void:
	var boot: BootScreen = _boot()
	boot.progress = 0.5
	boot._layout()
	assert_eq(boot._percent.text, "50%")
	assert_eq(boot._percent.get_theme_font("font"), BootScreen.PERCENT_FONT, "the Clash font")
	assert_almost_eq(boot._percent.get_global_rect().get_center().x, 360.0, 1.0, "centred left to right")
	assert_almost_eq(boot._percent.get_global_rect().get_center().y, boot._base.get_global_rect().position.y, 1.0, "vertically centred on the base's top edge")
	assert_lt(boot._percent.get_global_rect().position.y, boot._base.get_global_rect().position.y, "it overlaps the base")
	assert_gt(boot._percent.get_global_rect().end.y, boot._base.get_global_rect().position.y)
	boot.progress = 1.0
	boot._apply_progress()
	assert_eq(boot._percent.text, "100%")
	assert_null(boot.get("_icon_rect"), "no K! riding the bar")
	boot.queue_free()


func test_a_tip_floats_just_above_the_base_and_fits_the_screen() -> void:
	var boot: BootScreen = _boot()
	boot._layout()
	assert_true(BootScreen.TIPS.has(boot._tip.text), "one of the tips")
	var tip: Rect2 = boot._tip.get_global_rect()
	assert_lte(tip.end.y, boot._percent.get_global_rect().position.y + 1.0, "above the base and its percentage")
	assert_gte(tip.position.x, 0.0)
	assert_lte(tip.end.x, 720.0)
	assert_eq(boot._tip.horizontal_alignment, HORIZONTAL_ALIGNMENT_CENTER)
	assert_eq(boot._tip.get_theme_font("font"), KalyeahTheme.BODY_FONT, "the tip is in the body font")
	for line: String in BootScreen.TIPS:
		assert_ne(line, line.to_upper(), "sentence case, not all capitals")
		assert_eq(line[0], line[0].to_upper(), "starts with a capital")
	for line: String in BootScreen.TIPS:
		assert_lt(line.length(), 80, "short enough for two lines")
	boot.queue_free()


func test_every_scene_it_warms_up_exists() -> void:
	for path: String in BootScreen.WARM_SCENES:
		assert_true(ResourceLoader.exists(path), path)


# ---- the loading screen into a match ------------------------------------------------------

func _over_a_screen(path: String) -> BootScreen:
	var root: Control = autofree(Control.new()) as Control
	root.size = Vector2(720.0, 1280.0)
	add_child(root)
	var loading: BootScreen = BootScreen.for_scene(path)
	root.add_child(loading)
	return loading


func _until_finished(loading: BootScreen) -> bool:
	watch_signals(loading)
	for i: int in 600:
		if get_signal_emit_count(loading, "finished") > 0:
			return true
		await wait_process_frames(1)
	return get_signal_emit_count(loading, "finished") > 0


func test_the_loading_screen_covers_the_screen_loads_one_scene_and_leaves_the_change_to_its_caller() -> void:
	var path: String = "res://scenes/ui/lobby.tscn"
	var loading: BootScreen = _over_a_screen(path)
	await wait_process_frames(2)
	assert_eq(loading.size, Vector2(720.0, 1280.0), "the whole screen")
	assert_eq(loading.mouse_filter, Control.MOUSE_FILTER_STOP, "it takes every touch: nothing in the room under it can be pressed")
	assert_eq(loading.z_index, MenuDialog.MODAL_Z, "over the room's own sheets")
	assert_eq(loading.scenes, [path] as Array[String])
	assert_false(loading.hands_over)
	assert_true(await _until_finished(loading), "it finishes")
	assert_almost_eq(loading.progress, 1.0, 0.001, "with the bar full")
	var packed: PackedScene = loading.scene()
	assert_not_null(packed)
	assert_eq(packed.resource_path, path)


func test_the_room_shows_the_loading_screen_when_the_match_starts_and_back_waits() -> void:
	var lobby: LobbyScreen = autofree(preload("res://scenes/ui/lobby.tscn").instantiate()) as LobbyScreen
	lobby.client = GameClient.new(preload("res://data/rules/game_rules.tres"), preload("res://data/rules/map_layout.tres"), preload("res://data/rules/net_rules.tres"))
	lobby.set_intent(LobbyScreen.Intent.CREATE)
	add_child(lobby)
	await wait_process_frames(1)
	lobby._on_match_loading({})
	assert_not_null(lobby.loading_screen, "the loading screen, not a frozen room")
	lobby.loading_screen.set_process(false)  # stays here: this test does not open the match
	assert_eq(lobby.loading_screen.target, LobbyScreen.MATCH_SCENE)
	assert_true(lobby.loading_screen.is_visible_in_tree())
	var shown: BootScreen = lobby.loading_screen
	lobby._on_match_loading({})
	assert_eq(lobby.loading_screen, shown, "one loading screen, however many times the server says so")
	watch_signals(lobby)
	lobby._back_pressed()
	assert_true(is_instance_valid(lobby) and lobby.is_inside_tree(), "Back waits for the match")
	assert_eq(lobby.client.room, {}, "and leaves no room")
