extends GutTest

const CAMERA: CameraRules = preload("res://data/rules/camera_rules.tres")


## The shipped lens is off (it re-samples, and so softens, the whole picture); the maths is tested at
## the strength it had.
func test_the_shipped_lens_is_off_so_nothing_is_resampled() -> void:
	assert_eq(CAMERA.lens_k(), 0.0)
	assert_null(Cinematic.add_lens(self, CAMERA.lens_k()), "no lens layer at all")


func test_the_lens_leaves_the_centre_alone_and_pulls_the_edges_in() -> void:
	var k: float = -0.0498
	assert_lt(k, 0.0, "slightly concave, never convex")
	assert_gt(k, -0.05, "and so subtle the curve is hardly visible")
	assert_eq(LensCurve.source(Vector2(0.5, 0.5), k), Vector2(0.5, 0.5))
	assert_almost_eq(LensCurve.source(Vector2(1.0, 0.5), k).x, 1.0, 0.0001, "the middle of each frame edge still shows the flat edge")
	assert_gt(LensCurve.source(Vector2(0.9, 0.5), k).x, 0.9, "concave: the edges are stretched outward")
	assert_lte(LensCurve.source(Vector2(1.0, 1.0), k).x, 1.0, "the corners never sample past the picture")


func test_output_undoes_source() -> void:
	var k: float = CAMERA.lens_k()
	for flat: Vector2 in [Vector2(0.3, 0.2), Vector2(0.1, 0.9), Vector2(0.5, 0.62)]:
		var back: Vector2 = LensCurve.output(LensCurve.source(flat, k), k)
		assert_almost_eq(back.x, flat.x, 0.001)
		assert_almost_eq(back.y, flat.y, 0.001)
