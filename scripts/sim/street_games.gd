class_name StreetGames
extends Resource
## The Filipino street game behind each weapon, in one plain sentence each (shown in the Book). Text only: it
## never affects play. Values live in data/rules/street_games.tres, keyed by `WeaponDef.street_game`.

@export var blurbs: Dictionary[String, String] = {}


## The sentence for `game` (case-insensitive), or "" when there is none.
func blurb_of(game: String) -> String:
	if blurbs.has(game):
		return blurbs[game]
	var lower: String = game.to_lower()
	for known: String in blurbs:
		if known.to_lower() == lower:
			return blurbs[known]
	return ""
