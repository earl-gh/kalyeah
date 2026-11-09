class_name CameraRules
extends Resource
## The gameplay camera, like Clash Royale's: one fixed camera above the whole arena that never moves, so the
## entire 27 x 48 m street is on screen at all times (no scrolling, no following, nothing off screen). It is an
## ORTHOGRAPHIC camera tilted about 45 degrees (a kid is the same size at either end of the street, and the top
## and the front of every kid, wall and tower show), sized so the whole arena fits the phone with a margin kept
## clear at the top (score row), at the bottom (the controls) and at the sides. The view size and the small shift
## that centres the arena in the space left are solved from the screen's shape, so any phone, tablet or tall
## screen shows the whole street.
## The kids are drawn bigger than life (`kid_view_scale`) so they stay readable at this size; their hit area is
## the real one. Pure math on data from data/rules/camera_rules.tres.

## A lens curve of 1 is this barrel strength (k in shaders/lens_curve.gdshader).
const LENS_K_AT_FULL: float = 0.5
## The design screen: portrait 9:16.
const DESIGN_ASPECT: float = 9.0 / 16.0
## The smallest and largest distance (metres) the fit searches.
const MIN_DISTANCE: float = 10.0
const MAX_DISTANCE: float = 3000.0
## How far the orthographic camera stands from the point it aims at (metres). It changes nothing on screen, only where
## the depth range (near and far planes) sits.
const STANDOFF: float = 150.0
## The ground axes are rounded to this, so a quarter turn gives exact ones.
const AXIS_SNAP: float = 0.000001

## The camera's turn around the arena (0 = looking straight along the street).
@export var yaw_offset_degrees: float = 0.0
## Pitch from horizontal, in degrees.
@export var pitch_degrees: float = 0.0
## The wide curve lens (0 = none): distortion over the 3D view (shaders/lens_curve.gdshader).
@export var lens_curve: float = 0.0
## Fractions of the screen kept clear of the arena: the top (score and settings), the bottom (the controller body
## with the D-pad and the skills; at least this, and the real height of the controller when it is taller) and each side.
@export var frame_top: float = 0.0
@export var frame_bottom: float = 0.0
@export var frame_side: float = 0.0
## How much bigger than real the kids (and what they throw) are drawn, so they stay readable while the whole
## street is on screen. The hit area (the ring under a kid, the weapon shapes) is the real size and the same for every kid.
@export var kid_view_scale: float = 1.0
## How much smaller the health bars over the kids are drawn (1 = the full size).
@export var bar_scale: float = 1.0
## Follow camera (owner, 2026-11-09: Wild Rift style landscape; docs/GDD.md "Landscape layout"): instead of showing the whole street, the
## camera keeps your kid in the middle of the screen and the street scrolls. Off: the fixed whole-street camera above.
@export var follow: bool = false
## How many metres tall the followed view is (orthographic: the same at every phone width; a wider phone sees more street to the sides).
@export var follow_view_size: float = 18.0
## How quickly the view catches up with your kid (seconds to close most of the gap); 0 = it sticks to the kid. Reduce motion sticks.
@export var follow_lag: float = 0.0
## How far past the street's edges (metres: the sidewalks, the end props) the followed view may show before it stops scrolling.
@export var follow_edge: float = 0.0
## Third-person shoulder camera (owner, 2026-11-09: the players' point of view like the third-person hero shooters, Marvel Rivals and Deadlock;
## docs/GDD.md "Shoulder view"): a perspective camera behind and a little to the right of your kid, looking where you look (a drag on the right
## of the screen turns it), with a crosshair in the middle of the screen that the cards are cast at. It takes over from `follow`.
@export var third_person: bool = false
## The point the camera turns round: this high above your kid's feet (metres, for a kid drawn `kid_view_scale` times its size).
@export var shoulder_height: float = 3.0
## How far behind that point the camera stands, and how far to its right (the kid stands left of the crosshair, not under it).
@export var shoulder_distance: float = 6.5
@export var shoulder_offset: float = 1.1
## The lens: vertical field of view (degrees), and how near the camera it starts drawing (metres): a sidewalk prop or a wall closer than
## this (less than the way to your kid) is cut away instead of filling the screen.
@export var shoulder_fov: float = 75.0
@export var shoulder_near: float = 0.3
## How far down the view looks at the start (degrees below level), and the most it may look down and up (up is negative).
@export var look_pitch_degrees: float = 12.0
@export var look_pitch_max: float = 60.0
@export var look_pitch_min: float = -20.0
## How many degrees the view turns for each UI pixel a finger drags.
@export var look_sensitivity: float = 0.25
## Where the crosshair's ray meets nothing (looking over the horizon) the card goes this far along the view (metres).
@export var aim_reach: float = 12.0
## In this view a kid's name over its bar shows only within this many metres of the camera (farther kids keep their number badge and bar).
@export var shoulder_name_reach: float = 0.0
## The crosshair's point is taken as on the street up to this far past its edges and ends (metres).
@export var shoulder_bounds_edge: float = 2.5
## How long (s) the street takes to swing half way round when your view changes ends during a match (Tumbang Preso: becoming or no longer
## being the taya). It eases in and out; Reduce motion cuts instead.
@export var turn_seconds: float = 0.8


## Barrel strength of the lens for `lens_curve` (a curve of 1 would be k = 0.5).
func lens_k() -> float:
	return lens_curve * LENS_K_AT_FULL


## Where a ground point (x, z) at `height` shows on a flat screen of `aspect` (width / height) when the orthographic view is
## `view_size` metres tall and aims at `(0, aim_z)`: normalised, -1..1 across and up (x right, y up). Nothing shrinks with
## distance: a metre is the same number of pixels at either end of the street.
func screen_point(view_size: float, aspect: float, ground: Vector2, aim_z: float, height: float = 0.0) -> Vector2:
	var pitch: float = deg_to_rad(pitch_degrees)
	var half: float = view_size / 2.0
	var from_aim: Vector3 = Vector3(ground.x, height, ground.y - aim_z)
	var up: Vector3 = Vector3(0.0, cos(pitch), -sin(pitch))
	return Vector2(from_aim.x / (half * aspect), from_aim.dot(up) / half)


## The shift of the aim point along the street (metres, + toward the own end) that centres the arena's two ends in
## the band of the screen left between the top and bottom margins, for a camera `distance` away.
func centring_shift(distance: float, aspect: float, arena: Vector2, bottom: float = -1.0, top_margin: float = -1.0) -> float:
	var kept: float = frame_bottom if bottom < 0.0 else bottom
	var kept_top: float = frame_top if top_margin < 0.0 else top_margin
	var want: float = ((1.0 - 2.0 * kept_top) + (-1.0 + 2.0 * kept)) / 2.0
	var low: float = -arena.y
	var high: float = arena.y
	for i: int in 50:
		var shift: float = (low + high) / 2.0
		var far: float = screen_point(distance, aspect, Vector2(0.0, -arena.y / 2.0), shift).y
		var near: float = screen_point(distance, aspect, Vector2(0.0, arena.y / 2.0), shift).y
		if (far + near) / 2.0 > want:
			high = shift  # too high on the screen: aim further toward the far end less
		else:
			low = shift
	return (low + high) / 2.0


## The sideways shift of the aim point (metres, for the view that is not flipped; negate it for the flipped one) that centres the arena in the
## band of the screen left between a `left` and a `right` margin (fractions of the screen's width; -1 = `frame_side`): with a panel on one
## side the street is pushed to the other, at the size `frame` chose.
func centring_shift_x(distance: float, aspect: float, left: float = -1.0, right: float = -1.0) -> float:
	var left_kept: float = frame_side if left < 0.0 else left
	var right_kept: float = frame_side if right < 0.0 else right
	var band_centre: float = left_kept - right_kept  # normalised, -1..1 across
	return -band_centre * (distance / 2.0) * aspect


## True when the whole `arena` (x, z extents) fits inside the margins at `distance`.
func fits(distance: float, aspect: float, arena: Vector2, bottom: float = -1.0, top_margin: float = -1.0, left: float = -1.0, right: float = -1.0) -> bool:
	var kept: float = frame_bottom if bottom < 0.0 else bottom
	var kept_top: float = frame_top if top_margin < 0.0 else top_margin
	var shift: float = centring_shift(distance, aspect, arena, bottom, top_margin)
	var top: float = screen_point(distance, aspect, Vector2(0.0, -arena.y / 2.0), shift).y
	var bottom_edge: float = screen_point(distance, aspect, Vector2(0.0, arena.y / 2.0), shift).y
	if top > 1.0 - 2.0 * kept_top or bottom_edge < -1.0 + 2.0 * kept:
		return false
	for sx: float in [-1.0, 1.0]:
		for sz: float in [-1.0, 1.0]:
			var corner: Vector2 = screen_point(distance, aspect, Vector2(sx * arena.x / 2.0, sz * arena.y / 2.0), shift)
			if absf(corner.x) > 1.0 - (frame_side if left < 0.0 else left) - (frame_side if right < 0.0 else right):
				return false
	return true


## The camera's distance from the aim point (x) and the aim point's shift along the street (y) that put the whole
## `arena` on a screen of `aspect`: the nearest the camera can be and still fit.
func frame(aspect: float, arena: Vector2, bottom: float = -1.0, top_margin: float = -1.0, left: float = -1.0, right: float = -1.0) -> Vector2:
	var near: float = MIN_DISTANCE
	var far: float = MAX_DISTANCE
	for i: int in 50:
		var middle: float = (near + far) / 2.0
		if fits(middle, aspect, arena, bottom, top_margin, left, right):
			far = middle
		else:
			near = middle
	return Vector2(far, centring_shift(far, aspect, arena, bottom, top_margin))


## Camera position relative to the aim point. Own side (+Z, no flip) looks toward -Z, so the camera sits on the
## +Z side; the other team's view is turned half way round.
func camera_offset(camera_distance: float, flip: bool) -> Vector3:
	var pitch: float = deg_to_rad(pitch_degrees)
	var back: float = camera_distance * cos(pitch)
	var yaw: float = deg_to_rad(yaw_degrees(flip))
	return Vector3(back * sin(yaw), camera_distance * sin(pitch), back * cos(yaw))


## The camera's turn around the vertical axis (0 = looking straight down -Z).
func yaw_degrees(flip: bool) -> float:
	return yaw_offset_degrees + (180.0 if flip else 0.0)


## Where the camera aims: the middle of the street, shifted `aim_z` toward the own end (the other team's view is
## turned half way round, so the shift is the other way). Never moves with anyone.
func camera_target(aim_z: float, flip: bool, aim_x: float = 0.0) -> Vector2:
	return Vector2(-aim_x if flip else aim_x, -aim_z if flip else aim_z)


## The ground ways (sim XZ, unit) that point right on the screen and up the screen for a camera turned `yaw` degrees (0 = looking down -Z).
static func ground_axes(yaw: float) -> Array[Vector2]:
	var turn: float = deg_to_rad(yaw)
	# snapped, so a quarter or half turn gives exact axes (no 1e-16 left over from the sine of 180 degrees)
	return [Vector2(cos(turn), -sin(turn)).snappedf(AXIS_SNAP), Vector2(-sin(turn), -cos(turn)).snappedf(AXIS_SNAP)]


## A way on the screen (x right, y down, as a joystick reads) as a way on the ground for a camera turned `yaw` degrees.
static func ground_way(screen_way: Vector2, yaw: float) -> Vector2:
	var axes: Array[Vector2] = ground_axes(yaw)
	return axes[0] * screen_way.x - axes[1] * screen_way.y


## Where the followed view aims to keep `point` (sim XZ) in the middle without showing more than `follow_edge` past the `arena` (x, z
## extents, centred on the origin): along each screen axis the aim is held back from the ends, and a street narrower than the view along an
## axis stays centred on it. `view_size` metres tall on a screen of `aspect`, turned `yaw` degrees.
func follow_aim(point: Vector2, arena: Vector2, view_size: float, aspect: float, yaw: float) -> Vector2:
	var axes: Array[Vector2] = ground_axes(yaw)
	var half: Vector2 = arena / 2.0 + Vector2.ONE * follow_edge
	# the screen shows view_size * aspect metres across, and view_size / sin(pitch) metres of ground up the screen (the tilt shortens it)
	var seen: Vector2 = Vector2(view_size * aspect, view_size / maxf(sin(deg_to_rad(pitch_degrees)), 0.01)) / 2.0
	var aim: Vector2 = Vector2.ZERO
	for i: int in 2:
		var axis: Vector2 = axes[i]
		var reach: float = half.x * absf(axis.x) + half.y * absf(axis.y)
		var room: float = maxf(0.0, reach - seen[i])
		aim += axis * clampf(point.dot(axis), -room, room)
	return aim


## The shoulder camera's pose: for your kid's feet at `feet` (sim XZ) and a view turned `yaw` degrees (0 = looking down -Z) and tilted `pitch`
## degrees down, where the camera stands and its rotation in degrees. It is always exactly `shoulder_distance` behind the kid on the ground
## (owner, 2026-11-09: 12 m) and `shoulder_offset` to its right, as high as the tilt puts it while it looks at the point `shoulder_height`
## above the kid's feet; nothing pulls it in.
func shoulder_pose(feet: Vector2, yaw: float, pitch: float) -> Array[Vector3]:
	var rotation: Vector3 = Vector3(-pitch, yaw, 0.0)
	var basis: Basis = Basis.from_euler(rotation * (PI / 180.0))
	var pivot: Vector3 = Vector3(feet.x, shoulder_height, feet.y)
	var back: Vector3 = Vector3(basis.z.x, 0.0, basis.z.z).normalized()
	var rise: float = shoulder_distance * tan(deg_to_rad(clampf(pitch, -80.0, 80.0)))
	var at: Vector3 = pivot + back * shoulder_distance + Vector3.UP * rise + basis.x * shoulder_offset
	at.y = maxf(at.y, 0.5)
	return [at, rotation]


## The look's pitch kept inside its limits.
func clamp_pitch(pitch: float) -> float:
	return clampf(pitch, look_pitch_min, look_pitch_max)
