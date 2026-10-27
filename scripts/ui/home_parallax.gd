class_name HomeParallax
extends Node
## Home's tilt parallax: leaning the phone shifts the layers a little at different depths, like a window onto the street. The mode models
## (near) move with the lean, the street picture (far) the other way, so the gap between them reads as depth. Level follows the phone's
## posture over a couple of seconds (`TiltInput.lean`), so it only answers a change; it rests under a page or a sheet, with Reduce
## motion, and on a device with no tilt sensor.

## The most the chosen model and the street picture shift (px) at a full lean.
const MODEL_SHIFT: float = 12.0
const PICTURE_SHIFT: float = 6.0
## How quickly the layers follow the lean (per second): smooth, never a jitter.
const EASE: float = 8.0

var carousel: MenuCarousel
var backdrop: UiBackdrop
## Off under a page or a sheet (the screen's `_sync_ambient`): the layers ease back to rest.
var active: bool = true:
	set(value):
		active = value
		if not value:
			_tilt.reset()
var lean: Vector2 = Vector2.ZERO
var _tilt: TiltInput = TiltInput.new()


func _init(p_carousel: MenuCarousel, p_backdrop: UiBackdrop) -> void:
	name = "HomeParallax"
	carousel = p_carousel
	backdrop = p_backdrop


func _process(delta: float) -> void:
	step(TiltInput.gravity(), delta)


## One frame: the lean now (zero when resting), eased in, and the layers shifted by it.
func step(reading: Vector3, delta: float) -> void:
	var target: Vector2 = Vector2.ZERO
	if active and not Settings.reduced_motion:
		target = _tilt.lean(reading, delta)
	lean = lean.lerp(target, clampf(EASE * delta, 0.0, 1.0))
	if lean.length_squared() < 0.00001:
		lean = Vector2.ZERO
	if is_instance_valid(carousel):
		carousel.tilt = lean * MODEL_SHIFT
	if is_instance_valid(backdrop):
		backdrop.set_tilt(-lean * PICTURE_SHIFT)
