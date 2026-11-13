extends GutTest
## Full-game 1280x720 widescreen landscape orientation & responsive aspect scaling:
## the project runs natively in 1280x720 landscape (`always_landscape = true`) while keeping
## explicit aspect-fitting helpers for both landscape and legacy portrait test fixtures.


func test_the_app_starts_and_stays_in_landscape_turning_only_upside_down() -> void:
	# owner, 2026-11-13: landscape only (the phone may flip it 180 degrees, never upright), from the very first frame, so the main menu is
	# never laid out in a portrait window and cropped
	var orient: int = int(ProjectSettings.get_setting("display/window/handheld/orientation"))
	assert_eq(orient, DisplayServer.SCREEN_SENSOR_LANDSCAPE, "Android's sensorLandscape: landscape and reverse landscape only")
	assert_eq(Settings.ORIENTATION, DisplayServer.SCREEN_SENSOR_LANDSCAPE, "and the only one the game asks for at run time")
	assert_false(Settings.has_method("lock_portrait"), "nothing can ask for portrait any more")
	assert_true(Settings.always_landscape, "full-game 1280x720 widescreen landscape at runtime")
	assert_eq(int(ProjectSettings.get_setting("display/window/size/viewport_width")), 1280)
	assert_eq(int(ProjectSettings.get_setting("display/window/size/viewport_height")), 720)


func test_a_wide_window_keeps_the_vertical_shape_and_a_tall_one_fills_it() -> void:
	assert_eq(Settings.aspect_for(Vector2i(1280, 800)), Window.CONTENT_SCALE_ASPECT_KEEP, "portrait mode on a wide screen keeps aspect")
	assert_eq(Settings.aspect_for(Vector2i(1920, 1200)), Window.CONTENT_SCALE_ASPECT_KEEP)
	assert_eq(Settings.aspect_for(Vector2i(720, 1280)), Window.CONTENT_SCALE_ASPECT_EXPAND, "portrait mode on a tall screen expands")
	assert_eq(Settings.aspect_for(Vector2i(1200, 1920)), Window.CONTENT_SCALE_ASPECT_EXPAND)
	assert_eq(Settings.aspect_for(Vector2i(1000, 1000)), Window.CONTENT_SCALE_ASPECT_EXPAND)


func test_fitting_the_window_changes_only_its_scale_aspect_and_only_when_asked_off_a_phone() -> void:
	var window: Window = Settings.get_window()
	var saved_aspect: Window.ContentScaleAspect = window.content_scale_aspect
	var saved_size: Vector2i = window.size
	var saved_always: bool = Settings.always_landscape
	Settings.always_landscape = false
	Settings.set_landscape(false)
	window.content_scale_aspect = Window.CONTENT_SCALE_ASPECT_EXPAND
	window.size = Vector2i(1280, 800)
	Settings.fit_portrait()
	assert_eq(window.content_scale_aspect, Window.CONTENT_SCALE_ASPECT_EXPAND, "a desktop window is left alone unless forced")
	Settings.fit_portrait(true)
	assert_eq(window.content_scale_aspect, Window.CONTENT_SCALE_ASPECT_KEEP)
	window.size = Vector2i(720, 1280)
	Settings.fit_portrait(true)
	assert_eq(window.content_scale_aspect, Window.CONTENT_SCALE_ASPECT_EXPAND)
	Settings.always_landscape = saved_always
	Settings.set_landscape(true)
	window.size = saved_size
	window.content_scale_aspect = saved_aspect


func test_in_a_match_a_tall_window_keeps_the_wide_shape_and_a_wide_one_fills_it() -> void:
	assert_eq(Settings.aspect_for(Vector2i(2400, 1080), true), Window.CONTENT_SCALE_ASPECT_EXPAND, "a phone on its side")
	assert_eq(Settings.aspect_for(Vector2i(1280, 800), true), Window.CONTENT_SCALE_ASPECT_EXPAND, "a tablet on its side")
	assert_eq(Settings.aspect_for(Vector2i(1080, 2400), true), Window.CONTENT_SCALE_ASPECT_KEEP, "a tall window keeps wide shape")


func test_a_match_lays_the_ui_out_on_the_landscape_base_and_leaving_puts_the_portrait_base_back() -> void:
	var window: Window = Settings.get_window()
	var saved_always: bool = Settings.always_landscape
	Settings.set_landscape(true)
	assert_true(Settings.landscape)
	assert_eq(window.content_scale_size, Settings.LANDSCAPE_BASE)
	assert_eq(Settings.LANDSCAPE_BASE, Vector2i(int(ProjectSettings.get_setting("display/window/size/viewport_width")), int(ProjectSettings.get_setting("display/window/size/viewport_height"))), "1280x720 landscape base matches project settings")
	# When always_landscape is active, leaving a match stays in 1280x720 widescreen
	Settings.set_landscape(false)
	assert_true(Settings.landscape)
	assert_eq(window.content_scale_size, Settings.LANDSCAPE_BASE)
	# When always_landscape is disabled (portrait fixture mode), set_landscape(false) restores PORTRAIT_BASE
	Settings.always_landscape = false
	Settings.set_landscape(false)
	assert_false(Settings.landscape)
	assert_eq(window.content_scale_size, Settings.PORTRAIT_BASE)
	Settings.always_landscape = saved_always
	Settings.set_landscape(true)
