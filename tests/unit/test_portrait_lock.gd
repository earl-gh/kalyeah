extends GutTest
## The game stays vertical on tablets too (docs/GDD.md "Vertical layout"): the export asks for portrait (not sensor-portrait, so
## the device's auto-rotate cannot turn it), the game asks again at start, and a window that is wider than tall (Android 16 may
## ignore the request on a large screen) keeps the vertical shape with bars at its sides.


func test_the_project_asks_for_plain_portrait_not_a_sensor_that_follows_the_device() -> void:
	assert_eq(ProjectSettings.get_setting("display/window/handheld/orientation"), DisplayServer.SCREEN_PORTRAIT)
	assert_ne(ProjectSettings.get_setting("display/window/handheld/orientation"), DisplayServer.SCREEN_SENSOR_PORTRAIT)
	assert_ne(ProjectSettings.get_setting("display/window/handheld/orientation"), DisplayServer.SCREEN_SENSOR)


func test_a_wide_window_keeps_the_vertical_shape_and_a_tall_one_fills_it() -> void:
	assert_eq(Settings.aspect_for(Vector2i(1280, 800)), Window.CONTENT_SCALE_ASPECT_KEEP, "a tablet turned sideways")
	assert_eq(Settings.aspect_for(Vector2i(1920, 1200)), Window.CONTENT_SCALE_ASPECT_KEEP)
	assert_eq(Settings.aspect_for(Vector2i(720, 1280)), Window.CONTENT_SCALE_ASPECT_EXPAND, "a phone")
	assert_eq(Settings.aspect_for(Vector2i(1200, 1920)), Window.CONTENT_SCALE_ASPECT_EXPAND, "a tablet held upright")
	assert_eq(Settings.aspect_for(Vector2i(1000, 1000)), Window.CONTENT_SCALE_ASPECT_EXPAND)


func test_fitting_the_window_changes_only_its_scale_aspect_and_only_when_asked_off_a_phone() -> void:
	var window: Window = Settings.get_window()
	var saved: Window.ContentScaleAspect = window.content_scale_aspect
	var size: Vector2i = window.size
	window.content_scale_aspect = Window.CONTENT_SCALE_ASPECT_EXPAND
	window.size = Vector2i(1280, 800)
	Settings.fit_portrait()
	assert_eq(window.content_scale_aspect, Window.CONTENT_SCALE_ASPECT_EXPAND, "a desktop window is left alone")
	Settings.fit_portrait(true)
	assert_eq(window.content_scale_aspect, Window.CONTENT_SCALE_ASPECT_KEEP)
	window.size = Vector2i(720, 1280)
	Settings.fit_portrait(true)
	assert_eq(window.content_scale_aspect, Window.CONTENT_SCALE_ASPECT_EXPAND)
	window.size = size
	window.content_scale_aspect = saved
