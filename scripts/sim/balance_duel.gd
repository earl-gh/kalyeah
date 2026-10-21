class_name BalanceDuel
extends RefCounted
## One 1v1 between two sparring bots (FightBrain) on an open street, for balance numbers: the first to be
## tagged loses, and a duel nobody wins in time is a draw. Pure sim, no nodes, deterministic for a seed.
## The bots are simple (they do not lead a target, use cover or play traps and fields cleverly), so a result
## says how a weapon does in the hands of a simple player, not how strong it is in the hands of a good one.

## How far apart they start (metres), along the street.
const START_DISTANCE: float = 10.0
const MAX_SECONDS: float = 60.0
const NO_WINNER: int = -1


## The street without its cover (a fair, empty arena); the walls stay.
static func arena(layout: MapLayout) -> MapLayout:
	var open: MapLayout = layout.duplicate() as MapLayout
	var none: Array[Rect2] = []
	open.obstacles = none
	return open


## Plays `first` (team 0) against `second` (team 1). Returns {winner: 0, 1 or NO_WINNER, seconds, hp: [first, second]}.
## `skill` is how well both bots aim (0 to 1, see FightBrain); `cover` keeps the street's own cover in the way.
static func run(rules: GameRules, layout: MapLayout, first: Array[StringName], second: Array[StringName], seed_value: int, max_seconds: float = MAX_SECONDS, skill: float = 0.0, cover: bool = false) -> Dictionary:
	var sim: MatchSim = MatchSim.new(rules, layout if cover else arena(layout), seed_value)
	var top: Vector2 = Vector2(0.0, -START_DISTANCE / 2.0)
	var bottom: Vector2 = Vector2(0.0, START_DISTANCE / 2.0)
	var low: FightBrain = FightBrain.fighter(bottom)
	var high: FightBrain = FightBrain.fighter(top)
	low.skill = skill
	high.skill = skill
	sim.add_dummy(1, 0, bottom, low)
	sim.add_dummy(2, 1, top, high)
	sim.set_loadout(1, first[0], first[1])
	sim.set_loadout(2, second[0], second[1])
	var dt: float = rules.tick_dt()
	var ticks: int = ceili(max_seconds / dt)
	var winner: int = NO_WINNER
	var seconds: float = max_seconds
	for i: int in ticks:
		sim.step(dt)
		var first_down: bool = sim.players[1].tagged
		var second_down: bool = sim.players[2].tagged
		if first_down or second_down:
			if first_down and not second_down:
				winner = 1
			elif second_down and not first_down:
				winner = 0
			seconds = float(i + 1) * dt
			break
	return {"winner": winner, "seconds": seconds, "hp": [sim.players[1].hp, sim.players[2].hp]}


## Every two-weapon loadout the rules allow (two different weapons), as [first, second] ids in file order.
static func all_loadouts(rules: GameRules) -> Array[Array]:
	var found: Array[Array] = []
	for i: int in rules.weapons.size():
		for j: int in range(i + 1, rules.weapons.size()):
			found.append([rules.weapons[i].id, rules.weapons[j].id])
	return found
