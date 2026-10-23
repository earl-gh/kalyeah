class_name MatchSim
extends RefCounted
## Authoritative match simulation. Never touches nodes, input devices or
## rendering; the same class runs on the server, in offline practice and in tests.
## Advances only through step(dt) and uses its own seeded RNG (no randf()).

## At 0 HP a player is tagged: sent to their team's box until a teammate rescues them.
signal player_tagged(id: int)
## `by_id` is the rescuing teammate, or -1 when a training dummy walked out on its own.
signal player_rescued(id: int, by_id: int)

## A team scored (`how` is a CaptureKind). A freeze follows.
signal point_scored(team: int, how: int)
signal match_won(team: int)
## The freeze is over: everyone is back at their post at full HP.
signal point_reset
## The opening show is over: play starts now ("GO!").
signal intro_finished
## A status effect landed on a player (stun, slow, polymorph...).
signal effect_applied(id: int, type: int)
## A player regained HP (the heal skill).
signal player_healed(id: int, amount: int)
## A weapon was cast (for sounds and effects).
signal weapon_cast(id: int, weapon_id: StringName)
## A new card took a slot of a hand (cards mode).
signal card_drawn(id: int, slot: int)
## A player lost HP.
signal player_damaged(id: int, amount: int)

const RELEASED_ALONE: int = -1

## INTRO: the opening show of both teams at their posts; nothing moves.
enum Phase { PLAYING, POINT_FREEZE, MATCH_OVER, INTRO }
## How a team is scoring: everyone in the enemy base, or every enemy in the box.
enum CaptureKind { NONE = -1, BASE = 0, ALL_TAGGED = 1 }

var rules: GameRules
var layout: MapLayout
var rng: RandomNumberGenerator = RandomNumberGenerator.new()
var tick: int = 0
## id -> state, in insertion order (stepping order is deterministic).
var players: Dictionary[int, PlayerState] = {}
## Cover on the street (from the map layout); blocks walking and throws for everyone.
var obstacles: Array[Rect2] = []
## Live weapon effects (projectiles, zones, shields).
var weapons: WeaponSystem = WeaponSystem.new()
## Authoritative weapon input and cast-on-release state machine.
var casting: WeaponCasting = WeaponCasting.new()
## Tag, rescue, capture, scoring, and round reset rules.
var objectives: MatchObjectives = MatchObjectives.new()
## id -> WeaponDef, from rules.weapons.
var weapon_defs: Dictionary[StringName, WeaponDef] = {}
var score: MatchScore
## The team whose capture countdown is running (-1: none), how, and for how long.
var capture_team: int = -1
var capture_kind: CaptureKind = CaptureKind.NONE
var capture_elapsed: float = 0.0
## Seconds a capture has to hold now (the capture time, or the shorter overtime one).
var capture_needed: float = 0.0
## Seconds into this round (counts up in play; back to 0 after a point).
var round_time: float = 0.0
## A tagged player walks out of the box alone after this long (0 = waits for a rescue).
var tag_release_time: float = 0.0
var phase: Phase = Phase.PLAYING
## Seconds left in the freeze after a point.
var freeze_left: float = 0.0
## Seconds left of the opening show (Phase.INTRO).
var intro_left: float = 0.0
## The match clock: seconds of play so far (counts up; stops in freezes and the intro).
var match_time: float = 0.0
## How every point of the match was won, in order: (team that scored, CaptureKind).
var point_log: Array[Vector2i] = []

var _inputs: Dictionary[int, PlayerInput] = {}
var _brains: Dictionary[int, DummyBrain] = {}
var _team_counts: Dictionary[int, int] = {}
var _blocking: Array[Rect2] = []
## Which side team 0 defends this set (teams switch bases every set).


func _init(p_rules: GameRules, p_layout: MapLayout, seed_value: int) -> void:
	rules = p_rules
	layout = p_layout
	rng.seed = seed_value
	obstacles = layout.obstacle_rects()
	_blocking.append_array(layout.boundary_rects())
	_blocking.append_array(obstacles)
	for def: WeaponDef in rules.weapons:
		weapon_defs[def.id] = def
	score = MatchScore.new(rules.first_to)
	capture_needed = rules.capture_time
	tag_release_time = rules.tag_release_time


## Team 0 plays from the own (+Z) end, team 1 from the enemy (-Z) end.
func side_for_team(team: int) -> int:
	return MapLayout.SIDE_OWN if team == 0 else MapLayout.SIDE_ENEMY


## True when `state` is a player of the other team standing in its own team's tagged box, as
## `viewer_team` sees it. Free kids hide there among the tagged ones (owner): to the other team
## their bar and status are hidden and their minimap dot looks tagged, so they cannot be told
## apart from the tagged kids and the other team cannot tell who a rescuer is.
func hides_in_box(state: PlayerState, viewer_team: int) -> bool:
	return state.team != viewer_team and layout.jail_rect(side_for_team(state.team)).has_point(state.position)


## First to `count` points (one of `rules.first_to_choices`; anything else is ignored).
## Set before play starts: it restarts the score.
func set_first_to(count: int) -> void:
	if count in rules.first_to_choices:
		score = MatchScore.new(count)


## Client-side prediction: runs only the movement part of a tick for one player
## (walking; obstacles and speed modifiers included). Skills,
## weapons and everything else stay server-side.
func predict_move(id: int, input: PlayerInput, dt: float) -> void:
	var state: PlayerState = players.get(id) as PlayerState
	if state == null or state.tagged or phase != Phase.PLAYING:
		return
	_move(state, input, dt)


func add_player(id: int, team: int) -> PlayerState:
	var slot: int = 0
	if _team_counts.has(team):
		slot = _team_counts[team]
	_team_counts[team] = slot + 1
	var side: int = side_for_team(team)
	var state: PlayerState = PlayerState.new()
	state.id = id
	state.team = team
	state.spawn_position = _spawn_position(side, slot)
	state.position = state.spawn_position
	state.facing = Vector2(0.0, -side)
	state.hp = rules.player_max_hp
	state.radius = rules.player_radius
	state.effects.immunity_length = rules.cc_immunity
	players[id] = state
	return state


## A random pair of weapons with at least one that hurts, for a sparring bot (a bot with two shields is no
## sparring partner).
func fighting_loadout() -> Array[StringName]:
	var pair: Array[StringName] = random_loadout()
	for attempt: int in 20:
		if weapon_defs[pair[0]].damage > 0 or weapon_defs[pair[1]].damage > 0:
			break
		pair = random_loadout()
	return pair


## True when `id` is driven by a bot that fights back.
func is_fighter(id: int) -> bool:
	return _brains.get(id) is FightBrain


## Equips two different weapons. Returns false (and changes nothing) for an
## unknown weapon or the same weapon twice; two of the same *type* is fine.
func set_loadout(id: int, first: StringName, second: StringName) -> bool:
	if not players.has(id) or first == second:
		return false
	if not weapon_defs.has(first) or not weapon_defs.has(second):
		return false
	var state: PlayerState = players[id]
	state.weapons = [first, second]
	state.weapon_cooldowns = [0.0, 0.0]
	state.aim_hold = [-1.0, -1.0]
	return true


## Cards mode: shuffles every weapon into the player's deck (with the match's seeded rng); the first two are the hand, the third waits in
## the shuffler's window as the next card.
func deal_cards(id: int) -> bool:
	if not players.has(id) or weapon_defs.size() < 4:
		return false
	var state: PlayerState = players[id]
	var ids: Array[StringName] = []
	for def: WeaponDef in rules.weapons:
		ids.append(def.id)
	# Fisher-Yates with the match rng: the same seed deals the same decks
	for i: int in range(ids.size() - 1, 0, -1):
		var j: int = rng.randi_range(0, i)
		var held: StringName = ids[i]
		ids[i] = ids[j]
		ids[j] = held
	state.cards = true
	state.weapons = [ids[0], ids[1]]
	state.next_card = ids[2]
	state.deck.assign(ids.slice(3))
	state.incoming.clear()
	state.shuffle_card = &""
	state.shuffle_left = 0.0
	state.shuffle_total = 0.0
	state.weapon_cooldowns = [0.0, 0.0]
	state.aim_hold = [-1.0, -1.0]
	return true


## Equips a player for the match: a dealt hand in cards mode, else a random pair (`fighter`: one that hurts).
func equip(id: int, fighter: bool = false) -> void:
	if rules.card_cycle:
		deal_cards(id)
		return
	var pair: Array[StringName] = fighting_loadout() if fighter else random_loadout()
	set_loadout(id, pair[0], pair[1])


## A card was used from `slot`: the next card drops into the slot at once (if the shuffler has one in its window) and the used card goes
## to the shuffler, which plays its cooldown. The other card in the hand is untouched.
func cycle_used(state: PlayerState, slot: int) -> void:
	if not state.cards:
		return
	state.incoming.append(state.weapons[slot])
	state.weapons[slot] = state.next_card
	state.next_card = &""
	state.weapon_cooldowns[slot] = 0.0
	if state.weapons[slot] != &"":
		card_drawn.emit(state.id, slot)
	_tick_shuffler(state, 0.0)  # an idle shuffler takes the card now


## The shuffler: it takes a used card, plays that card's cooldown, then sends the card to the back of the deck and puts the front card of the
## deck in an empty hand slot, or in its window when the hand is full.
func _tick_shuffler(state: PlayerState, dt: float) -> void:
	if not state.cards:
		return
	if state.shuffle_card == &"" and not state.incoming.is_empty():
		state.shuffle_card = state.incoming.pop_front()
		state.shuffle_total = weapon_defs[state.shuffle_card].cooldown
		state.shuffle_left = state.shuffle_total
	if state.shuffle_card == &"":
		return
	state.shuffle_left -= dt
	if state.shuffle_left > 0.0:
		return
	state.deck.append(state.shuffle_card)
	state.shuffle_card = &""
	state.shuffle_left = 0.0
	state.shuffle_total = 0.0
	_place_next(state)


## The front card of the deck goes to the first empty hand slot, else to the window.
func _place_next(state: PlayerState) -> void:
	if state.deck.is_empty():
		return
	for slot: int in state.weapons.size():
		if state.weapons[slot] == &"":
			state.weapons[slot] = state.deck.pop_front()
			card_drawn.emit(state.id, slot)
			return
	if state.next_card == &"":
		state.next_card = state.deck.pop_front()
		card_drawn.emit(state.id, -1)


## A new round: the shuffler finishes at once and the hand and the window are full again.
func finish_reloads(state: PlayerState) -> void:
	for slot: int in state.weapon_cooldowns.size():
		state.weapon_cooldowns[slot] = 0.0
	if not state.cards:
		return
	if state.shuffle_card != &"":
		state.deck.append(state.shuffle_card)
	state.deck.append_array(state.incoming)
	state.incoming.clear()
	state.shuffle_card = &""
	state.shuffle_left = 0.0
	state.shuffle_total = 0.0
	for attempt: int in 3:
		_place_next(state)


## Box swap: only while tagged (waiting in the box), as often as you like.
## A weapon you keep keeps its cooldown, so swapping away and back clears nothing.
func swap_loadout(id: int, first: StringName, second: StringName) -> bool:
	if rules.card_cycle or not players.has(id) or not players[id].tagged:
		return false
	var state: PlayerState = players[id]
	var kept: Dictionary[StringName, float] = {}
	for slot: int in state.weapons.size():
		kept[state.weapons[slot]] = state.weapon_cooldowns[slot]
	if not set_loadout(id, first, second):
		return false
	for slot: int in state.weapons.size():
		state.weapon_cooldowns[slot] = kept.get(state.weapons[slot], 0.0) as float
	return true


## Two different random weapons (auto-fill when the pick timer runs out).
func random_loadout() -> Array[StringName]:
	var ids: Array[StringName] = []
	for def: WeaponDef in rules.weapons:
		ids.append(def.id)
	var first: int = rng.randi_range(0, ids.size() - 1)
	var second: int = rng.randi_range(0, ids.size() - 2)
	if second >= first:
		second += 1
	return [ids[first], ids[second]]


## Sets a player's character (the one they picked). A team never has the same kid twice:
## the teammate who held it gets the previous one back (a swap). The other team may have the
## same kid (once). False for an unknown id or player.
func set_character(id: int, character_id: StringName) -> bool:
	if not players.has(id) or not rules.characters.any(func(c: CharacterDef) -> bool: return c.id == character_id):
		return false
	var previous: StringName = players[id].character_id
	for other: int in players:
		if other != id and players[other].team == players[id].team and players[other].character_id == character_id:
			players[other].character_id = previous
	players[id].character_id = character_id
	return true


## The kids a teammate of `id` has locked in (they confirmed), which `id` cannot take.
func teammates_kids(id: int, confirmed: Array[int]) -> Array[StringName]:
	var kids: Array[StringName] = []
	for other: int in confirmed:
		if other != id and players.has(other) and players.has(id) and players[other].team == players[id].team:
			kids.append(players[other].character_id)
	return kids


## Gives every player a random character, no duplicates (seeded, so the server and
## replays agree). Purely cosmetic.
func assign_characters() -> void:
	var ids: Array[StringName] = []
	for character: CharacterDef in rules.characters:
		ids.append(character.id)
	for i: int in range(ids.size() - 1, 0, -1):
		var j: int = rng.randi_range(0, i)
		var swap: StringName = ids[i]
		ids[i] = ids[j]
		ids[j] = swap
	var index: int = 0
	for id: int in players:
		players[id].character_id = ids[index % ids.size()]
		index += 1


## Players that can be hit (everyone not waiting in a box).
func is_targetable(state: PlayerState) -> bool:
	return not state.tagged


func enemies_of(team: int) -> Array[PlayerState]:
	var result: Array[PlayerState] = []
	for id: int in players:
		var state: PlayerState = players[id]
		if state.team != team and is_targetable(state):
			result.append(state)
	return result


func nearest_enemy(caster: PlayerState, max_range: float) -> PlayerState:
	var best: PlayerState = null
	var best_distance: float = max_range
	for target: PlayerState in enemies_of(caster.team):
		var distance: float = caster.position.distance_to(target.position)
		if distance <= best_distance:
			best_distance = distance
			best = target
	return best


## Status effect from a weapon.
func apply_effect(id: int, type: StatusEffects.Type, duration: float, magnitude: float) -> void:
	if not players.has(id) or not is_targetable(players[id]):
		return
	var state: PlayerState = players[id]
	var fresh: bool = type != StatusEffects.Type.NONE and not state.effects.has(type)
	var applied: bool = state.effects.apply(type, duration, magnitude)
	if fresh and applied:
		effect_applied.emit(id, type)


## A training dummy: a normal player on `team` placed at `position`, driven by `brain`.
## Tagged dummies walk out of the box on their own after `dummy_release_time` and go
## back to this spot. Dummies don't count for captures.
func add_dummy(id: int, team: int, position: Vector2, brain: DummyBrain) -> PlayerState:
	var state: PlayerState = add_player(id, team)
	state.position = position
	state.spawn_position = position
	_brains[id] = brain
	return state


func set_input(id: int, input: PlayerInput) -> void:
	_inputs[id] = input


## Applies damage. At 0 HP the player is tagged.
func damage(id: int, amount: int) -> void:
	if not players.has(id) or amount <= 0:
		return
	var state: PlayerState = players[id]
	if state.tagged:
		return
	state.hp = maxi(0, state.hp - amount)
	player_damaged.emit(id, amount)
	if state.hp <= 0:
		objectives.tag(self, state)


## Heals up to max HP; returns the HP actually gained. Nothing while tagged.
func heal(id: int, amount: int) -> int:
	var state: PlayerState = players.get(id) as PlayerState
	if state == null or amount <= 0 or state.tagged:
		return 0
	var gained: int = mini(amount, rules.player_max_hp - state.hp)
	if gained <= 0:
		return 0
	state.hp += gained
	player_healed.emit(id, gained)
	return gained


## Tags a player straight away (scripted or out-of-play).
func kill(id: int) -> void:
	if players.has(id) and not players[id].tagged:
		players[id].hp = 0
		objectives.tag(self, players[id])


## Starts the opening show: both teams stand at their posts for
## `rules.intro_time`, then play starts. Call once, before the first step.
func begin_intro() -> void:
	phase = Phase.INTRO
	intro_left = rules.intro_time


func step(dt: float) -> void:
	if phase == Phase.MATCH_OVER:
		tick += 1
		return
	if phase == Phase.INTRO:
		intro_left -= dt
		if intro_left <= 0.0:
			intro_left = 0.0
			phase = Phase.PLAYING
			intro_finished.emit()
		tick += 1
		return
	if phase == Phase.POINT_FREEZE:
		freeze_left -= dt
		if freeze_left <= 0.0:
			objectives.end_freeze(self)
		tick += 1
		return
	for id: int in _brains:
		if not players[id].tagged:
			_inputs[id] = _brains[id].think(players[id], self)
	for id: int in players:
		var state: PlayerState = players[id]
		var input: PlayerInput = _inputs[id] if _inputs.has(id) else null
		if state.tagged:
			_step_tagged(state, input, dt)
		else:
			_step_free(state, input, dt)
	match_time += dt
	round_time += dt
	capture_needed = current_capture_time()
	weapons.step(self, dt)
	objectives.check_rescues(self)
	objectives.check_captures(self, _brains, dt)
	tick += 1


func _tick_cooldowns(state: PlayerState, dt: float) -> void:
	for slot: int in state.weapon_cooldowns.size():
		state.weapon_cooldowns[slot] = maxf(0.0, state.weapon_cooldowns[slot] - dt)
	_tick_shuffler(state, dt)


func _step_free(state: PlayerState, input: PlayerInput, dt: float) -> void:
	state.effects.step(dt)
	_tick_cooldowns(state, dt)
	if input != null:
		casting.handle(self, state, input, dt)
	_move(state, input, dt)


## Waiting in the box: no moving or skills, cooldowns keep running. Training dummies
## walk out on their own after a while.
func _step_tagged(state: PlayerState, input: PlayerInput, dt: float) -> void:
	_tick_cooldowns(state, dt)
	state.tag_time += dt
	if input != null:
		state.previous_buttons = input.buttons
	var training_dummy: bool = _brains.has(state.id) and state.tag_time >= rules.dummy_release_time
	var timed_out: bool = tag_release_time > 0.0 and state.tag_time >= tag_release_time
	if training_dummy or timed_out:
		objectives.rescue(self, state, RELEASED_ALONE)


func weapon_ready(state: PlayerState, slot: int) -> bool:
	return casting.weapon_ready(state, slot)


## The aim a release would use: the stick if it is past the deadzone, otherwise the
## auto-aim locked at the press (or a live auto-aim if the slot is not held yet).
func resolved_aim(state: PlayerState, slot: int, stick: Vector2) -> Vector2:
	return casting.resolved_aim(self, state, slot, stick)


## The target a TARGETED weapon would throw at (locked at the press while held).
func resolved_target(state: PlayerState, slot: int) -> int:
	return casting.resolved_target(self, state, slot)


func _move(state: PlayerState, input: PlayerInput, dt: float) -> void:
	if state.cast_lock_left > 0.0:
		# a heavy attack's cast animation: aiming was free while moving, but now stand still
		state.cast_lock_left = maxf(0.0, state.cast_lock_left - dt)
		return
	if input == null or not state.effects.can_move():
		return
	var move: Vector2 = input.move.limit_length(1.0)
	if move.length_squared() > 0.0:
		state.facing = move.normalized()
	var speed: float = rules.move_speed
	speed *= state.effects.speed_multiplier(rules.polymorph_speed_scale)
	_move_by(state, move * speed * dt)


## Moves by `delta` in sub-steps no longer than the player's radius so fast
## moves can never tunnel through an obstacle.
func _move_by(state: PlayerState, delta: Vector2) -> void:
	var length: float = delta.length()
	if length <= 0.0:
		return
	var count: int = maxi(1, ceili(length / state.radius))
	var piece: Vector2 = delta / count
	for i: int in count:
		state.position = Collision.resolve(state.position + piece, state.radius, _blocking)


## Slot 0 next to the post, then alternating +x / -x by spawn_spacing.
func _spawn_position(side: int, slot: int) -> Vector2:
	var base: Vector2 = layout.spawn_center(side)
	var offset: int = ceili(slot / 2.0)
	var direction: int = 1 if slot % 2 == 1 else -1
	return base + Vector2(direction * offset * rules.spawn_spacing, 0.0)


## What stops `team`'s players and throws: the street edges and every obstacle
## (cover is the same for both teams).
func blocking_rects_for_team(_team: int) -> Array[Rect2]:
	return _blocking


## Index of the obstacle at `point`, or -1.
func obstacle_at_point(point: Vector2) -> int:
	for index: int in obstacles.size():
		if obstacles[index].has_point(point):
			return index
	return -1


## Every player of `team` free and standing in the enemy base.
func team_in_enemy_base(team: int) -> bool:
	return objectives.team_in_enemy_base(self, team, _brains)


## Every player of the other team waiting in a box (a team of training dummies
## never scores this way).
func all_enemies_tagged(team: int) -> bool:
	return objectives.all_enemies_tagged(self, team, _brains)


## How long a capture has to hold right now: the capture time, or the overtime one once the round has dragged on.
func current_capture_time() -> float:
	if in_overtime():
		return minf(rules.capture_time, rules.overtime_capture_time)
	return rules.capture_time


## True once the round has dragged on past `rules.overtime_after` (the header says OVERTIME).
func in_overtime() -> bool:
	return rules.overtime_after > 0.0 and round_time >= rules.overtime_after


## `team` is all in the enemy base but a defender is still standing in it (the capture waits).
func base_contested_by(team: int) -> bool:
	return objectives.base_contested_by(self, team, _brains)


## Seconds left on the running capture countdown (0 when none runs).
func capture_time_left() -> float:
	return objectives.capture_time_left(self)
