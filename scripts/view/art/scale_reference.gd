class_name ScaleReference
extends RefCounted
## What things really measure, to hold every model to one scale. The kids are drawn `kid_view_scale` times their model (a model is KID_HEIGHT
## tall, a real eight to ten year old about REAL_KID); every prop and building should be drawn REAL_KID-relative: its real size times
## `world_factor()`, give or take the chunky look (a little bigger than true is allowed, see `CHUNK`). `tools/art/scale_audit.gd` prints the
## table and `tests/unit/test_model_scale.gd` holds it.

## A real kid, in metres.
const REAL_KID: float = 1.3
## Props may be drawn up to this much bigger (and down to 1 / this) than their true proportion to the kids: soft, chunky, toy-like.
const CHUNK: float = 1.3
## Real heights in metres (height of what you see; the basketball pole is its length lying down; the drainage cover its width).
const REAL_HEIGHT: Dictionary[String, float] = {
	"ice_cream_cart": 2.1, "tricycle": 1.7, "ebike": 1.15,
	"truck_wheels": 1.2, "bench": 0.9, "crates": 0.9, "sacks": 0.9, "no_id_no_entry": 1.5,
	"rooster_tent": 1.3, "plank_fence": 1.5, "roof_wall": 2.4, "roof_wall_2": 2.4,
	"basketball_pole": 4.6, "cat": 0.25, "dog": 0.5, "chicken_cage": 0.7, "nanay": 1.6, "drainage_cover": 0.6,
}
## Buildings are sized by the width of their lot (they stand in a row), so they are held to a real lot width and a believable height
## (one to two storeys) instead of one real height each.
const BUILDINGS: Array[String] = ["house_1", "house_2", "house_3", "karinderya", "nena_store", "vulcanizing", "barber_shop", "tricycle_terminal"]
const REAL_BUILDING_WIDTH: float = 5.0
## How far a building's drawn width may be from the real lot width times the world factor (a fraction), and its real height range.
const BUILDING_WIDTH_TOLERANCE: float = 0.2
const REAL_BUILDING_HEIGHT: Vector2 = Vector2(3.5, 7.0)
## Things drawn on purpose off the true proportion, and why (the audit and the test accept the listed range instead of the general one).
const EXCEPTIONS: Dictionary[String, Vector2] = {
}


## How many times bigger than real the world is drawn: the kid's drawn height over a real kid.
static func world_factor(kid_view_scale: float) -> float:
	return KidModel.KID_HEIGHT * kid_view_scale / REAL_KID


## The ratio range a prop's drawn size may have to its true size times the world factor.
static func allowed(id: String) -> Vector2:
	if EXCEPTIONS.has(id):
		return EXCEPTIONS[id]
	return Vector2(1.0 / CHUNK, CHUNK)
