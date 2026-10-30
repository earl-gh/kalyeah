extends GutTest
## The sticky page header's shadow: on the pages that scroll (Games, Cards, History, and Settings since its Graphics card) a faint shadow
## sits under the header and deepens as content moves beneath it; a page that fits on the screen keeps its plain header.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")


func _screen() -> TitleScreen:
	var phone: Control = autofree(Control.new()) as Control
	phone.custom_minimum_size = Vector2(720.0, 1280.0)
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	phone.add_child(screen)
	return screen


func _shadow(page: Control) -> TextureRect:
	return page.find_child("HeaderShadow", true, false) as TextureRect


func test_scrolling_pages_have_a_header_shadow_that_deepens_with_the_scroll() -> void:
	var screen: TitleScreen = _screen()
	await wait_process_frames(2)
	for id: StringName in [&"codex", &"street", &"settings"]:
		screen.open_page(id)
		await wait_process_frames(4)
		var page: Control = screen.pages[id]
		var shade: TextureRect = _shadow(page)
		assert_not_null(shade, "%s has a header shadow" % id)
		assert_true(shade.visible, "%s scrolls, so its header casts a shadow" % id)
		assert_almost_eq(shade.modulate.a, UiKit.HEADER_SHADOW_REST, 0.01, "faint at the top")
		assert_almost_eq(shade.size.y, UiKit.HEADER_SHADOW_HEIGHT, 0.5)
		var scroll: ScrollContainer = page.find_child("Scroll", true, false) as ScrollContainer
		scroll.scroll_vertical = 200
		await wait_process_frames(2)
		assert_almost_eq(shade.modulate.a, 1.0, 0.01, "full once content is under the header")
		assert_eq(shade.mouse_filter, Control.MOUSE_FILTER_IGNORE, "it never takes a touch")


func test_a_page_that_fits_keeps_its_plain_header() -> void:
	var page: VBoxContainer = autofree(VBoxContainer.new()) as VBoxContainer
	page.custom_minimum_size = Vector2(720.0, 1000.0)
	page.size = Vector2(720.0, 1000.0)
	add_child(page)
	var line: Label = Label.new()
	line.text = "A page with a few short lines"
	UiKit.add_gutter_scroll(page, line)
	await wait_process_frames(4)
	var shade: TextureRect = _shadow(page)
	assert_not_null(shade)
	assert_false(shade.visible, "it fits on the screen: no shadow")
