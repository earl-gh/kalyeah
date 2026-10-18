extends GutTest
## The street props must be whole surfaces. When the Meshy models were decimated without first
## welding their split vertices they were torn into open pieces: thousands of open edges (cracks,
## holes, seams) on a few thousand triangles. Now only a few percent of the edges are open.

const PROPS: Array[String] = [
	"basketball_pole", "bench", "cardboard_wall", "crates", "electric_pole",
	"fishball_cart", "ironing_board", "rooster_tent", "school_zone", "truck_wheels",
	"tricycle", "ebike", "sacks", "sewing_machine", "no_id_no_entry", "jeep", "pedicab", "ice_cream_cart", "nanay",
	"nena_store", "vulcanizing", "barber_shop", "house_1", "house_2", "house_3", "cat", "dog", "chicken_cage", "drainage_cover", "tricycle_terminal", "plank_fence", "roof_wall", "roof_wall_2",
]
## Open edges as a fraction of the triangles: the torn models were 0.9 to 1.3; whole ones are under 0.3.
const MAX_OPEN_FRACTION: float = 0.35
## The electric pole's dangling cables are thin flat ribbons, which are open strips by nature.
const MAX_OPEN_FRACTION_POLE: float = 0.7
## The owner's two houses are open by nature (thin eaves, railings and gutters; the source has more open edges than the game model).
const MAX_OPEN_FRACTION_HOUSE: float = 0.7


func test_the_street_props_are_whole_surfaces_not_torn_into_pieces() -> void:
	for id: String in PROPS:
		var counts: Vector2i = MeshAudit.open_edges_and_triangles("res://assets/models/props/%s.glb" % id)
		assert_gt(counts.y, 500, "%s has its triangles" % id)
		var limit: float = MAX_OPEN_FRACTION
		if id == "electric_pole":
			limit = MAX_OPEN_FRACTION_POLE
		elif id.begins_with("house_"):
			limit = MAX_OPEN_FRACTION_HOUSE
		assert_lt(float(counts.x) / float(counts.y), limit, "%s: %d open edges on %d triangles" % [id, counts.x, counts.y])
