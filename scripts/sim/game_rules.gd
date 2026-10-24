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
## After a hard crowd control (stun, airborne, bounce, knock-out, polymorph) runs out, a new one is ignored for this many
## seconds (0 = no immunity), so one kid cannot be chain-stunned out of a round. Slows are never blocked.
@export var cc_immunity: float = 0.0
## A returning boomerang is caught when this close to its thrower.
@export var boomerang_catch_distance: float = 0.0
## Scoring: a capture (the whole team in the enemy base, or every enemy tagged) has
## to hold this long to win the point.
@export var capture_time: float = 0.0
## A base capture does not run while a free player of the defending team stands in that base (the defenders must
## be tagged or leave first). Off: only the capture time decides.
@export var capture_contested: bool = true
## Overtime: once a round has gone on this long (seconds, 0 = never) the capture time drops to `overtime_capture_time`,
## so a round always ends.
@export var overtime_after: float = 0.0
@export var overtime_capture_time: float = 0.0
## A tagged player walks out of the box alone after this many seconds (0 = they wait for a rescue), online and in rooms.
@export var tag_release_time: float = 0.0
## The same, for a Practice match.
@export var practice_tag_release_time: float = 0.0
## The opening show before the first round (both teams at their posts, then GO!).
@export var intro_time: float = 0.0
## Freeze after a point before everyone is reset to their posts.
@export var point_freeze_time: float = 0.0
## Weapon cards (owner): nobody picks weapons. Every player has a shuffled deck of all the weapons and a hand of two cards (the two
## skill buttons). Using a card sends it to the back of the deck; when its cooldown is over, the next card of the deck takes its
## place. Off = the old pick of two weapons before the match and the swap in the box.
@export var card_cycle: bool = true
## docs/GDD.md "Controls: tap to move, cards to cast". Defaults are the GDD's; the .tres may override them.
## Cards in the hand (NEXT is extra).
@export var hand_size: int = 4
## Elixir pays for cards (D26). Off = the old shuffler that plays each card's `cooldown`.
@export var elixir_enabled: bool = true
@export var elixir_max: float = 10.0
## Elixir at match start (and at every round start with `elixir_reset_on_round`).
@export var elixir_start: float = 5.0
## Every round starts at `elixir_start`; off = keep what you had (D30).
@export var elixir_reset_on_round: bool = true
## Points per second; it also fills while tagged.
@export var elixir_regen_per_s: float = 0.36
## Off = no stamina, no exhaustion.
@export var stamina_enabled: bool = true
@export var stamina_max: float = 100.0
## Points per second of moving, at any speed (D29).
@export var stamina_drain_per_s: float = 10.0
## Points per second standing still (after `stamina_regen_delay`) and while exhausted (no delay).
@export var stamina_regen_per_s: float = 25.0
## Seconds standing still before stamina refills.
@export var stamina_regen_delay: float = 0.5
## Seconds a kid cannot move once its stamina hits 0 (owner).
@export var exhaust_seconds: float = 2.0
## Metres; within it the move target is reached.
@export var move_arrival_radius: float = 0.1
## A tick that makes less than this share of its intended progress toward the target counts as stuck.
@export var move_stuck_share: float = 0.25
## Seconds of stuck ticks that end the move (D28).
@export var move_stuck_time: float = 0.2
## Input: a held finger / right button drags the move target (D27). Not read by the sim.
@export var move_follow_hold: bool = true
## Input: a press and release on a card within this many seconds is an auto-aim tap. Not read by the sim.
@export var card_tap_time: float = 0.3
## Metres; a heal dropped this close to your own kid heals you.
@export var self_cast_radius: float = 1.0
## Metres; a card dropped this close to your kid has no direction, so the kid's facing is used.
@export var aim_no_direction_radius: float = 0.1
## Bots: with no enemy near, a bot below this share of stamina stops to refill, until it is back to `bot_stamina_resume`.
@export var bot_stamina_rest: float = 0.2
@export var bot_stamina_resume: float = 0.6
## The match is won by the first team to this many points (practice, and the lobby's default).
@export var first_to: int = 0
## What the host can pick in the lobby.
@export var first_to_choices: Array[int] = []
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
## How well a bot aims, 0 (at where you are) to 1 (at where you will be, with little error).
@export var bot_skill: float = 0.0
## A bot team in a Practice match waits this many seconds into a round before it attacks (0 = at once).
@export var bot_assault_delay: float = 0.0
## Then it attacks in waves: this many seconds of attack (0 = it never stops), then `bot_rest_time` seconds back at its base.
@export var bot_assault_window: float = 0.0
@export var bot_rest_time: float = 0.0


## Fixed simulation step in seconds (1 / tick_rate).
func tick_dt() -> float:
	return 1.0 / tick_rate


## True when cards are paid for with elixir (cards on and elixir on); otherwise each card plays its `cooldown`.
func uses_elixir() -> bool:
	return card_cycle and elixir_enabled
