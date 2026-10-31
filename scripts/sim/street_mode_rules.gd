class_name StreetModeRules
extends Resource
## The numbers of a timed street game (docs/GDD.md "Tumbang Preso and Patintero"). Values live in
## data/rules/tumbang_preso.tres and data/rules/patintero.tres; nothing here is hardcoded.
## The street runs along Z: the throwers / runners start at the +Z end (the bottom of the screen) and run to the -Z end.

## RoomService.MAP_IDS id: &"tumbang_preso" or &"patintero".
@export var id: StringName = &""
## The game's name, as the Games page and the Practice row write it.
@export var title: String = ""
## Length of the match (seconds). The leader then wins; a tie plays on until the next point.
@export var match_seconds: float = 300.0
## In a Local or Online room, the game starts with at least this many players (owner: Tumbang Preso 3 to 6; Patintero is 1v1 to 3v3).
@export var room_min_players: int = 2
## Nobody can be tagged for this long after a swap of roles (everyone is walking to their places).
@export var swap_grace: float = 1.5
## Every runner (and every Tumbang Preso thrower in the fetch area) has a tag circle round it, this big (m, radius): the taya or guard
## that moves into it tags. The circle is drawn on the ground for the one who tags.
@export var tag_circle_radius: float = 1.3
## After a tag the round resets by itself: everyone stands still this long (s, the tagger plays its touch animation), then all go back
## to their places.
@export var reset_seconds: float = 1.3
## The safe home (Tumbang Preso: the throwing area, behind the throw line; Patintero: past the first guard line, clear of its tag circle):
## z at least this. Everything in front of it up to the far end is the fetch area (Tumbang Preso) or the court (Patintero).
@export var home_z: float = 16.0
## The other end: z at most this (Patintero: past the last guard line, clear of its tag circle: the runner has passed the last guard).
@export var goal_z: float = -18.0
## Kids start this far apart across the street (m).
@export var spawn_spacing: float = 3.0

@export_group("Tumbang Preso")
## Where the can stands (x, z).
@export var can_spot: Vector2 = Vector2.ZERO
## A thrown slipper that passes this close to the standing can hits it (m).
@export var can_hit_radius: float = 0.45
## The throw is aimed by holding the Throw button (docs/GDD.md "Aiming a throw"): the aim line starts `throw_min_range` m long and grows to
## `throw_max_range` m over `throw_charge_time` s of holding; the slipper flies that far, at `throw_speed` m/s, and lands there.
@export var throw_min_range: float = 6.0
@export var throw_max_range: float = 18.0
@export var throw_charge_time: float = 1.4
@export var throw_speed: float = 18.0
## The aim stays within this many degrees either side of straight toward the far end.
@export var aim_arc_degrees: float = 80.0
## How far the aim turns per pixel of finger drag (radians): `aim_sensitivity_low` while the line is short, rising to
## `aim_sensitivity_high` at full length (Beatrix's sniper aim: fine at first, fast at the limit).
@export var aim_sensitivity_low: float = 0.0015
@export var aim_sensitivity_high: float = 0.006
## A kid this close picks up its own slipper, or (the taya) the fallen can (m). A little bigger than the slipper model; it is the ring
## drawn on the thrower's own slipper.
@export var pickup_radius: float = 0.7
## The can carried this close to its spot stands again (m).
@export var place_radius: float = 0.8
## Each round the taya starts, and waits, this far behind the can (away from the throwers, m): a slight, small distance.
@export var guard_offset: float = 1.6
## The taya (the "it") runs this many times as fast as a thrower.
@export var taya_speed_share: float = 1.5
## A slipper that lands in the fetch area gives its thrower this long (s) to step into the fetch area, or the thrower becomes the taya
## (too scared to fetch it).
@export var scared_seconds: float = 5.0
## A hit sends the can `can_fling_distance` m and the slipper `slipper_fling_distance` m, each a little off what the guide shows: the
## direction by up to `fling_error_degrees`, the length by up to `fling_distance_error` (a share). The can goes on along the throw bent
## up to `can_deflect_degrees` toward the side it was hit on; the slipper bounces off, `slipper_back_degrees` round (a dead-on hit) down
## to `slipper_side_degrees` (a glancing one).
@export var can_fling_distance: float = 6.0
@export var slipper_fling_distance: float = 5.0
@export var fling_error_degrees: float = 18.0
@export var fling_distance_error: float = 0.2
@export var can_deflect_degrees: float = 35.0
@export var slipper_back_degrees: float = 150.0
@export var slipper_side_degrees: float = 80.0
## `bounce_chance` of the time the can and the slipper bounce once more after the fling: another fling `bounce_share` as long, turned
## by up to `bounce_error_degrees` (two flings: harder to fetch). Each fling takes `fling_time` s to land.
@export var bounce_chance: float = 0.3
@export var bounce_share: float = 0.5
@export var bounce_error_degrees: float = 25.0
@export var fling_time: float = 0.55
## The aim guide of where the can and the slipper would go is only this long (m): it shows the direction, never the end.
@export var guide_length: float = 2.5
## The Kick button (a thrower with no slipper in hand): within `kick_reach` m of the can it flings the can once, in a random direction,
## `kick_min_distance` to `kick_max_distance` m (mid range). A thrower can kick again after `kick_cooldown` s.
@export var kick_reach: float = 1.6
@export var kick_min_distance: float = 4.0
@export var kick_max_distance: float = 8.0
@export var kick_cooldown: float = 4.0
## The kick sends the can away from the kicker in a random direction within this many degrees either side of straight away (a forward cone, so it
## flies the way the foot swings); a kicker standing right on the can sends it anywhere.
@export var kick_cone_degrees: float = 75.0

@export_group("Patintero")
## The court: two horizontal lines across the street at z = +line_span and -line_span, with one vertical line down the middle (x = 0). A
## guard keeps to the one line it is given. A runner has to cross the vertical line once before the far end counts.
@export var line_span: float = 10.0
## How wide the sidewalk is (m, past the street's wall) where a tagged runner waits and moves; it matches the one the street draws.
@export var sidewalk_width: float = 2.4
## A tagged runner stands still this long (s, it stumbles) and then is on the sidewalk.
@export var catch_seconds: float = 0.7
## A round in which no runner has passed the first guard after this long (s) is a point for the guards, and the teams swap roles.
@export var stall_seconds: float = 20.0
## The Dive button (runners and guards): a quick burst of `dive_speed_share` times the run speed, `dive_time` s long, along the way the
## knob points (a guard too: a dive is the one thing that takes it off its line). Then the kid lies flat and gets up (`dive_recover`). A
## guard then runs back to the nearest point of its line, `dive_return_speed_share` times the run speed, and cannot move or dive meanwhile.
## The cooldown (`dive_cooldown` s) starts when the guard is back on its line, or when a runner is on its feet again.
@export var dive_speed_share: float = 2.6
@export var dive_time: float = 0.4
## After the burst the kid lies flat on the street and gets up: it cannot steer for this long more (s).
@export var dive_recover: float = 0.5
@export var dive_cooldown: float = 5.0
@export var dive_return_speed_share: float = 1.5

@export_group("Bots")
## Bots aim at the can within this much (m, either way, seeded) and hold the aim line until it reaches it.
@export var bot_throw_error: float = 0.8
## A bot holds the aim line this much (m) longer than the way to its target, so the slipper reaches the can.
@export var bot_throw_margin: float = 0.5
## Seconds a bot thrower waits behind the line before it throws (from 0 up to this, seeded).
@export var bot_throw_wait: float = 2.0
## A bot guard runs at this share of its speed (a human guard runs at full speed).
@export var bot_guard_speed: float = 0.8
## A bot fetches its slipper or crosses a line when no guard is closer than this (m).
@export var bot_safe_distance: float = 5.0
## A Patintero bot dives when its line or its next line is this near (m, along its way).
@export var bot_dive_range: float = 3.0
## A bot thrower fetches its slipper whatever the taya does once its scared timer is down to this many seconds.
@export var bot_rush_seconds: float = 2.0
