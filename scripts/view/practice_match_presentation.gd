class_name PracticeMatchPresentation
extends RefCounted
## Owns PracticeMatch's HUD wiring, actor views, camera presentation, and banners.

## A dive's dust (particles) at both ends, and the streak it leaves on the street: how wide, how high above it and its colour.
const DIVE_DUST: int = 6
const DIVE_STREAK_WIDTH: float = 0.7
const DIVE_STREAK_LIFT: float = 0.1
## The stars of a catch hang this high (m) over the one caught, and the screen shakes a little for the kid that caught or was caught.
const CATCH_STARS_HEIGHT: float = 1.4
const CATCH_SHAKE: float = 0.3
const DIVE_STREAK_COLOR: Color = Color(1.0, 1.0, 1.0, 0.55)

var owner: PracticeMatch


## The arena starts this far (UI pixels) below the header, so its shadow falls on the street's end and not on the play.
const HEADER_GAP: float = 12.0


func _init(owner: PracticeMatch) -> void:
	self.owner = owner


func setup_hud() -> void:
	owner.input.tap_time = owner.sim.rules.card_tap_time
	owner.input.follow_hold = owner.sim.rules.move_follow_hold
	owner.hud.console_height_changed.connect(func(height: float) -> void: owner._camera.bottom_reserve_px = height)
	owner.hud.side_reserve_changed.connect(func(width: float, left: bool) -> void:
		owner._camera.side_reserve_px = width
		owner._camera.side_reserve_left = left)
	owner.hud.header_height_changed.connect(func(height: float) -> void:
		owner._camera.top_reserve_px = height + HEADER_GAP
		owner.hud.overhead.top_limit = height)
	owner.hud.set_bottom_inset(_safe_bottom_inset())
	owner._camera.bottom_reserve_px = owner.hud.console_height()
	owner._camera.side_reserve_px = owner.hud.side_reserve()
	owner._camera.side_reserve_left = owner.hud.side_reserve_left()
	Cinematic.add_lens(owner, owner._camera.rules.lens_k())
	Cinematic.add_film_finish(owner)
	owner.hud.overhead.setup(owner.sim, owner._camera, owner.local_id, owner.local_team)
	owner.hud.overhead.blend = owner.blend
	owner.hud.overhead.view_scale = owner._camera.rules.kid_view_scale
	owner.hud.overhead.bar_scale = owner._camera.rules.bar_scale
	owner.hud.marker.setup(owner.sim, owner._camera, owner.local_id)
	owner._fx.view_scale = owner._camera.rules.kid_view_scale
	owner._fx.local_team = owner.local_team
	if owner.sim.mode != null and owner.sim.mode.is_free_for_all():
		owner.hud.setup_street_scores(owner.sim, owner.local_id)
	owner.hud.set_top_inset(_safe_top_inset())
	owner._camera.top_reserve_px = owner.hud.header_height() + HEADER_GAP
	owner.hud.overhead.top_limit = owner.hud.header_height()


func connect_hud() -> void:
	owner.hud.hand.card_pressed.connect(owner.card_pressed)
	owner.hud.hand.card_moved.connect(owner.card_moved)
	owner.hud.hand.card_released.connect(owner.card_released)
	owner.hud.hand.card_cancelled.connect(owner.card_cancelled)
	owner.hud.set_chat_enabled(true)
	owner.hud.chat.said.connect(func(text: String) -> void: owner.hud.overhead.say(owner.local_id, text))
	owner.hud.street_pad.knob_moved.connect(owner.knob_moved)
	owner.hud.street_pad.action_pressed.connect(owner.action_pressed)
	owner.hud.street_pad.aim_started.connect(owner.throw_pressed)
	owner.hud.street_pad.aim_dragged.connect(owner.throw_dragged)
	owner.hud.street_pad.aim_released.connect(owner.throw_released)
	owner.hud.street.move_pressed.connect(owner.move_pressed)
	owner.hud.street.move_dragged.connect(owner.move_dragged)
	owner.hud.street.move_released.connect(owner.move_released)
	owner.hud.menu.set_practice(not owner.online, Net.host != null, Session.practice_kind == &"match")
	owner.hud.again_button.text = "Return home" if owner.online else "Practice again"
	owner.hud.again_pressed.connect(_on_again_pressed)
	# online, the card's one button is already "Return home"; in practice Home sits beside Play again
	owner.hud.results.home_button.visible = not owner.online
	owner.hud.results.home_pressed.connect(owner._on_exit_pressed)
	owner.hud.results_mode = ("Local" if Session.lobby_hotspot else "Online") if owner.online else "Practice"
	owner.hud.swap_pressed.connect(owner.open_swap)
	owner.hud.exit_pressed.connect(owner._on_exit_pressed)


func connect_sim() -> void:
	owner.sim.player_damaged.connect(func(id: int, amount: int) -> void:
		if id == owner.local_id:
			owner._camera.shake(minf(0.1 + amount * 0.01, 0.4))
			owner.hud.pulse.hit(amount, owner.sim.rules.player_max_hp))
	owner.sim.weapon_cast.connect(func(id: int, weapon: StringName) -> void:
		var view: KidModel = owner._views[id] as KidModel
		if weapon == StreetMode.TAG_CAST:
			view.play_touch()  # a tag: the kid reaches out and touches
		else:
			view.play_cast(cast_lead(owner.sim, weapon)))
	owner.sim.match_won.connect(_on_match_won)
	owner.sim.point_reset.connect(func() -> void:
		owner.hud.street.release())
	owner.sim.intro_finished.connect(func() -> void:
		owner.show_banner("GO!")
		# the first practice match opens with the walkthrough (it teaches Agawan Base)
		if Session.coach_pending and not owner.online and owner.sim.mode == null:
			Session.coach_pending = false
			owner.hud.coach.start(owner.sim.rules.capture_solo_time, owner.sim.score.points_to_win()))
	owner.sim.player_tagged.connect(_on_player_tagged)
	owner.sim.player_rescued.connect(_on_player_rescued)
	if owner.sim.mode != null:
		_connect_street_game(owner.sim.mode)


## Tumbang Preso and Patintero say what happens in a banner: who is it or whose turn it is, a point, and (for you) what to do next.
func _connect_street_game(mode: StreetMode) -> void:
	mode.tagged.connect(_on_caught)
	mode.scored.connect(func(team: int, id: int) -> void:
		var by_touch: bool = not owner.sim.point_log.is_empty() and owner.sim.point_log[-1].y == MatchSim.CaptureKind.TOUCHED
		if by_touch:
			return  # the tag has its own banner
		if id == owner.local_id:
			owner.show_banner("+1 YOU SCORED!")
		elif not mode.is_free_for_all():
			owner.show_banner("+1 POINT!" if team == owner.local_team else "+1 THEM"))
	if mode is TumbangPreso:
		_connect_tumbang(mode as TumbangPreso)
	if mode is Patintero:
		var lines: Patintero = mode as Patintero
		lines.roles_swapped.connect(func(team: int) -> void: owner.show_banner("YOU RUN!" if team == owner.local_team else "YOU GUARD!"))
		lines.runner_crossed.connect(func(id: int) -> void:
			if id == owner.local_id:
				owner.show_banner("PAST THE LAST GUARD! RUN BACK!"))
		lines.tagged.connect(func(guard: int, runner: int) -> void:
			if runner == owner.local_id:
				owner.show_banner("TAGGED!")
			elif guard == owner.local_id:
				owner.show_banner("YOU TAGGED A RUNNER! +1")
			else:
				owner.show_banner("RUNNER TAGGED!" if owner.sim.players[guard].team != owner.local_team else "RUNNER TAGGED! +1"))
		lines.dived.connect(_on_dived)


## A kid was caught (a tag): it stumbles and falls while everyone stands still, with stars and dust where it was touched. The one that
## touched it has played its touch (or is finishing its dive: the catch is the dive's end).
func _on_caught(tagger: int, target: int) -> void:
	var view: KidModel = owner._views.get(target) as KidModel
	var pause: float = owner.sim.mode.rules.reset_seconds
	if view != null:
		view.play_tripped(pause)
	var at: Vector2 = owner.sim.players[target].position
	var context: FxContext = owner._fx.context()
	context.particles.puff(FxParticles.Preset.STARS, Vector3(at.x, CATCH_STARS_HEIGHT, at.y), FxBursts.STARS)
	context.particles.puff(FxParticles.Preset.DUST, Vector3(at.x, 0.0, at.y), DIVE_DUST)
	if tagger == owner.local_id or target == owner.local_id:
		owner._camera.shake(CATCH_SHAKE)


## A kid dives (Patintero): it crouches, leaps, slides on its belly and gets up (`KidModel.play_dive`, `time` s of burst and `recover` s of
## lying and rising), with dust where it leaves the ground and where the burst ends and a streak on the street between them.
func _on_dived(id: int, way: Vector2, time: float, recover: float) -> void:
	var view: KidModel = owner._views.get(id) as KidModel
	if view != null:
		view.play_dive(time + recover)
	var state: PlayerState = owner.sim.players[id]
	var context: FxContext = owner._fx.context()
	var lines: Patintero = owner.sim.mode as Patintero
	var distance: float = lines.rules.dive_speed_share * owner.sim.rules.move_speed * time
	var from: Vector2 = state.position
	context.particles.puff(FxParticles.Preset.DUST, Vector3(from.x, 0.0, from.y), DIVE_DUST)
	var to: Vector2 = from + way * distance
	context.ground.trail(Vector3(from.x, DIVE_STREAK_LIFT, from.y), Vector3(to.x, DIVE_STREAK_LIFT, to.y), DIVE_STREAK_WIDTH, DIVE_STREAK_COLOR, time)
	owner.get_tree().create_timer(time).timeout.connect(func() -> void:
		if is_instance_valid(owner) and owner.sim != null and owner.sim.players.has(id):
			var landed: Vector2 = owner.sim.players[id].position
			context.particles.puff(FxParticles.Preset.DUST, Vector3(landed.x, 0.0, landed.y), DIVE_DUST))


func _connect_tumbang(game: TumbangPreso) -> void:
	game.taya_changed.connect(func(_old: int, new_taya: int, why: StringName) -> void:
		var scared: String = "TOO SCARED! " if why == &"scared" else ""
		owner.show_banner(scared + ("YOU'RE IT!" if new_taya == owner.local_id else "%s IS IT!" % owner.hud.overhead.name_of(owner.sim.players[new_taya]).to_upper())))
	game.slipper_landed.connect(func(id: int, _at: Vector2, _hit: bool) -> void:
		if id == owner.local_id:
			owner.show_banner("MISSED! FETCH YOUR SLIPPER!"))
	game.slipper_flung.connect(func(id: int, _from: Vector2, path: Array[Vector2]) -> void:
		if id == owner.local_id:
			owner.show_banner("CAN DOWN! FETCH YOUR SLIPPER!" if path.size() == 1 else "BOUNCED! FETCH YOUR SLIPPER!")
		elif game.taya_id == owner.local_id:
			owner.show_banner("CAN DOWN! PUT IT BACK!"))
	game.can_kicked.connect(func(id: int) -> void:
		if id == owner.local_id:
			owner.show_banner("KICKED!")
		elif game.taya_id == owner.local_id:
			owner.show_banner("THEY KICKED THE CAN!"))
	game.slipper_fetched.connect(func(id: int) -> void:
		if id == owner.local_id:
			owner.show_banner("RUN TO THE OTHER END!"))
	game.can_placed.connect(func(_id: int) -> void: owner.show_banner("THE CAN IS UP!"))


## What your kid is to do now, for the line on the pad and the opening show: [what, how].
static func street_status(sim: MatchSim, player: PlayerState) -> Array[String]:
	var mode: StreetMode = sim.mode
	if mode is TumbangPreso:
		var game: TumbangPreso = mode as TumbangPreso
		var id: int = player.id
		if id == game.taya_id:
			return ["YOU'RE IT!", "Move into a thrower's circle to tag it!" if game.can_up else "Fetch the can and put it back!"] as Array[String]
		if game.flights.has(id):
			return ["", ""] as Array[String]
		if game.fetched.has(id):
			return ["RUN TO THE OTHER END!", ""] as Array[String]
		if game.has_slipper(id):
			return ["YOUR THROW!", "Hold THROW to aim, let go to throw."] as Array[String]
		if game.scared_left.has(id) and player.position.y >= game.rules.home_z:
			return ["FETCH IT OR YOU'RE IT!", UiText.f("Step into the fetch area: %d", [ceili(game.scared_left[id])])] as Array[String]
		return ["FETCH YOUR SLIPPER!", "Kick the can to keep the taya busy."] as Array[String]
	var lines: Patintero = mode as Patintero
	if lines.is_attacker(player):
		return ["YOU RUN!", "Past the last guard, then back!"] as Array[String]
	return ["YOU GUARD!", "Move into a runner's circle to tag!"] as Array[String]


## How long before its release a kid's cast clip starts for `weapon`: a delayed cone (the big snip) strikes `delay` seconds
## after the cast, so the hand comes down on the strike; everything else releases on the cast tick.
static func cast_lead(sim: MatchSim, weapon: StringName) -> float:
	var def: WeaponDef = sim.weapon_defs.get(weapon) as WeaponDef
	if def != null and def.shape == WeaponDef.Shape.CONE and def.delay > 0.0:
		return def.delay
	return 0.0


func sync(delta: float) -> void:
	for id: int in owner.sim.players:
		_sync_actor(owner.sim.players[id], delta)
	if owner.sim.phase in [MatchSim.Phase.POINT_FREEZE, MatchSim.Phase.MATCH_OVER] and owner._pick_screen.visible:
		owner._pick_screen.close()
	var player: PlayerState = owner.sim.players[owner.local_id]
	owner.hud.sync_player(owner.sim, player, owner._pick_screen.visible)
	_sync_aim(player)
	owner._fx.sync(owner.sim, delta)
	owner._map.sync_rescue(owner.sim)
	owner.mode_view.sync(delta)
	owner.hud.overhead.tick(delta)
	owner.hud.marker.tick(delta)
	owner.hud.sync_score(owner.sim, owner.local_team, delta, owner.local_id)
	owner._camera.place(owner._flip())
	if owner.sim.phase == MatchSim.Phase.INTRO:
		_play_intro_shot()
		return
	owner.hud.show_intro("")


func make_actor_view(state: PlayerState) -> void:
	var character: CharacterDef = owner._characters.get(state.character_id) as CharacterDef
	if character == null:
		character = owner.rules.characters[0]
	var view: KidModel = KidModel.new()
	view.setup(character)
	view.set_team_outline(TeamColors.world_own() if state.team == owner.local_team else TeamColors.world_enemy())
	view.scale = Vector3.ONE * owner._camera.rules.kid_view_scale
	owner._actors.add_child(view)
	owner._views[state.id] = view


func refresh_kids() -> void:
	for id: int in owner.sim.players:
		var view: KidModel = owner._views[id] as KidModel
		if view != null and view.character.id == owner.sim.players[id].character_id:
			continue
		if view != null:
			view.queue_free()
		make_actor_view(owner.sim.players[id])
	sync(0.0)


func _sync_actor(state: PlayerState, delta: float) -> void:
	var view: KidModel = owner._views[state.id]
	# the kid's own Airborne clip already leaves the ground: no extra lift
	var at: Vector2 = owner.blend.kid(state)
	view.position = Vector3(at.x, 0.0, at.y)
	view.face(state.facing)
	view.animate(delta, minf(view.ground_speed(state.position, delta), 20.0), state)


## The aim guide and the hint for the held card: before it leaves its card only the rings a tap would reach; over the street the
## weapon's shape toward the street point under the finger, clamped to its range from where the kid stands now; back over the hand
## the last shape in red (letting go there cancels).
func _sync_aim(player: PlayerState) -> void:
	var input: MatchInput = owner.input
	var slot: int = input.held_slot
	var playing: bool = owner.hud.street.active
	if not playing:
		owner._aim_indicator.clear()
		owner.hud.sync_hold_hint(player, false, MatchInput.Release.NONE, false)
		return
	var release: MatchInput.Release = input.release_kind(Time.get_ticks_msec())
	var nobody: bool = false
	if slot < 0 or player.tagged or slot >= player.weapons.size() or (player.aim_hold[slot] < 0.0 and not owner.sim.weapon_ready(player, slot)):
		owner._aim_indicator.clear()
		owner.hud.sync_hold_hint(player, playing, release if slot >= 0 else MatchInput.Release.NONE, false)
		return
	var def: WeaponDef = owner.sim.weapon_defs[player.weapons[slot]]
	var hold: float = maxf(player.aim_hold[slot], 0.0)
	if not input.left_card:
		owner._aim_indicator.show_aim(owner.sim, player, def, owner.sim.resolved_aim(player, slot, Vector2.ZERO),
			owner.sim.resolved_target(player, slot), false, hold, false)
		nobody = owner.sim.lacks_target(player, slot)
	elif not input.aimed:
		owner._aim_indicator.clear()  # dragged along the hand, never onto the street: nothing to show yet
	else:
		var aim: Vector2 = owner.sim.aim_from_point(player, def, input.aim_point)
		var target: int = owner.sim.target_near_point(player, def, input.aim_point) if def.shape == WeaponDef.Shape.TARGETED else -1
		owner._aim_indicator.show_aim(owner.sim, player, def, aim, target, not input.over_street, hold, true)
		nobody = not owner.sim.weapons.would_cast(owner.sim, player, def, aim, target)
	owner.hud.sync_hold_hint(player, playing, release, nobody)


func _play_intro_shot() -> void:
	var mode: StreetMode = owner.sim.mode
	if mode != null:
		var skip: String = "" if owner.online else " · TAP TO SKIP"
		owner.hud.show_intro(street_status(owner.sim, owner.sim.players[owner.local_id])[0], "%d MIN · MOST POINTS WIN%s" % [roundi(mode.rules.match_seconds / 60.0), skip])
		owner._camera.place(owner._flip())
		return
	var first_to: int = owner.sim.score.first_to
	var sub: String = "FIRST TO %d" % first_to
	if not owner.online:
		sub += " · TAP TO SKIP"
	owner.hud.show_intro("TAG THEIR BASE!", sub)
	# no fly-through of the two teams (owner): the fixed street, with the objective on it
	owner._camera.place(owner._flip())


func _on_player_tagged(id: int) -> void:
	# The sim has already moved the kid to the sidewalk; keep the last visible street position.
	var view: KidModel = owner._views.get(id) as KidModel
	if view != null:
		owner.hud.overhead.show_tag(id, view.position)
	if id == owner.local_id:
		owner._camera.shake(0.25)
		owner.hud.pulse.tagged()


func _on_player_rescued(id: int, _by_id: int) -> void:
	if id == owner.local_id:
		if owner._pick_screen.visible:
			owner._pick_screen.force_finish()


func _on_match_won(team: int) -> void:
	var mode: String = "practice"
	if owner.online:
		mode = "hotspot" if Session.lobby_hotspot else "online"
	var points: Array[int] = owner.sim.score.points
	if owner.sim.mode != null:
		var result: Dictionary = owner.sim.mode.local_result(owner.sim, owner.local_id)
		MatchHistory.record(mode, result["won"] as bool, result["mine"] as int, result["theirs"] as int)
		return
	MatchHistory.record(mode, team == owner.local_team, points[owner.local_team], points[1 - owner.local_team])


func _on_again_pressed() -> void:
	if owner.online:
		owner._on_exit_pressed()
		return
	owner.get_tree().reload_current_scene()


## The part of the screen's bottom edge the system keeps (a home bar), in UI pixels; 0 on a phone without one.
func _safe_bottom_inset() -> float:
	var window: Vector2i = DisplayServer.window_get_size()
	var safe: Rect2i = DisplayServer.get_display_safe_area()
	if window.y <= 0 or safe.size.y <= 0:
		return 0.0
	var below: float = maxf(0.0, float(DisplayServer.window_get_position().y + window.y - safe.end.y))
	return below * owner.get_viewport().get_visible_rect().size.y / float(window.y)


func _safe_top_inset() -> float:
	var window: Vector2i = DisplayServer.window_get_size()
	if window.y <= 0:
		return 0.0
	var safe: Rect2i = DisplayServer.get_display_safe_area()
	var screen_inset: float = maxf(0.0, float(safe.position.y - DisplayServer.window_get_position().y))
	return screen_inset * owner.get_viewport().get_visible_rect().size.y / float(window.y)
