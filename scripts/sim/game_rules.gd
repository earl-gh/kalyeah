class_name GameRules
extends Resource
## Global gameplay numbers (docs/GDD.md "Core"). Values live in
## data/rules/game_rules.tres; nothing here is hardcoded.

@export var tick_rate: int = 0
@export var player_max_hp: int = 0
@export var move_speed: float = 0.0
@export var player_radius: float = 0.0
@export var spawn_spacing: float = 0.0
## Tag: at 0 HP a player is sent to the sidewalk beside their team's rescue area. A free teammate who stays in the rescue area for `rescue_hold_time`
## frees the teammate who was tagged first (data toggle: on = everyone waiting there at once).
@export var rescue_frees_all: bool = false
## Seconds a teammate has to stay in the rescue area to free one tagged teammate (0 = a touch frees at once). Only one teammate
## rescues at a time (the first to arrive); leaving, being tagged or a new round starts the count over.
@export var rescue_hold_time: float = 5.0
## A free player cannot cast while standing in their own team's rescue area with a teammate waiting on the sidewalk.
@export var no_cast_in_rescue_area: bool = true
## A free player cannot cast while standing in the enemy base (capturing it).
@export var no_cast_in_enemy_base: bool = true
## Practice: a tagged training dummy walks out on its own after this long.
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
## A base capture (free players of one team standing in the enemy base): one player alone needs this long ...
@export var capture_solo_time: float = 12.0
## ... and each further teammate in the base adds this share of one player's speed (1.0 = linear, 0 = no help):
## the countdown runs at 1 + (n - 1) * bonus times its solo speed.
@export var capture_ally_bonus: float = 0.75
## Overtime: once a round has gone on this long (seconds, 0 = never) the capture time drops to `overtime_capture_time`,
## so a round always ends.
@export var overtime_after: float = 0.0
@export var overtime_capture_time: float = 0.0
## A tagged player walks out alone after this many seconds (0 = they wait for a rescue), online and in rooms.
@export var tag_release_time: float = 0.0
## The same, for a Practice match.
@export var practice_tag_release_time: float = 0.0
## The opening show before the first round (both teams at their posts, then GO!).
@export var intro_time: float = 0.0
## Freeze after a point before everyone is reset to their posts.
@export var point_freeze_time: float = 0.0
## Weapon cards (owner): nobody picks weapons. Every player has a shuffled deck of all the weapons and a hand of two cards (the two
## skill buttons). Using a card sends it to the back of the deck; when its cooldown is over, the next card of the deck takes its
## place. Off = the old pick of two weapons before the match and the swap while tagged.
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
## Points per second standing still (after `stamina_regen_delay`) and, once the blink is over, while overclocked.
@export var stamina_regen_per_s: float = 25.0
## Seconds standing still before stamina refills.
@export var stamina_regen_delay: float = 0.5
## Overclock (owner): at 0 stamina the kid can only walk (`overclock_speed_share` of its run speed) until the bar is full again.
## For the first `exhaust_seconds` the bar blinks red and does not refill; then it refills at `stamina_regen_per_s`, walking or not.
@export var exhaust_seconds: float = 2.0
@export var overclock_speed_share: float = 0.5
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
