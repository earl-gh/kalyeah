extends GutTest
## Graphics settings (docs/GDD.md "Graphics settings"): Frame rate 30 / 60, Performance Standard / Turbo and Screen shake.
## Seam: Settings (values, save file, apply_graphics), GraphicsOptions on the Settings page, ArenaCamera.shake, FilmFinish.

var _saved: Dictionary = {}


func before_each() -> void:
	_saved = {"rate": Settings.frame_rate, "mode": Settings.performance, "shake": Settings.screen_shake, "motion": Settings.reduced_motion}
	Settings.set_reduced_motion(false)
	Settings.set_frame_rate(60)
	Settings.set_performance(&"standard")
	Settings.set_screen_shake(true)


func after_each() -> void:
	Settings.set_frame_rate(_saved["rate"] as int)
	Settings.set_performance(_saved["mode"] as StringName)
	Settings.set_screen_shake(_saved["shake"] as bool)
	Settings.set_reduced_motion(_saved["motion"] as bool)


func test_the_defaults_are_60_standard_and_shake_on() -> void:
	assert_eq(Settings.FRAME_RATES, [30, 60] as Array[int])
	assert_eq(Settings.PERFORMANCES, [&"standard", &"turbo"] as Array[StringName])
	assert_eq(Settings.nearest_frame_rate(null), 60, "nothing saved: 60")
	assert_eq(Settings.nearest_frame_rate("fast"), 60, "rubbish in the file: 60")


func test_a_frame_rate_is_one_of_the_two_offered() -> void:
	assert_eq(Settings.nearest_frame_rate(30), 30)
	assert_eq(Settings.nearest_frame_rate(90), 60)
	assert_eq(Settings.nearest_frame_rate(40.0), 30)
	Settings.set_frame_rate(30)
	assert_eq(Settings.frame_rate, 30)
	Settings.set_frame_rate(144)
	assert_eq(Settings.frame_rate, 60, "an odd value becomes the nearest offered one")


func test_choices_are_saved() -> void:
	Settings.set_frame_rate(30)
	Settings.set_performance(&"turbo")
	Settings.set_screen_shake(false)
	var file: ConfigFile = ConfigFile.new()
	assert_eq(file.load(Settings.SAVE_PATH), OK)
	assert_eq(int(file.get_value("graphics", "frame_rate", 0)), 30)
	assert_eq(String(file.get_value("graphics", "performance", "")), "turbo")
	assert_false(bool(file.get_value("graphics", "screen_shake", true)))


func test_only_a_phone_gets_a_frame_cap_so_tests_and_desktops_never_slow_down() -> void:
	var before: int = Engine.max_fps
	Settings.set_frame_rate(30)
	assert_eq(Engine.max_fps, before)


func test_turbo_turns_off_anti_aliasing_and_draws_the_3d_street_smaller_and_standard_puts_it_back() -> void:
	var viewport: Viewport = Settings.get_viewport()
	var project_msaa: int = ProjectSettings.get_setting("rendering/anti_aliasing/quality/msaa_3d", 2) as int
	Settings.set_performance(&"turbo")
	assert_eq(viewport.msaa_3d, Viewport.MSAA_DISABLED)
	assert_almost_eq(viewport.scaling_3d_scale, Settings.TURBO_SCALE, 0.0001)
	assert_eq(Settings.TURBO_SCALE, 0.75)
	Settings.set_performance(&"standard")
	assert_eq(int(viewport.msaa_3d), project_msaa, "the project's own anti-aliasing")
	assert_eq(viewport.scaling_3d_scale, 1.0)
	Settings.set_performance(&"ludicrous")
	assert_eq(Settings.performance, &"standard", "an unknown mode changes nothing")


func test_turbo_also_drops_the_film_grain() -> void:
	var finish: FilmFinish = autofree(FilmFinish.new()) as FilmFinish
	add_child(finish)
	assert_gt(finish.finish.get_shader_parameter("grain") as float, 0.0)
	Settings.set_performance(&"turbo")
	assert_eq(finish.finish.get_shader_parameter("grain") as float, 0.0)


func test_screen_shake_off_keeps_the_street_still() -> void:
	var camera: ArenaCamera = autofree(ArenaCamera.new()) as ArenaCamera
	add_child(camera)
	Settings.set_screen_shake(true)
	camera.shake(0.3)
	assert_gt(camera._shake, 0.0, "on: it shakes")
	camera._shake = 0.0
	Settings.set_screen_shake(false)
	camera.shake(0.3)
	assert_eq(camera._shake, 0.0, "off: it does not")


func test_the_settings_page_has_a_graphics_card_that_changes_the_settings() -> void:
	var page: SettingsPage = autofree(SettingsPage.new()) as SettingsPage
	add_child(page)
	await wait_frames(2)
	var options: GraphicsOptions = page.find_child("GraphicsOptions", true, false) as GraphicsOptions
	assert_not_null(options, "a Graphics section on the page")
	assert_eq(options.frame_choice.buttons.size(), 2)
	assert_eq(options.frame_choice.current, &"60")
	options.frame_choice.buttons[&"30"].pressed.emit()
	assert_eq(Settings.frame_rate, 30)
	assert_eq(options.performance_choice.current, &"standard")
	options.performance_choice.buttons[&"turbo"].pressed.emit()
	assert_eq(Settings.performance, &"turbo")
	assert_true(options.shake_toggle.button_pressed)
	options.shake_toggle.button_pressed = false
	options.shake_toggle.toggled.emit(false)
	assert_false(Settings.screen_shake)
