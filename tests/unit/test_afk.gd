extends GutTest
## A player gone from a running match for `afk_after` seconds is away (AFK). A team that is all away forfeits: in a 1v1
## the match ends with the opponent winning; in a bigger match a single away player is only marked.

const RULES: GameRules = preload("res://data/rules/game_rules.tres")
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
const NET: NetRules = preload("res://data/rules/net_rules.tres")
const DT: float = 1.0 / 30.0


func _service() -> RoomService:
	return RoomService.new(NET, 7)


## A started room of `team_size` a side, with peers 1, 2, ... in it.
func _running_room(service: RoomService, team_size: int) -> RoomService.Room:
	var room: RoomService.Room = service.create_room(1, "P1", team_size)
	for peer: int in range(2, team_size * 2 + 1):
		service.join(room.code, peer, "P%d" % peer)
	room.state = RoomService.State.PLAYING
	return room


func _playing(room: RoomService.Room) -> ServerMatch:
	var running: ServerMatch = ServerMatch.new(null, room, RULES, LAYOUT, NET, 5)
	running.stage = ServerMatch.Stage.PLAYING
	running.sim.begin_intro()
	return running


func test_thirty_seconds_is_the_rule() -> void:
	assert_eq(NET.afk_after, 30.0)
	assert_lt(NET.afk_after, NET.reconnect_window, "an away player can still come back")


func test_a_player_gone_too_long_from_a_running_match_is_away_once() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = _running_room(service, 1)
	var gone: RoomService.Member = room.members[1]
	service.disconnected(2)
	assert_eq(service.step(NET.afk_after - 1.0).size(), 0, "not yet")
	assert_false(gone.afk)
	var changed: Array[RoomService.Room] = service.step(1.5)
	assert_eq(changed.size(), 1, "the room changed: the others are told")
	assert_true(gone.afk)
	assert_true(((service.describe(room)["members"] as Array)[1] as Dictionary)["afk"] as bool)
	assert_eq(service.step(1.0).size(), 0, "said once")


func test_coming_back_clears_away_and_starts_the_count_again() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = _running_room(service, 1)
	var gone: RoomService.Member = room.members[1]
	service.disconnected(2)
	service.step(NET.afk_after + 1.0)
	assert_not_null(service.reconnect(gone.token, 9))
	assert_false(gone.afk)
	service.disconnected(9)
	service.step(NET.afk_after - 1.0)
	assert_false(gone.afk, "a fresh 30 s after the next drop")


func test_a_forfeit_ends_the_match_and_the_other_team_wins() -> void:
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, 1)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	watch_signals(sim)
	sim.forfeit(1)
	assert_eq(sim.phase, MatchSim.Phase.MATCH_OVER)
	assert_eq(sim.score.winner, 0)
	assert_signal_emitted_with_parameters(sim, "match_won", [0])
	sim.forfeit(0)
	assert_eq(sim.score.winner, 0, "a match that is over stays decided")


func test_a_1v1_ends_when_the_opponent_is_away() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = _running_room(service, 1)
	var running: ServerMatch = _playing(room)
	room.members[1].connected = false
	running.poll(DT)
	assert_ne(running.sim.phase, MatchSim.Phase.MATCH_OVER, "a drop alone does not end it")
	room.members[1].afk = true
	running.poll(DT)
	assert_eq(running.sim.phase, MatchSim.Phase.MATCH_OVER)
	assert_eq(running.sim.score.winner, room.members[0].team)


func test_a_2v2_goes_on_with_one_player_away_and_ends_when_a_whole_team_is() -> void:
	var service: RoomService = _service()
	var room: RoomService.Room = _running_room(service, 2)
	var running: ServerMatch = _playing(room)
	var team: int = room.members[0].team
	var mates: Array[RoomService.Member] = []
	for m: RoomService.Member in room.members:
		if m.team == team:
			mates.append(m)
	mates[0].connected = false
	mates[0].afk = true
	running.poll(DT)
	assert_ne(running.sim.phase, MatchSim.Phase.MATCH_OVER, "one away player is only marked")
	mates[1].connected = false
	mates[1].afk = true
	running.poll(DT)
	assert_eq(running.sim.phase, MatchSim.Phase.MATCH_OVER, "nobody left on that team")
	assert_eq(running.sim.score.winner, 1 - team)
