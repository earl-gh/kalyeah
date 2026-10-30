class_name BotLevel
extends Resource
## One Practice difficulty (Easy, Medium, Hard): the bot numbers that differ between them. Chosen before a Practice match against bots
## (docs/GDD.md "Practice difficulty"); `GameRules.with_bot_level` copies them over the rules' own bot numbers. Medium is those
## numbers as they stand (a test pins that), so the game's balance is unchanged by default.

@export var id: StringName = &""
@export var label: String = ""
## One line under the choice.
@export var blurb: String = ""
## How well a bot aims (`bot_skill`).
@export var skill: float = 0.0
## How far off its aim can be, in degrees (`bot_aim_error_degrees`).
@export var aim_error_degrees: float = 0.0
## Seconds between a bot's casts (`bot_cast_gap`).
@export var cast_gap: float = 0.0
## How near an enemy must come before a bot fights it (`bot_aggro_range`).
@export var aggro_range: float = 0.0
## Seconds into a round before a team attacks, then seconds of attack and seconds back at base (`bot_assault_delay`, `bot_assault_window`,
## `bot_rest_time`).
@export var assault_delay: float = 0.0
@export var assault_window: float = 0.0
@export var rest_time: float = 0.0
