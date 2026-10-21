class_name MatchScore
extends RefCounted
## One-tier scoring (docs/GDD.md "Scoring"): every capture is one point and the
## match is first to 3, 5 or 7 points (the host picks it in the lobby): the first team to
## reach that many wins (it used to be best of 3, 5 or 7). Plain data; MatchSim decides when a point happens.

enum Result { POINT, MATCH }

## Points per team (0, 1). Kept as-is when the match ends.
var points: Array[int] = [0, 0]
## How many points win the match (3, 5 or 7): the first team to reach it wins.
var first_to: int = 3
## Team that won the match, or -1.
var winner: int = -1


func _init(p_first_to: int) -> void:
	first_to = p_first_to


## Points a team needs: `first_to` itself.
func points_to_win() -> int:
	return first_to


func award_point(team: int) -> Result:
	if winner >= 0:
		return Result.MATCH
	points[team] += 1
	if points[team] >= points_to_win():
		winner = team
		return Result.MATCH
	return Result.POINT
