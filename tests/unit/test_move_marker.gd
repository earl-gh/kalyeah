extends GutTest
## The tap-to-move marker (owner: "tapping a point to move is medium sized circle that smoothly shrinks to a white dot, current size of
## circle is small"): a fresh tap starts as a medium circle and eases down to a small white dot that stays as the target.

const DT: float = 1.0 / 30.0
## The size of the ring this replaced (radius on the ground, metres).
const OLD_RING: float = 0.42

var _marker: MoveMarker
var _motion: bool


func before_each() -> void:
	_motion = Settings.reduced_motion
	Settings.reduced_motion = false
	_marker = autofree(MoveMarker.new()) as MoveMarker


func after_each() -> void:
	Settings.reduced_motion = _motion


func test_a_tap_starts_as_a_medium_circle_clearly_bigger_than_the_old_ring() -> void:
	_marker.ping(Vector2(1.0, 2.0))
	assert_eq(_marker.radius(), MoveMarker.START_RADIUS, "it starts at the full size")
	assert_gte(MoveMarker.START_RADIUS, OLD_RING * 2.0, "at least twice the radius the small ring had")
	assert_between(MoveMarker.START_RADIUS, 0.8, 1.5, "medium: about two kids across, not the whole street")
	assert_eq(_marker.progress(), 0.0)


func test_it_shrinks_to_a_small_dot_and_stays_there() -> void:
	_marker.ping(Vector2.ZERO)
	_marker.tick(MoveMarker.SHRINK_TIME)
	assert_almost_eq(_marker.radius(), MoveMarker.DOT_RADIUS, 0.0001, "after the shrink it is the dot")
	assert_eq(_marker.progress(), 1.0)
	assert_between(MoveMarker.DOT_RADIUS, 0.1, 0.3, "a small dot")
	assert_lt(MoveMarker.DOT_RADIUS, MoveMarker.START_RADIUS / 3.0, "much smaller than the circle it came from")
	_marker.tick(5.0)
	assert_almost_eq(_marker.radius(), MoveMarker.DOT_RADIUS, 0.0001, "and it stays the dot as the target")


func test_half_way_is_between_the_two_and_ahead_of_a_straight_line() -> void:
	_marker.ping(Vector2.ZERO)
	_marker.tick(MoveMarker.SHRINK_TIME / 2.0)
	var middle: float = (MoveMarker.START_RADIUS + MoveMarker.DOT_RADIUS) / 2.0
	assert_gt(_marker.radius(), MoveMarker.DOT_RADIUS)
	assert_lt(_marker.radius(), MoveMarker.START_RADIUS)
	assert_lt(_marker.radius(), middle, "an ease-out: most of the shrink is done by half time")


func test_the_shrink_is_smooth_and_never_grows_back() -> void:
	_marker.ping(Vector2.ZERO)
	var last: float = _marker.radius()
	var biggest_step: float = 0.0
	var ticks: int = int(ceil(MoveMarker.SHRINK_TIME / DT)) + 3
	for i: int in ticks:
		_marker.tick(DT)
		var now: float = _marker.radius()
		assert_lte(now, last + 0.00001, "tick %d: never bigger than before" % i)
		biggest_step = maxf(biggest_step, last - now)
		last = now
	assert_lt(biggest_step, (MoveMarker.START_RADIUS - MoveMarker.DOT_RADIUS) * 0.5, "no frame jumps more than half of the way: eased, not stepped")
	assert_gte(MoveMarker.SHRINK_TIME, 3.0 * DT, "and it takes several frames")
	assert_almost_eq(last, MoveMarker.DOT_RADIUS, 0.0001)


func test_the_circle_has_no_tint_inside_and_only_closes_shut_at_the_end() -> void:
	_marker.ping(Vector2.ZERO)
	assert_eq(_marker.closed(), 0.0, "on the tap: an empty circle, just a line")
	_marker.tick(MoveMarker.SHRINK_TIME * 0.3)
	assert_eq(_marker.closed(), 0.0, "while it is still a circle nothing fills it")
	var last: float = 0.0
	for i: int in 20:
		_marker.tick(DT)
		assert_gte(_marker.closed(), last, "the line only ever closes up")
		last = _marker.closed()
	assert_eq(_marker.closed(), 1.0, "at the end it is the solid dot")
	assert_eq(MoveMarker.DOT_COLOR, Color.WHITE, "a white dot")


func test_the_line_thins_as_the_circle_shrinks_so_a_small_circle_is_not_a_fat_ring() -> void:
	_marker.ping(Vector2.ZERO)
	assert_eq(_marker.line_width(), MoveMarker.WIDTH, "full width on the tap")
	var last: float = _marker.line_width()
	for i: int in 14:
		_marker.tick(DT)
		assert_lte(_marker.line_width(), last + 0.00001, "never thicker than before")
		last = _marker.line_width()
	assert_almost_eq(_marker.line_width(), MoveMarker.THIN_WIDTH, 0.0001, "thin at the end")
	assert_lt(MoveMarker.THIN_WIDTH, MoveMarker.WIDTH / 2.0, "clearly thinner")


func test_reduce_motion_shows_just_the_dot() -> void:
	Settings.reduced_motion = true
	_marker.ping(Vector2.ZERO)
	assert_almost_eq(_marker.radius(), MoveMarker.DOT_RADIUS, 0.0001, "no shrink to watch")


func test_dragging_moves_the_dot_without_bringing_the_circle_back() -> void:
	_marker.ping(Vector2.ZERO)
	_marker.tick(MoveMarker.SHRINK_TIME * 2.0)
	_marker.ping(Vector2(3.0, 1.0), false)
	assert_almost_eq(_marker.radius(), MoveMarker.DOT_RADIUS, 0.0001, "the finger sliding on the street does not replay the shrink")
	_marker.ping(Vector2(4.0, 1.0))
	assert_eq(_marker.radius(), MoveMarker.START_RADIUS, "a new tap does")
