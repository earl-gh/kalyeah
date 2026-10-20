class_name GameRules
extends Resource
## Global gameplay numbers (docs/GDD.md "Core"). Values live in
## data/rules/game_rules.tres; nothing here is hardcoded.

@export var tick_rate: int = 0
@export var player_max_hp: int = 0
@export var move_speed: float = 0.0
@export var player_radius: float = 0.0
@export var spawn_spacing: float = 0.0
## Tag: at 0 HP a player is sent to their team's box. A free teammate stepping into
## the box rescues everyone waiting there (data toggle: off = one per touch).
@export var rescue_frees_all: bool = true
## Practice: a tagged training dummy walks out of the box on its own after this long.
@export var dummy_release_time: float = 0.0
## Every weapon in the game (docs/GDD.md "Weapons"); each player equips two different ones.
@export var weapons: Array[WeaponDef] = []
## The six cosmetic characters, assigned at random with no duplicates.
@export var characters: Array[CharacterDef] = []
## Weapon pick time (match start).
@export var weapon_pick_time: float = 0.0
## POLYMORPH: walk at this fraction of normal speed (no casting).
@export var polymorph_speed_scale: float = 0.0
## Aim stick shorter than this counts as "no aim" and the weapon auto-aims.
@export var aim_deadzone: float = 0.0
## A returning boomerang is caught when this close to its thrower.
@export var boomerang_catch_distance: float = 0.0
## Scoring: a capture (the whole team in the enemy base, or every enemy tagged) has
## to hold this long to win the point.
@export var capture_time: float = 0.0
## The opening show before the first round (both teams at their posts, then GO!).
@export var intro_time: float = 0.0
## Freeze after a point before everyone is reset to their posts.
@export var point_freeze_time: float = 0.0
## The match is best of this many points (practice, and the lobby's default).
@export var best_of: int = 0
## What the host can pick in the lobby.
@export var best_of_choices: Array[int] = []
## Practice-mode training dummies (two enemies and one ally).
@export var dummy_z: float = 0.0
@export var dummy_stand_x: float = 0.0
@export var dummy_patrol_x: float = 0.0
@export var dummy_patrol_range: float = 0.0
@export var dummy_patrol_speed_scale: float = 0.0
@export var dummy_ally_x: float = 0.0
@export var dummy_ally_z: float = 0.0
## Practice sparring partners: how many of the enemy dummies fight back (the patrol one first, then the
## standing one), how close an enemy must come before one reacts, the seconds between its casts, how far off
## its aim can be (degrees) and the share of its weapon reach it likes to keep from an enemy.
@export var practice_fighters: int = 0
@export var bot_aggro_range: float = 0.0
@export var bot_cast_gap: float = 0.0
@export var bot_aim_error_degrees: float = 0.0
@export var bot_standoff_share: float = 0.0


## Fixed simulation step in seconds (1 / tick_rate).
func tick_dt() -> float:
	return 1.0 / tick_rate
