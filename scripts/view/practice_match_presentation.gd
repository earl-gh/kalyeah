class_name PracticeMatchPresentation
extends RefCounted
## Owns PracticeMatch's HUD wiring, actor views, camera presentation, and banners.

var owner: PracticeMatch


## The arena starts this far (UI pixels) below the header, so its shadow falls on the street's end and not on the play.
const HEADER_GAP: float = 12.0


func _init(owner: PracticeMatch) -> void:
	self.owner = owner


func setup_hud() -> void:
	owner.input.tap_time = owner.sim.rules.card_tap_time
	owner.input.follow_hold = owner.sim.rules.move_follow_hold
	owner.hud.console_height_changed.connect(func(height: float) -> void: owner._camera.bottom_reserve_px = height)
	owner.hud.header_height_changed.connect(func(height: float) -> void:
		owner._camera.top_reserve_px = height + HEADER_GAP
		owner.hud.overhead.top_limit = height)
	owner.hud.set_bottom_inset(_safe_bottom_inset())
	owner._camera.bottom_reserve_px = owner.hud.console_height()
	Cinematic.add_lens(owner, owner._camera.rules.lens_k())
	Cinematic.add_film_finish(owner)
	owner.hud.overhead.setup(owner.sim, owner._camera, owner.local_id, owner.local_team)
	owner.hud.overhead.view_scale = owner._camera.rules.kid_view_scale
	owner.hud.overhead.bar_scale = owner._camera.rules.bar_scale
	owner.hud.marker.setup(owner.sim, owner._camera, owner.local_id)
	owner._fx.view_scale = owner._camera.rules.kid_view_scale
	owner._fx.local_team = owner.local_team
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
	owner.sim.weapon_cast.connect(func(id: int, _weapon: StringName) -> void: (owner._views[id] as KidModel).play_cast())
	owner.sim.player_damaged.connect(func(id: int, _amount: int) -> void: (owner._views[id] as KidModel).play_hit())
	owner.sim.match_won.connect(_on_match_won)
	owner.sim.point_reset.connect(func() -> void:
		owner.hud.street.release())
	owner.sim.intro_finished.connect(func() -> void:
		# the first practice match opens with the walkthrough
		if Session.coach_pending and not owner.online:
			Session.coach_pending = false
			owner.hud.coach.start(owner.sim.rules.capture_solo_time, owner.sim.score.points_to_win()))
	owner.sim.player_tagged.connect(_on_player_tagged)
	owner.sim.player_rescued.connect(_on_player_rescued)


func sync(delta: float) -> void:
	for id: int in owner.sim.players:
		_sync_actor(owner.sim.players[id], delta)
	if owner.sim.phase in [MatchSim.Phase.POINT_FREEZE, MatchSim.Phase.MATCH_OVER] and owner._pick_screen.visible:
		owner._pick_screen.close()
	var player: PlayerState = owner.sim.players[owner.local_id]
	owner.hud.sync_player(owner.sim, player, owner._pick_screen.visible)
	_sync_aim(player)
	owner._fx.sync(owner.sim, delta)
	owner.hud.overhead.tick(delta)
	owner.hud.marker.tick(delta)
	owner.hud.sync_score(owner.sim, owner.local_team, delta, owner.local_id)
	owner._camera.place(owner._flip())
	var bounds: Rect2 = Rect2()
	var first: bool = true
	for x: float in [-owner.layout.lane_width / 2.0, owner.layout.lane_width / 2.0]:
		for z: float in [-owner.layout.lane_length / 2.0, owner.layout.lane_length / 2.0]:
			var at: Vector2 = owner._camera.screen_position(Vector3(x, 0.0, z))
			bounds = Rect2(at, Vector2.ZERO) if first else bounds.expand(at)
			first = false
	owner.hud.scoreboard.set_play_area(bounds)
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
	view.position = Vector3(state.position.x, 0.0, state.position.y)
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
	var first_to: int = owner.sim.score.first_to
	var sub: String = "FIRST TO %d" % first_to
	if not owner.online:
		sub += " · TAP TO SKIP"
	owner.hud.show_intro("TAG THEIR BASE!", sub)
	# no fly-through of the two teams (owner): the fixed street, with the objective on it
	owner._camera.place(owner._flip())


func _on_player_tagged(id: int) -> void:
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
