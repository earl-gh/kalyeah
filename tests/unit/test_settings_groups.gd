extends GutTest
## The Settings page is grouped by what a setting is for (owner): an Accessibility section (what lets a player use the game despite a
## limit of sight, motion sensitivity, handedness or aiming), then Controls (layout), Graphics (device speed) and Quit.
## Seam: SettingsPage (the section headings and where each switch sits), ControlOptions.Part, GraphicsOptions.with_shake.

const ACCESSIBILITY: Array[String] = ["ReducedMotion", "Colorblind", "ScreenShake", "LeftHanded", "AimGuide"]


func _page() -> SettingsPage:
	var page: SettingsPage = autofree(SettingsPage.new()) as SettingsPage
	add_child(page)
	return page


func test_the_sections_come_in_this_order() -> void:
	var page: SettingsPage = _page()
	await wait_frames(2)
	var names: Array[String] = []
	for child: Node in page.find_child("Accessibility", true, false).get_parent().get_children():
		names.append(String(child.name))
	assert_eq(names.slice(0, 2), ["Accessibility", "Controls"] as Array[String])
	assert_true(names.has("Graphics"))
	assert_lt(names.find("Controls"), names.find("Graphics"))
	assert_eq(names.find("Accessibility"), 0)
	assert_null(page.find_child("Comfort", true, false), "the old Comfort heading is gone")


func test_the_five_accessibility_switches_sit_in_the_accessibility_section() -> void:
	var page: SettingsPage = _page()
	await wait_frames(2)
	var section: Node = page.find_child("Accessibility", true, false)
	for switch_name: String in ACCESSIBILITY:
		assert_not_null(section.find_child(switch_name, true, false), "%s is under Accessibility" % switch_name)


func test_controls_hold_the_layout_and_graphics_hold_device_speed_only() -> void:
	var page: SettingsPage = _page()
	await wait_frames(2)
	var controls: Node = page.find_child("Controls", true, false)
	assert_not_null(controls.find_child("CardsTray", true, false), "Cards tray is a Controls setting")
	for switch_name: String in ACCESSIBILITY:
		assert_null(controls.find_child(switch_name, true, false), "%s is not under Controls" % switch_name)
	var graphics: Node = page.find_child("Graphics", true, false)
	assert_not_null(graphics.find_child("FrameRate", true, false))
	assert_not_null(graphics.find_child("Performance", true, false))
	assert_null(graphics.find_child("ScreenShake", true, false), "Screen shake moved to Accessibility")


func test_each_switch_appears_once_on_the_page() -> void:
	var page: SettingsPage = _page()
	await wait_frames(2)
	for switch_name: String in ACCESSIBILITY + ["CardsTray", "FrameRate", "Performance"]:
		assert_eq(page.find_children(switch_name, "", true, false).size(), 1, "%s once" % switch_name)


func test_the_match_menu_keeps_its_short_list() -> void:
	var column: VBoxContainer = autofree(VBoxContainer.new()) as VBoxContainer
	add_child(column)
	SettingsPage.fill(column, true)
	await wait_frames(1)
	assert_not_null(column.find_child("LeftHanded", true, false))
	assert_not_null(column.find_child("ReducedMotion", true, false))
	assert_null(column.find_child("AimGuide", true, false), "still only on the Settings page")
	assert_null(column.find_child("CardsTray", true, false))
