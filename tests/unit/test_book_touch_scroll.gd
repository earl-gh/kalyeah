extends GutTest
## The Characters and Weapons lists scroll when a finger drags anywhere on them, cards included, not only on the
## scrollbar; and a drag that scrolls never opens the card it started on.


## A phone-sized screen of its own, so the touches reach the page and not the test runner's window.
var _phone: SubViewport
var _was_emulating: bool


## A desktop test machine has no touchscreen, and Godot only drag-scrolls a list where there is one.
func before_each() -> void:
	_was_emulating = Input.emulate_touch_from_mouse
	Input.emulate_touch_from_mouse = true


func after_each() -> void:
	Input.emulate_touch_from_mouse = _was_emulating


func _page(page: Control) -> Control:
	_phone = autofree(SubViewport.new()) as SubViewport
	_phone.size = Vector2i(720, 1280)
	add_child(_phone)
	page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_phone.add_child(page)
	return page


## Drags a finger up from `from` by `distance`, through the viewport the way a phone sends it.
func _drag_up(from: Vector2, distance: float) -> void:
	var viewport: Viewport = _phone
	viewport.push_input(TouchHelper.click(from, true))
	var at: Vector2 = from
	var step: Vector2 = Vector2(0.0, -distance / 12.0)
	for i: int in 12:
		at += step
		var moved: InputEventMouseMotion = TouchHelper.motion(at)
		moved.relative = step
		viewport.push_input(moved)
	viewport.push_input(TouchHelper.click(at, false))


func _first_card_centre(card: Control) -> Vector2:
	return card.get_global_rect().get_center()


func test_dragging_on_a_character_card_scrolls_the_list_and_opens_nothing() -> void:
	var page: CharactersPage = _page(CharactersPage.new()) as CharactersPage
	await wait_process_frames(3)
	var scroll: ScrollContainer = page.get_node("Scroll") as ScrollContainer
	watch_signals(page)
	_drag_up(_first_card_centre(page.cards.values()[0] as Control), 300.0)
	await wait_process_frames(2)
	assert_gt(scroll.scroll_vertical, 100, "a drag that starts on a card scrolls the list")
	assert_signal_not_emitted(page, "character_opened", "scrolling is not a tap")


func test_dragging_on_a_weapon_tile_scrolls_the_list_and_opens_nothing() -> void:
	var page: WeaponsPage = _page(WeaponsPage.new()) as WeaponsPage
	await wait_process_frames(3)
	watch_signals(page)
	_drag_up(_first_card_centre(page.tiles[0]), 300.0)
	await wait_process_frames(2)
	assert_gt(page._scroll.scroll_vertical, 100, "a drag that starts on a tile scrolls the list")
	assert_signal_not_emitted(page, "weapon_opened", "scrolling is not a tap")


func test_a_tap_on_a_character_card_still_opens_it() -> void:
	var page: CharactersPage = _page(CharactersPage.new()) as CharactersPage
	await wait_process_frames(3)
	watch_signals(page)
	var at: Vector2 = _first_card_centre(page.cards.values()[0] as Control)
	_phone.push_input(TouchHelper.click(at, true))
	_phone.push_input(TouchHelper.click(at, false))
	await wait_process_frames(1)
	assert_signal_emitted(page, "character_opened")
