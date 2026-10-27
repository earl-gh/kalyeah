class_name LarongKalye
extends Resource
## The Filipino street game of every month (larong kalye), taught from the calendar icon on the main screen so kids can play the real
## thing in the street or the backyard. Text only: it never affects play. The data is `data/rules/larong_kalye.tres`, written by
## `tools/data/make_larong_kalye.py`. Each game is a Dictionary: month, name, also, players_min, players_max, time, place, items,
## goal, setup, how, rules, win, safety, twist.

const MONTH_NAMES: PackedStringArray = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"]

@export var games: Array[Dictionary] = []


## The game of `month` (1 to 12), or an empty Dictionary when there is none.
func for_month(month: int) -> Dictionary:
	for game: Dictionary in games:
		if int(game["month"]) == month:
			return game
	return {}


static func month_name(month: int) -> String:
	return MONTH_NAMES[clampi(month, 1, 12) - 1]
