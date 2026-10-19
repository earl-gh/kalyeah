extends GutTest

var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
## A kid whose animation is paced by the game (Junjun plays his own clips at their own speed).
const PACED: int = 1


func _kid(index: int) -> KidModel:
	var kid: KidModel = autofree(KidModel.new()) as KidModel
	kid.setup(RULES.characters[index], Palette.TEAM_OWN, RULES.player_radius)
	add_child(kid)
	return kid


func test_each_kid_has_their_own_outfit() -> void:
	var seen: Dictionary = {}
	for i: int in RULES.characters.size():
		var def: CharacterDef = RULES.characters[i]
		var key: String = "%d-%d-%d-%d" % [def.hair, def.top, def.bottom, def.extra]
		assert_false(seen.has(key), "%s looks different from the others" % def.display_name)
		seen[key] = true
	var inday: CharacterDef = RULES.characters[5]
	assert_lt(inday.skin.get_luminance(), RULES.characters[1].skin.get_luminance(), "varied Filipino skin tones")


func test_kid_turns_into_a_can_and_greys_out() -> void:
	var kid: KidModel = _kid(0)
	var state: PlayerState = PlayerState.new()
	state.effects.apply(StatusEffects.Type.POLYMORPH, 1.0)
	kid.animate(0.1, 0.0, state)
	assert_true(kid.is_can())
	state.effects.clear()
	state.tagged = true
	kid.animate(0.1, 0.0, state)
	assert_false(kid.is_can())
	assert_true(kid.is_ghost(), "tagged kids wait greyed out in the box")


func test_kid_faces_its_movement() -> void:
	var kid: KidModel = _kid(1)
	kid.face(Vector2(1.0, 0.0))
	var forward: Vector3 = -kid.global_transform.basis.z
	assert_almost_eq(forward.x, 1.0, 0.001)


func test_every_sound_is_built_and_not_silent() -> void:
	for id: StringName in SoundBank.ids():
		var stream: AudioStreamWAV = SoundBank.get_sound(id)
		assert_gt(stream.data.size(), 400, String(id))
		var loudest: int = 0
		for i: int in range(0, stream.data.size(), 64):
			loudest = maxi(loudest, absi(stream.data.decode_s16(i)))
		assert_gt(loudest, 1000, "%s is audible" % id)


func test_music_renders_in_chunks_and_loops() -> void:
	var renderer: SoundBank.MusicRenderer = SoundBank.MusicRenderer.new()
	var chunks: int = 0
	while not renderer.step(20000):
		chunks += 1
	assert_gt(chunks, 3, "built over several frames, not one")
	var stream: AudioStreamWAV = renderer.stream()
	assert_eq(stream.loop_mode, AudioStreamWAV.LOOP_FORWARD)
	assert_almost_eq(float(stream.data.size()) / 2.0 / stream.mix_rate, 8.0 * 4.0 * 60.0 / SoundBank.TEMPO, 0.05)


func test_match_audio_follows_sim_events() -> void:
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, 1)
	var audio: MatchAudio = autofree(MatchAudio.new()) as MatchAudio
	add_child(audio)
	audio.watch(sim, 0)
	sim.point_scored.emit(0, MatchSim.CaptureKind.BASE)
	sim.point_scored.emit(1, MatchSim.CaptureKind.ALL_TAGGED)
	sim.player_tagged.emit(3)
	sim.player_rescued.emit(3, 4)
	assert_eq(audio.played, [&"point", &"lose_point", &"ko", &"revive"] as Array[StringName])


func test_the_ui_theme_is_applied_everywhere() -> void:
	var button: Button = autofree(Button.new()) as Button
	add_child(button)
	var box: StyleBoxFlat = button.get_theme_stylebox("normal") as StyleBoxFlat
	assert_not_null(box, "every button gets the raised Kalyeah action style")
	assert_eq(box.bg_color, KalyeahTheme.AMBER)
	assert_eq(button.get_theme_font("font"), KalyeahTheme.BODY_FONT, "actions use the readable body face")
	assert_eq(KalyeahTheme.build().default_font, KalyeahTheme.BODY_FONT, "body copy uses Source Sans 3")
	assert_eq((KalyeahTheme.BODY_FONT as FontVariation).base_font, KalyeahTheme.SOURCE_SANS)
	assert_eq(KalyeahTheme.DISPLAY_FONT.get_font_name(), "Bungee", "headings use Bungee")
	var field: LineEdit = autofree(LineEdit.new()) as LineEdit
	add_child(field)
	var normal: StyleBoxFlat = field.get_theme_stylebox("normal") as StyleBoxFlat
	var focused: StyleBoxFlat = field.get_theme_stylebox("focus") as StyleBoxFlat
	assert_not_null(normal)
	assert_not_null(focused)
	assert_ne(normal.border_color, focused.border_color, "fields have a visible focus state")
	assert_eq(field.get_theme_font("font"), KalyeahTheme.BODY_FONT, "fields use the readable body face")


func _animation_after(kid: KidModel, state: PlayerState, speed: float = 0.0) -> String:
	kid.animate(0.05, speed, state)
	return kid.current_animation()


func test_each_state_plays_its_own_animation() -> void:
	var kid: KidModel = _kid(3)  # Toni: his model has every clip, including the landing squash
	var state: PlayerState = PlayerState.new()
	for i: int in 10:
		kid.animate(0.05, 5.0, state)
	assert_eq(kid.pose, "run")
	assert_eq(kid.current_animation(), "run")
	kid.play_cast()
	assert_eq(_animation_after(kid, state), "run", "a cast on the move keeps the legs running")
	assert_eq(kid.pose, "cast")
	for i: int in 20:
		kid.animate(0.05, 0.0, state)
	kid.play_cast()
	assert_eq(_animation_after(kid, state), "cast", "standing still: the whole body throws")
	state = PlayerState.new()
	state.effects.apply(StatusEffects.Type.STUN, 2.0)
	assert_eq(_animation_after(kid, state), "stun", "stars")
	state = PlayerState.new()
	for i: int in 20:
		kid.animate(0.05, 0.0, state)  # the cast is over
	state.effects.apply(StatusEffects.Type.AIRBORNE, 2.0)
	assert_eq(_animation_after(kid, state), "airborne", "flailing in the air")
	assert_true((kid.find_child("Stars", true, false) as Node3D).visible, "dizzy stars in the air too")
	state.effects.clear()
	assert_eq(_animation_after(kid, state), "land", "a landing squash when back on the ground")
	state = PlayerState.new()
	state.tagged = true
	assert_eq(_animation_after(kid, state), "idle", "tagged kids stand in the box")


func test_a_knockout_falls_then_lies() -> void:
	var kid: KidModel = _kid(1)
	var state: PlayerState = PlayerState.new()
	state.effects.apply(StatusEffects.Type.KNOCKOUT, 3.0)
	assert_eq(_animation_after(kid, state), "ko_stagger")
	kid.animate(KidModel.KO_FALL_TIME, 0.0, state)
	assert_eq(kid.current_animation(), "ko_lying")
	assert_eq(kid.pose, "ko")
	state.effects.clear()
	assert_eq(_animation_after(kid, state), "idle", "gets back up")


func test_every_weapon_has_its_own_cast_sound() -> void:
	for i: int in RULES.weapons.size():
		var id: StringName = RULES.weapons[i].id
		assert_ne(SoundBank.cast_id(id), &"cast", "%s has a specific cast sound" % id)
		assert_gt(SoundBank.get_sound(SoundBank.cast_id(id)).data.size(), 200)
	assert_eq(SoundBank.cast_id(&"mystery"), &"cast", "unknown weapons fall back")


func test_match_audio_has_sounds_for_casts_heals_effects_and_the_result() -> void:
	var sim: MatchSim = MatchSim.new(RULES, LAYOUT, 1)
	var audio: MatchAudio = autofree(MatchAudio.new()) as MatchAudio
	add_child(audio)
	audio.watch(sim, 0, 1)
	sim.weapon_cast.emit(1, &"lata")
	sim.player_healed.emit(1, 50)
	sim.effect_applied.emit(2, StatusEffects.Type.STUN)
	sim.effect_applied.emit(2, StatusEffects.Type.POLYMORPH)
	sim.player_rescued.emit(1, MatchSim.RELEASED_ALONE)
	sim.player_rescued.emit(2, MatchSim.RELEASED_ALONE)
	sim.match_won.emit(0)
	sim.match_won.emit(1)
	assert_eq(audio.played, [&"cast_lata", &"heal", &"stun", &"poof", &"revive", &"victory", &"defeat"] as Array[StringName], "a dummy walking out alone is quiet")


func test_running_makes_footstep_signals() -> void:
	var kid: KidModel = _kid(PACED)
	var state: PlayerState = PlayerState.new()
	watch_signals(kid)
	for i: int in 60:
		kid.animate(0.03, 5.0, state)
	# 1.8 s at 5 m/s = 9 m; two footfalls per 1.6 m stride
	assert_between(get_signal_emit_count(kid, "footstep"), 10, 12)


func test_camera_shake_kicks_then_settles() -> void:
	var camera: FollowCamera = autofree(FollowCamera.new()) as FollowCamera
	camera.rules = load("res://data/rules/camera_rules.tres") as CameraRules
	camera.layout = LAYOUT
	add_child(camera)
	camera.shake(0.3)
	camera._process(0.016)
	assert_ne(camera.h_offset, 0.0)
	for i: int in 60:
		camera._process(0.05)
	assert_eq(camera.h_offset, 0.0)


func test_every_skill_has_painted_art() -> void:
	for i: int in RULES.weapons.size():
		var def: WeaponDef = RULES.weapons[i]
		assert_not_null(Icons.art(StringName("btn_" + String(def.id))), String(def.id))
	for id: StringName in [&"gear"]:
		assert_not_null(Icons.art(id), String(id))


func test_a_moving_kid_keeps_running_between_the_30_hz_sim_steps() -> void:
	var kid: KidModel = _kid(0)
	var position: Vector2 = Vector2.ZERO
	var state: PlayerState = PlayerState.new()
	var run_frames: int = 0
	# 60 fps drawing, the sim moves the kid 5 m/s in 1/30 s steps (every other frame)
	for frame: int in 60:
		if frame % 2 == 0:
			position += Vector2(0.0, -5.0 / 30.0)
		var speed: float = kid.ground_speed(position, 1.0 / 60.0)
		kid.animate(1.0 / 60.0, speed, state)
		if frame > 12 and kid.current_animation() == "run":
			run_frames += 1
	assert_eq(run_frames, 47, "run on every drawn frame, never back to idle")
	for frame: int in 20:
		kid.animate(1.0 / 60.0, kid.ground_speed(position, 1.0 / 60.0), state)
	assert_eq(kid.current_animation(), "idle", "standing still again once the movement stops")


func test_every_kid_is_the_same_3d_model_at_one_height() -> void:
	for i: int in RULES.characters.size():
		var kid: KidModel = _kid(i)
		var model: Node3D = kid.find_child("Model", true, false) as Node3D
		assert_not_null(model, RULES.characters[i].display_name)
		var mesh: MeshInstance3D = model.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
		assert_almost_eq(mesh.get_aabb().size.y * model.scale.y, KidModel.KID_HEIGHT, 0.01, "one kid height")
		assert_null(kid.find_child("Sprite", true, false), "no sprite: a real model")


func test_the_model_has_every_animation_and_loops_only_the_continuous_ones() -> void:
	var player: AnimationPlayer = _kid(0).find_child("AnimationPlayer", true, false) as AnimationPlayer
	for clip: String in ["idle", "run", "cast", "stun", "ko"]:
		assert_true(player.has_animation(clip), clip)
	for clip: String in ["idle", "run", "stun"]:
		assert_eq(player.get_animation(clip).loop_mode, Animation.LOOP_LINEAR, "%s loops" % clip)
	for clip: String in ["cast", "ko"]:
		assert_eq(player.get_animation(clip).loop_mode, Animation.LOOP_NONE, "%s plays once" % clip)


func test_the_model_gets_the_matte_look_and_a_tint() -> void:
	var kid: KidModel = _kid(0)
	var mesh: MeshInstance3D = kid.find_children("*", "MeshInstance3D", true, false).filter(func(n: Node) -> bool: return n.name == "Mesh0")[0] as MeshInstance3D
	var material: StandardMaterial3D = mesh.get_surface_override_material(0) as StandardMaterial3D
	assert_not_null(material)
	assert_eq(material.roughness, 1.0)
	assert_eq(material.metallic, 0.0)
	assert_false(material.normal_enabled)
	assert_eq(material.diffuse_mode, BaseMaterial3D.DIFFUSE_LAMBERT_WRAP)
	assert_true(material.rim_enabled)
	var state: PlayerState = PlayerState.new()
	kid.animate(0.05, 0.0, state)
	assert_almost_eq(material.albedo_color.r, ModelLook.ALBEDO_GAIN, 0.001, "untinted")
	state.tagged = true
	kid.animate(0.05, 0.0, state)
	assert_lt(material.albedo_color.r, ModelLook.ALBEDO_GAIN * 0.6, "greyed out in the box")


func test_the_run_cycle_follows_the_ground_speed() -> void:
	var kid: KidModel = _kid(PACED)
	var player: AnimationPlayer = kid.find_child("AnimationPlayer", true, false) as AnimationPlayer
	var state: PlayerState = PlayerState.new()
	for i: int in 10:
		kid.animate(0.05, 2.5, state)
	var slow: float = player.speed_scale
	for i: int in 10:
		kid.animate(0.05, 5.0, state)
	assert_gt(player.speed_scale, slow * 1.5, "faster ground speed, faster legs")


func test_a_cast_plays_at_the_cast_time_and_restarts() -> void:
	var kid: KidModel = _kid(PACED)
	var player: AnimationPlayer = kid.find_child("AnimationPlayer", true, false) as AnimationPlayer
	var state: PlayerState = PlayerState.new()
	kid.play_cast(0.5)
	kid.animate(0.02, 0.0, state)
	assert_eq(player.current_animation, "cast")
	assert_almost_eq(player.speed_scale, player.get_animation("cast").length / 0.5, 0.01)
	kid.animate(0.2, 0.0, state)
	player.advance(0.2)
	var before: float = player.current_animation_position
	assert_gt(before, 0.0)
	kid.play_cast(0.5)
	kid.animate(0.02, 0.0, state)
	assert_lt(player.current_animation_position, before, "a second cast starts over")


func _hips_height(kid: KidModel) -> float:
	var skeleton: Skeleton3D = kid.find_children("*", "Skeleton3D", true, false)[0] as Skeleton3D
	var bone: int = skeleton.find_bone("hips")
	return (skeleton.global_transform * skeleton.get_bone_global_pose(bone)).origin.y - kid.global_position.y


func test_a_knockout_knocks_the_kid_down_on_their_back_and_back_up() -> void:
	var kid: KidModel = _kid(1)
	var body: Node3D = kid.find_child("Body", true, false) as Node3D
	var player: AnimationPlayer = kid.find_child("AnimationPlayer", true, false) as AnimationPlayer
	# This test advances the clip explicitly; idle frames must not advance a second clock.
	player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
	player.advance(0.0)
	await wait_process_frames(2)
	var standing: float = _hips_height(kid)
	var state: PlayerState = PlayerState.new()
	state.effects.apply(StatusEffects.Type.KNOCKOUT, 3.0)
	for i: int in 30:
		kid.animate(0.02, 0.0, state)
	assert_eq(player.current_animation, "ko", "the kid's own knock-down clip")
	player.advance(1.5)
	# Evaluate the settled blend at the same clip time before sampling the bones.
	player.advance(0.0)
	await wait_process_frames(2)  # the skeleton applies the new pose on the next frame
	assert_lt(_hips_height(kid), standing * 0.35, "lying on the street")
	assert_almost_eq(body.rotation.x, 0.0, 0.05, "the clip falls, the body is not tipped as well")
	state.effects.clear()
	for i: int in 30:
		kid.animate(0.02, 0.0, state)
	assert_eq(player.current_animation, "idle", "back on their feet")


func test_a_cast_on_the_move_moves_only_the_upper_body() -> void:
	var kid: KidModel = _kid(PACED)
	var upper: AnimationPlayer = kid.find_child("UpperPlayer", true, false) as AnimationPlayer
	assert_not_null(upper)
	var state: PlayerState = PlayerState.new()
	for i: int in 10:
		kid.animate(0.05, 5.0, state)
	kid.play_cast(0.5)
	kid.animate(0.02, 5.0, state)
	assert_eq(upper.current_animation, "cast")
	assert_almost_eq(upper.speed_scale, upper.get_animation("cast").length / 0.5, 0.01, "at the cast time")
	for clip: String in ["cast", "spell"]:
		var anim: Animation = upper.get_animation(clip)
		assert_gt(anim.get_track_count(), 20, "%s moves the arms, spine and head" % clip)
		for track: int in anim.get_track_count():
			var part: String = String(anim.track_get_path(track).get_subname(0))
			for lower: String in ["thigh", "shin", "foot", "toe", "pelvis", "hips", "root", "knee_flex", "hip_flex"]:
				assert_false(part.begins_with(lower), "%s leaves %s to the run" % [clip, part])
	for i: int in 30:
		kid.animate(0.05, 5.0, state)
	assert_false(upper.is_playing(), "the layer stops once the cast is over")


func test_heals_shields_and_traps_are_cast_with_an_open_hand() -> void:
	var spells: Array[StringName] = []
	for def: WeaponDef in RULES.weapons:
		if KidModel.is_spell(def):
			spells.append(def.id)
	spells.sort_custom(func(a: StringName, b: StringName) -> bool: return String(a) < String(b))
	assert_eq(spells, [&"heal", &"langit_lupa", &"papel_shield", &"papel_trap"] as Array[StringName])
	var kid: KidModel = _kid(3)
	var state: PlayerState = PlayerState.new()
	kid.play_cast(0.5, true)
	assert_eq(_animation_after(kid, state), "spell")
	var player: AnimationPlayer = kid.find_child("AnimationPlayer", true, false) as AnimationPlayer
	assert_eq(player.current_animation, "spell")
	assert_almost_eq(player.current_animation_position, KidModel.SPELL_FROM, 0.05, "starts after the charge-up")


func test_stars_circle_the_head_only_while_stunned() -> void:
	var kid: KidModel = _kid(0)
	var stars: Node3D = kid.find_child("Stars", true, false) as Node3D
	var state: PlayerState = PlayerState.new()
	kid.animate(0.05, 0.0, state)
	assert_false(stars.visible)
	state.effects.apply(StatusEffects.Type.STUN, 2.0)
	kid.animate(0.05, 0.0, state)
	assert_true(stars.visible)


func test_the_card_portrait_is_a_picture_of_the_kid() -> void:
	var portrait: Texture2D = KidModel.portrait(&"junjun")
	assert_not_null(portrait)
	assert_gt(portrait.get_size().y, portrait.get_size().x, "a tall portrait, not a wide empty cell")
	assert_eq(KidModel.portrait(&"nobody"), portrait, "kids with no portrait use Junjun's")


func test_the_rebuilt_kids_without_a_landing_clip_still_play_every_other_state() -> void:
	for index: int in [1, 2]:  # Ligaya and Migo (the owner's rebuilt rigs have no landing clip)
		var kid: KidModel = _kid(index)
		var state: PlayerState = PlayerState.new()
		for i: int in 10:
			kid.animate(0.05, 5.0, state)
		assert_eq(kid.current_animation(), "run", "%s runs" % RULES.characters[index].id)
		state.effects.apply(StatusEffects.Type.AIRBORNE, 2.0)
		assert_eq(_animation_after(kid, state), "airborne")
		state.effects.clear()
		assert_ne(_animation_after(kid, state), "airborne", "back on the ground, with or without a landing squash")
