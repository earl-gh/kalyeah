class_name LensCurve
extends RefCounted
## The wide curve lens (shaders/lens_curve.gdshader) as maths, so things drawn by the HUD over the
## bent picture (health bars, the aim guide) line up with what the shader shows.
## A point at screen position `out` (0..1 across the screen) shows what the camera sees at
## `source(out)`; the middle keeps its scale and the edges are pulled in (barrel distortion).


## Where on the camera's flat picture (0..1) the screen position `out` (0..1) shows.
static func source(out: Vector2, k: float) -> Vector2:
	var p: Vector2 = out * 2.0 - Vector2.ONE
	return (p * (1.0 + k * p.dot(p)) / (1.0 + k)) * 0.5 + Vector2(0.5, 0.5)


## Where on screen (0..1) the point `flat` (0..1) of the camera's flat picture ends up (the
## inverse of `source`, by a few rounds of fixed-point iteration).
static func output(flat: Vector2, k: float) -> Vector2:
	var target: Vector2 = flat * 2.0 - Vector2.ONE
	var p: Vector2 = target
	for i: int in 10:
		p = target * (1.0 + k) / (1.0 + k * p.dot(p))
	return p * 0.5 + Vector2(0.5, 0.5)


## Pixels: the same two maps for a viewport of `size` pixels.
static func source_px(out_px: Vector2, size: Vector2, k: float) -> Vector2:
	return source(out_px / size, k) * size


static func output_px(flat_px: Vector2, size: Vector2, k: float) -> Vector2:
	return output(flat_px / size, k) * size
