class_name PracticeMatchPresentation
extends RefCounted
## Owns PracticeMatch's HUD wiring, actor views, camera presentation, and banners.

const AIRBORNE_LIFT: float = 1.0
const INTRO_SPLIT: float = 0.45
const INTRO_NEAR: float = 9.0
const INTRO_FAR: float = 13.0
const INTRO_LOW: float = 4.5
const INTRO_HIGH: float = 7.5
const INTRO_SIDE: float = 2.5
const INTRO_POST_TOP: float = 3.5

var owner: PracticeMatch


func _init(owner: PracticeMatch) -> void:
	self.owner = owner


func setup_hud() -> void:
	owner.input.camera_yaw = owner._camera.rules.yaw_offset_degrees
	Cinematic.add_lens(owner, owner._camera.rules.lens_k())
	Cinematic.add_film_finish(owner)
	owner.hud.overhead.setup(owner.sim, owner._camera, owner.local_id, owner.local_team)
	owner.hud.minimap.setup(owner.sim, owner.local_id, owner.local_team)
	owner.hud.set_top_inset(_safe_top_inset())


func connect_hud() -> void:
	owner.hud.joystick.changed.connect(owner.set_stick)
	for slot: int in owner.hud.aim_buttons.size():
		owner.hud.aim_buttons[slot].aim_started.connect(owner.aim_started.bind(slot))
		owner.hud.aim_buttons[slot].aim_released.connect(owner.aim_released.bind(slot))
	owner.hud.hurt_button.pressed.connect(owner.hurt_local.bind(PracticeMatch.DEBUG_DAMAGE))
	owner.hud.menu.set_practice(not owner.online)
	owner.hud.status_test_pressed.connect(owner.play_status_test)
	owner.hud.again_pressed.connect(_on_again_pressed)
	owner.hud.watch_requested.connect(func(id: int) -> void: owner.watch_id = id)
	owner.hud.swap_pressed.connect(owner.open_swap)
	owner.hud.exit_pressed.connect(owner._on_exit_pressed)


func connect_sim() -> void:
	owner.sim.player_damaged.connect(func(id: int, amount: int) -> void:
		if id == owner.local_id:
			owner._camera.shake(minf(0.1 + amount * 0.01, 0.4)))
	owner.sim.weapon_cast.connect(func(id: int, weapon: StringName) -> void:
		var def: WeaponDef = owner.sim.weapon_defs[weapon]
		(owner._views[id] as KidModel).play_cast(def.cast_lock, KidModel.is_spell(def)))
	owner.sim.player_damaged.connect(func(id: int, _amount: int) -> void: (owner._views[id] as KidModel).play_hit())
	owner.sim.point_scored.connect(_on_point_scored)
	owner.sim.match_won.connect(_on_match_won)
	owner.sim.point_reset.connect(func() -> void:
		owner.hud.joystick.release()
		owner.watch_id = -1
		owner._snap_camera = true)
	owner.sim.intro_finished.connect(func() -> void:
		owner._snap_camera = true
		owner.show_banner("GO!"))
	owner.sim.player_tagged.connect(_on_player_tagged)
	owner.sim.player_rescued.connect(_on_player_rescued)


func sync(delta: float) -> void:
	for id: int in owner.sim.players:
		_sync_actor(owner.sim.players[id], delta)
	var player: PlayerState = owner.sim.players[owner.local_id]
	owner.hud.sync_player(owner.sim, player, owner.watch_id, owner._pick_screen.visible)
	_sync_aim(player)
	owner._fx.sync(owner.sim, delta)
	owner.hud.overhead.tick(delta)
	owner.hud.sync_score(owner.sim, owner.local_team, delta)
	if owner.sim.phase == MatchSim.Phase.INTRO:
		_play_intro_shot()
		return
	owner.hud.show_intro("")
	var target: Vector2 = player.position
	if player.tagged and owner.sim.players.has(owner.watch_id):
		target = owner.sim.players[owner.watch_id].position
	owner._camera.follow(target, owner._flip(), 0.0 if owner._snap_camera else delta)
	owner._snap_camera = false


func make_actor_view(state: PlayerState) -> void:
	var team_color: Color = Palette.TEAM_ENEMY
	if state.id == owner.local_id:
		team_color = Palette.TEAM_OWN
	elif state.team == owner.local_team:
		team_color = Palette.TEAM_ALLY
	var character: CharacterDef = owner._characters.get(state.character_id) as CharacterDef
	if character == null:
		character = owner.rules.characters[0]
	var view: KidModel = KidModel.new()
	view.setup(character, team_color, owner.rules.player_radius)
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
		if id == owner.local_id:
			(owner._views[owner.local_id] as KidModel).footstep.connect(func() -> void: owner._audio.play(&"footstep", 0.15))
	sync(0.0)


func _sync_actor(state: PlayerState, delta: float) -> void:
	var view: KidModel = owner._views[state.id]
	var lift: float = AIRBORNE_LIFT if state.effects.has(StatusEffects.Type.AIRBORNE) else 0.0
	view.position = Vector3(state.position.x, lift, state.position.y)
	view.face(state.facing)
	view.animate(delta, minf(view.ground_speed(state.position, delta), 20.0), state)


func _sync_aim(player: PlayerState) -> void:
	var any_aiming: bool = false
	for slot: int in MatchInput.SLOTS:
		if player.tagged:
			break
		var stick: Vector2
		var cancelled: bool
		if owner.input.touch_held[slot]:
			stick = owner._screen_to_world(owner.hud.aim_buttons[slot].aim)
			cancelled = owner.hud.aim_buttons[slot].over_cancel
		elif owner.input.key_held[slot]:
			stick = owner.input.key_aim[slot]
			cancelled = owner.input.key_cancel[slot]
		else:
			continue
		if slot >= player.weapons.size():
			continue
		if player.aim_hold[slot] < 0.0 and not owner.sim.weapon_ready(player, slot):
			continue
		var def: WeaponDef = owner.sim.weapon_defs[player.weapons[slot]]
		var aim: Vector2 = owner.sim.resolved_aim(player, slot, stick)
		owner._aim_indicator.show_aim(
			owner.sim, player, def, aim, owner.sim.resolved_target(player, slot), cancelled
		)
		any_aiming = true
		break
	if not any_aiming:
		owner._aim_indicator.clear()
	owner.hud.cancel_zone.visible = owner.input.any_touch_held()


func _first_watch_id() -> int:
	var ids: Array[int] = TaggedPanel.cards_for(owner.sim, owner.sim.players[owner.local_id])
	for id: int in ids:
		if owner.sim.players[id].team == owner.local_team and not owner.sim.players[id].tagged:
			return id
	return ids[0] if not ids.is_empty() else -1


func _play_intro_shot() -> void:
	var length: float = maxf(owner.rules.intro_time, 0.001)
	var t: float = clampf(1.0 - owner.sim.intro_left / length, 0.0, 1.0)
	var split: float = INTRO_SPLIT
	var side: int = -owner.own_side() if t < split else owner.own_side()
	var u: float = t / split if t < split else (t - split) / (1.0 - split)
	var post: Vector2 = owner.layout.base_center(side)
	var to_field: Vector2 = -post.normalized()
	var across: Vector2 = to_field.orthogonal()
	var ground: Vector3 = Vector3(post.x, 0.0, post.y)
	var out: Vector3 = Vector3(to_field.x, 0.0, to_field.y)
	var eye: Vector3 = ground + out * lerpf(INTRO_NEAR, INTRO_FAR, u)
	eye += Vector3(across.x, 0.0, across.y) * INTRO_SIDE
	eye += Vector3.UP * lerpf(INTRO_LOW, INTRO_HIGH, u)
	var look: Vector3 = ground + Vector3.UP * lerpf(INTRO_POST_TOP, 0.8, minf(u * 1.6, 1.0))
	look += out * lerpf(0.5, 2.0, u)
	owner._camera.frame_shot(eye, look)
	var best_of: int = owner.sim.score.best_of
	owner.hud.show_intro(
		"TAG THEIR BASE!",
		"BO%d  •  FIRST TO %d" % [best_of, owner.sim.score.points_to_win()]
	)


func _on_player_tagged(id: int) -> void:
	owner.hud.feed(MatchHud.Feed.TAGGED, owner.sim.players[id].team == owner.local_team, _name_of(id))
	if id == owner.local_id:
		owner._camera.shake(0.25)
		owner.watch_id = _first_watch_id()


func _on_player_rescued(id: int, by_id: int) -> void:
	if by_id != MatchSim.RELEASED_ALONE:
		owner.hud.feed(MatchHud.Feed.RESCUED, owner.sim.players[id].team == owner.local_team, _name_of(id))
	if id == owner.local_id:
		owner.watch_id = -1
		owner._snap_camera = true
		if owner._pick_screen.visible:
			owner._pick_screen.force_finish()


func _name_of(id: int) -> String:
	if id == owner.local_id:
		return "YOU"
	var character: CharacterDef = owner._characters.get(owner.sim.players[id].character_id) as CharacterDef
	return character.display_name if character != null else "Player %d" % id


func _on_point_scored(team: int, _how: int) -> void:
	var points: Array[int] = owner.sim.score.points
	var mine: int = points[owner.local_team]
	var theirs: int = points[1 - owner.local_team]
	owner.show_banner("SCORED!  %d-%d" % [mine, theirs] if team == owner.local_team else "THEY SCORED  %d-%d" % [mine, theirs])


func _on_match_won(team: int) -> void:
	owner.show_banner("VICTORY!" if team == owner.local_team else "DEFEAT")
	var mode: String = "practice"
	if owner.online:
		mode = "hotspot" if Session.lobby_hotspot else "online"
	var points: Array[int] = owner.sim.score.points
	MatchHistory.record(mode, team == owner.local_team, points[owner.local_team], points[1 - owner.local_team])


func _on_again_pressed() -> void:
	if owner.online:
		owner.client.leave_room()
		owner.get_tree().change_scene_to_file("res://scenes/ui/lobby.tscn")
		return
	owner.get_tree().reload_current_scene()


func _safe_top_inset() -> float:
	var window: Vector2i = DisplayServer.window_get_size()
	if window.y <= 0:
		return 0.0
	var safe: Rect2i = DisplayServer.get_display_safe_area()
	var screen_inset: float = maxf(0.0, float(safe.position.y - DisplayServer.window_get_position().y))
	return screen_inset * owner.get_viewport().get_visible_rect().size.y / float(window.y)
