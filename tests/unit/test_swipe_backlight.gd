extends GutTest
## Swiping the mode models: the backlight rests while the models are dragged and slide (it only lagged behind them), then comes
## back on the chosen model.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")

var _reduced: bool


func before_each() -> void:
	_reduced = Settings.reduced_motion
	Settings.set_reduced_motion(false)


func after_each() -> void:
	Settings.set_reduced_motion(_reduced)


func _screen() -> TitleScreen:
	var phone: Control = autofree(Control.new()) as Control
	phone.custom_minimum_size = Vector2(720.0, 1280.0)
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	phone.add_child(screen)
	return screen


func _button(pressed: bool, x: float) -> InputEventMouseButton:
	var event: InputEventMouseButton = InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = pressed
	event.position = Vector2(x, 600.0)
	return event


func _motion(x: float) -> InputEventMouseMotion:
	var event: InputEventMouseMotion = InputEventMouseMotion.new()
	event.position = Vector2(x, 600.0)
	return event


func test_the_lights_rest_while_the_models_are_dragged_and_return_when_settled() -> void:
	var screen: TitleScreen = _screen()
	await wait_process_frames(3)
	var carousel: MenuCarousel = screen.carousel
	assert_true(screen.backdrop.glow.visible, "lit when idle")
	assert_true(screen.backdrop.core.visible)
	carousel.swipe_input(_button(true, 400.0))
	carousel.swipe_input(_motion(330.0))
	carousel.swipe_input(_motion(260.0))
	assert_true(carousel.swiping, "dragging")
	assert_false(screen.backdrop.glow.visible, "the backlight is off while the models follow the finger")
	assert_false(screen.backdrop.core.visible)
	carousel.swipe_input(_button(false, 260.0))
	assert_false(screen.backdrop.glow.visible, "still off while they slide to the next model")
	await wait_seconds(MenuCarousel.WRAP_TIME + 0.2)
	assert_false(carousel.swiping, "settled")
	assert_true(screen.backdrop.glow.visible, "back on")
	assert_true(screen.backdrop.core.visible)
	await wait_seconds(UiBackdrop.LIGHT_FADE + 0.1)
	assert_almost_eq(screen.backdrop.glow.self_modulate.a, 1.0, 0.01, "fully back after the fade")
	var card: Button = carousel.cards[carousel.index]
	var centre: Vector2 = screen.backdrop.get_global_transform().affine_inverse() * card.get_global_rect().get_center()
	assert_almost_eq(screen.backdrop.glow.get_rect().get_center().x, centre.x, 3.0, "and it sits behind the chosen model")


func test_a_plain_tap_on_the_chosen_model_does_not_flicker_the_lights() -> void:
	var screen: TitleScreen = _screen()
	await wait_process_frames(3)
	var carousel: MenuCarousel = screen.carousel
	watch_signals(carousel)
	carousel.swipe_input(_button(true, 360.0))
	carousel.swipe_input(_button(false, 360.0))
	await wait_process_frames(2)
	assert_signal_not_emitted(carousel, "swiping_changed")
	assert_true(screen.backdrop.glow.visible)


func test_with_reduce_motion_the_lights_come_back_at_once() -> void:
	Settings.set_reduced_motion(true)
	var screen: TitleScreen = _screen()
	await wait_process_frames(3)
	var carousel: MenuCarousel = screen.carousel
	carousel.swipe_input(_button(true, 400.0))
	carousel.swipe_input(_motion(300.0))
	assert_false(screen.backdrop.glow.visible)
	carousel.swipe_input(_button(false, 300.0))
	await wait_process_frames(2)
	assert_true(screen.backdrop.glow.visible)
	assert_almost_eq(screen.backdrop.glow.self_modulate.a, 1.0, 0.01, "no fade")
