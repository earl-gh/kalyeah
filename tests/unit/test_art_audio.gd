extends GutTest

var RULES: GameRules = load("res://data/rules/game_rules.tres") as GameRules
const LAYOUT: MapLayout = preload("res://data/rules/map_layout.tres")
## Any one of the kids (they all play their own clips, see test_authored_kids.gd).
const PACED: int = 1


func _kid(index: int) -> KidModel:
	var kid: KidModel = autofree(KidModel.new()) as KidModel
	kid.setup(RULES.characters[index])
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
	assert_true(kid.is_ghost(), "tagged kids wait greyed out on the sidewalk")


func test_kid_faces_its_movement() -> void:
	var kid: KidModel = _kid(1)
	kid.face(Vector2(1.0, 0.0))
	var forward: Vector3 = -kid.global_transform.basis.z
	assert_almost_eq(forward.x, 1.0, 0.001)





func test_the_ui_theme_is_applied_everywhere() -> void:
	var button: Button = autofree(Button.new()) as Button
	add_child(button)
	var box: StyleBoxTexture = button.get_theme_stylebox("normal") as StyleBoxTexture
	assert_not_null(box, "every button gets the painted Kalyeah game-button style")
	assert_eq(box, UiSkin.button("gold"), "the default button is the gold one")
	assert_eq(button.get_theme_font("font"), KalyeahTheme.BUTTON_FONT)
	assert_eq(KalyeahTheme.build().default_font, KalyeahTheme.BODY_FONT)
	assert_eq(KalyeahTheme.DISPLAY_FONT.get_font_name(), "Clash", "headings use Clash")
	for font: Font in [KalyeahTheme.BODY_FONT, KalyeahTheme.CAPTION_FONT, KalyeahTheme.BUTTON_FONT]:
		assert_eq((font as FontVariation).base_font, KalyeahTheme.DISPLAY_FONT, "one typeface everywhere (owner): Clash, tracked for its role")
	assert_eq(KalyeahTheme.ACTION_FONT, KalyeahTheme.DISPLAY_FONT, "numbers stay untracked")
	assert_lt((KalyeahTheme.BODY_FONT as FontVariation).spacing_glyph, (KalyeahTheme.CAPTION_FONT as FontVariation).spacing_glyph, "the smaller the text, the more it is opened up")
	assert_gt(KalyeahTheme.READING_LINE_SPACING, KalyeahTheme.LINE_SPACING, "paragraphs have air between their lines")
	var field: LineEdit = autofree(LineEdit.new()) as LineEdit
	add_child(field)
	var normal: StyleBoxFlat = field.get_theme_stylebox("normal") as StyleBoxFlat
	var focused: StyleBoxFlat = field.get_theme_stylebox("focus") as StyleBoxFlat
	assert_not_null(normal)
	assert_not_null(focused)
	assert_ne(normal.border_color, focused.border_color, "fields have a visible focus state")
	assert_eq(field.get_theme_font("font"), KalyeahTheme.BODY_FONT, "fields use Clash too")


func _animation_after(kid: KidModel, state: PlayerState, speed: float = 0.0) -> String:
	kid.animate(0.05, speed, state)
	return kid.current_animation()


func test_each_state_plays_its_own_animation() -> void:
	var kid: KidModel = _kid(PACED)
	var state: PlayerState = PlayerState.new()
	for i: int in 10:
		kid.animate(0.05, 5.0, state)
	assert_eq(kid.pose, "run")
	assert_eq(kid.current_animation(), "run")
	kid.play_cast()
	assert_eq(_animation_after(kid, state, 5.0), "cast_run", "a cast on the move is the running cast")
	assert_eq(kid.pose, "cast")
	for i: int in 20:
		kid.animate(0.05, 0.0, state)
	kid.play_cast()
	assert_eq(_animation_after(kid, state), "cast", "standing still: the standing cast")
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
	assert_ne(_animation_after(kid, state), "airborne", "back on the ground")
	state = PlayerState.new()
	state.effects.apply(StatusEffects.Type.KNOCKOUT, 2.0)
	assert_eq(_animation_after(kid, state), "stun", "no kid has a fall: knocked out shows as stunned")
	state = PlayerState.new()
	state.tagged = true
	assert_eq(_animation_after(kid, state), "idle", "tagged kids stand on the sidewalk")


func test_running_makes_footstep_signals() -> void:
	var kid: KidModel = _kid(PACED)
	var state: PlayerState = PlayerState.new()
	watch_signals(kid)
	for i: int in 60:
		kid.animate(0.03, 5.0, state)
	# 1.8 s of running: two footfalls per cycle of the run clip (0.57 s), so about six
	assert_between(get_signal_emit_count(kid, "footstep"), 5, 7)


func test_camera_shake_kicks_then_settles() -> void:
	var camera: ArenaCamera = autofree(ArenaCamera.new()) as ArenaCamera
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
		assert_not_null(Icons.skill(def.id), String(def.id))
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
		assert_almost_eq(KidModel.model_height(model) * model.scale.y, KidModel.KID_HEIGHT, 0.001, "one kid height")
		assert_null(kid.find_child("Sprite", true, false), "no sprite: a real model")


func test_the_model_has_every_animation_and_loops_only_the_continuous_ones() -> void:
	var player: AnimationPlayer = _kid(0).find_child("AnimationPlayer", true, false) as AnimationPlayer
	for clip: String in ["idle", "run", "tired", "cast", "cast_run", "stun", "airborne"]:
		assert_true(player.has_animation(clip), clip)
	for clip: String in ["idle", "run", "tired", "stun", "airborne"]:
		assert_eq(player.get_animation(clip).loop_mode, Animation.LOOP_LINEAR, "%s loops" % clip)
	for clip: String in ["cast", "cast_run"]:
		assert_eq(player.get_animation(clip).loop_mode, Animation.LOOP_NONE, "%s plays once" % clip)


func test_a_second_cast_starts_the_clip_over() -> void:
	var kid: KidModel = _kid(PACED)
	var player: AnimationPlayer = kid.find_child("AnimationPlayer", true, false) as AnimationPlayer
	var state: PlayerState = PlayerState.new()
	kid.play_cast()
	kid.animate(0.02, 0.0, state)
	assert_eq(player.current_animation, "cast")
	player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
	player.advance(0.2)
	var before: float = player.current_animation_position
	assert_gt(before, kid.cast_release("cast") + 0.1)
	kid.play_cast()
	kid.animate(0.02, 0.0, state)
	assert_lt(player.current_animation_position, before, "a second cast starts over")


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


func test_ligaya_and_migo_play_every_state() -> void:
	for index: int in [1, 2]:  # Ligaya and Migo
		var kid: KidModel = _kid(index)
		var state: PlayerState = PlayerState.new()
		for i: int in 10:
			kid.animate(0.05, 5.0, state)
		assert_eq(kid.current_animation(), "run", "%s runs" % RULES.characters[index].id)
		state.effects.apply(StatusEffects.Type.AIRBORNE, 2.0)
		assert_eq(_animation_after(kid, state), "airborne")
		state.effects.clear()
		assert_ne(_animation_after(kid, state), "airborne", "back on the ground")


func test_a_settings_row_detail_wraps_in_the_muted_tracked_caption_style() -> void:
	var row: HBoxContainer = autofree(UiKit.setting_row("Reduce motion", "Calmer menus, no drifting lights", Control.new())) as HBoxContainer
	add_child(row)
	var detail: Label = row.find_child("Detail", true, false) as Label
	assert_eq(detail.autowrap_mode, TextServer.AUTOWRAP_WORD_SMART, "long details wrap instead of running off the page")
	assert_eq(detail.theme_type_variation, &"MutedLabel")
	assert_eq(detail.get_theme_font("font"), KalyeahTheme.CAPTION_FONT, "small text is opened up")
