extends GutTest
## The Play tab (owner, 2026-10-29): a swipe anywhere on the screen steps the mode carousel, not only a swipe on the mode models. The
## buttons keep their taps, a tap on the empty screen picks nothing, and no other page is swiped behind.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")

var _phone: SubViewport


func _screen() -> TitleScreen:
	_phone = autofree(SubViewport.new()) as SubViewport
	_phone.size = Vector2i(720, 1280)
	add_child(_phone)
	var screen: TitleScreen = TITLE_SCENE.instantiate() as TitleScreen
	_phone.add_child(screen)
	return screen


## A finger dragged sideways from `from` by `distance` px, through the viewport the way a phone sends it.
func _swipe(from: Vector2, distance: float) -> void:
	_phone.push_input(TouchHelper.click(from, true))
	var at: Vector2 = from
	var step: Vector2 = Vector2(distance / 12.0, 0.0)
	for i: int in 12:
		at += step
		var moved: InputEventMouseMotion = TouchHelper.motion(at)
		moved.relative = step
		_phone.push_input(moved)
	_phone.push_input(TouchHelper.click(at, false))


func test_a_swipe_on_the_phrase_under_the_models_steps_the_carousel() -> void:
	var screen: TitleScreen = _screen()
	await wait_process_frames(3)
	var before: int = screen.carousel.index
	var phrase: Vector2 = screen.mode_text_label.get_global_rect().get_center()
	assert_false(screen.carousel.get_global_rect().has_point(phrase), "the phrase is outside the carousel's own area")
	_swipe(phrase, -260.0)
	assert_eq(screen.carousel.index, posmod(before + 1, screen.carousel.cards.size()), "a swipe left there is one step on")
	_swipe(phrase, 260.0)
	assert_eq(screen.carousel.index, before, "and a swipe right one step back")


func test_a_swipe_on_the_empty_space_beside_the_buttons_steps_it_too() -> void:
	var screen: TitleScreen = _screen()
	await wait_process_frames(3)
	var before: int = screen.carousel.index
	var buttons: Rect2 = screen.primary_button.get_global_rect()
	var beside: Vector2 = Vector2(16.0, buttons.end.y + 24.0)
	assert_false(screen.carousel.get_global_rect().has_point(beside))
	_swipe(beside, 300.0)
	assert_eq(screen.carousel.index, posmod(before - 1, screen.carousel.cards.size()))


func test_a_tap_on_the_empty_screen_picks_nothing_and_buttons_keep_their_taps() -> void:
	var screen: TitleScreen = _screen()
	await wait_process_frames(3)
	var before: int = screen.carousel.index
	var phrase: Vector2 = screen.mode_text_label.get_global_rect().get_center()
	_phone.push_input(TouchHelper.click(phrase, true))
	_phone.push_input(TouchHelper.click(phrase, false))
	assert_eq(screen.carousel.index, before, "a tap is not a swipe")
	var pressed: Array[bool] = []
	screen.primary_button.pressed.connect(func() -> void: pressed.append(true))
	var on_button: Vector2 = screen.primary_button.get_global_rect().get_center()
	_phone.push_input(TouchHelper.click(on_button, true))
	_phone.push_input(TouchHelper.click(on_button, false))
	assert_eq(pressed.size(), 1, "the button above the swipe area still gets its tap")
	assert_eq(screen.carousel.index, before)


func test_no_swipe_reaches_the_carousel_while_another_page_is_open() -> void:
	var screen: TitleScreen = _screen()
	await wait_process_frames(3)
	var before: int = screen.carousel.index
	screen.open_page(&"settings")
	await wait_process_frames(2)
	screen.swipe_area.gui_input.emit(TouchHelper.click(Vector2(100.0, 600.0), true))
	screen.swipe_area.gui_input.emit(TouchHelper.motion(Vector2(400.0, 600.0)))
	screen.swipe_area.gui_input.emit(TouchHelper.click(Vector2(400.0, 600.0), false))
	assert_eq(screen.carousel.index, before, "Settings is open: the modes stay put")
