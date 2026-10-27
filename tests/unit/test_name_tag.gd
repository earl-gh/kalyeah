extends GutTest
## The name tag on the main screen: a white card on a black lanyard between the logo row and the models behind the chosen one.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")
var _saved_name: String = ""


func before_each() -> void:
	_saved_name = Session.player_name
	Session.player_name = Session.DEFAULT_NAME


func after_each() -> void:
	Session.player_name = _saved_name


func _screen() -> TitleScreen:
	var phone: Control = autofree(Control.new()) as Control
	phone.custom_minimum_size = Vector2(720.0, 1280.0)
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	phone.add_child(screen)
	# the layout is measured at rest: the swing has its own test
	screen.name_tag.swing = false
	return screen


func test_a_new_player_is_called_gabo() -> void:
	assert_eq(Session.DEFAULT_NAME, "GABO")
	Session.player_name = ""
	Session.ensure_profile()
	assert_eq(Session.player_name, "GABO", "a new profile starts with the default name")


func test_the_tag_says_hi_im_and_the_name_in_capitals_and_has_no_edit_button() -> void:
	var screen: TitleScreen = _screen()
	await wait_frames(4)
	var tag: NameTag = screen.name_tag
	assert_eq(tag.greeting_label.text, "Hi! I'm")
	assert_eq(tag.name_label.text, "GABO")
	assert_lt(tag.greeting_label.get_theme_font_size("font_size"), tag.name_label.get_theme_font_size("font_size"), "the greeting is the smaller row")
	assert_lt(tag.greeting_label.get_global_rect().end.y, tag.name_label.get_global_rect().position.y + 4.0, "row 1 above row 2")
	assert_null(tag.find_child("EditButton", true, false), "no edit button (owner: tap the tag)")
	assert_null(tag.find_child("Pencil", true, false), "and no pencil")
	assert_almost_eq(tag.name_label.get_global_rect().get_center().x, tag.card.get_global_rect().get_center().x, 2.0, "the name is centred on the card")
	tag.set_player_name("Ana")
	assert_eq(tag.name_label.text, "ANA", "a name is shown in capitals")
	assert_eq(tag.player_name(), "Ana", "and kept as typed")
	tag.set_player_name("   ")
	assert_eq(tag.name_label.text, "GABO", "an empty name shows the default")


func test_the_card_is_white_with_a_punched_slot_at_the_top_centre() -> void:
	var screen: TitleScreen = _screen()
	await wait_frames(4)
	var tag: NameTag = screen.name_tag
	var paper: StyleBoxFlat = tag.card.get_node("Paper").get_theme_stylebox("panel") as StyleBoxFlat
	assert_eq(paper.bg_color, Color.WHITE, "a white card")
	assert_gt(paper.corner_radius_top_left, 0, "rounded")
	var card: Rect2 = tag.card.get_global_rect()
	var slot: Rect2 = tag.slot.get_global_rect()
	assert_almost_eq(slot.get_center().x, card.get_center().x, 1.5, "the slot is in the middle across")
	assert_lt(slot.get_center().y - card.position.y, 40.0, "and near the top")
	assert_lt(slot.size.y, slot.size.x / 2.0, "a small slot, wider than tall")
	var paper_node: Panel = tag.card.get_node("Paper") as Panel
	var material: ShaderMaterial = paper_node.material as ShaderMaterial
	assert_not_null(material, "the card is cut by a shader")
	assert_eq(material.shader, NameTag.PAPER_SHADER)
	assert_eq(material.get_shader_parameter("slot_half"), NameTag.SLOT_SIZE / 2.0, "a hole the size of the slot")
	assert_eq(material.get_shader_parameter("rect_size"), tag.card.size)
	assert_eq(material.get_shader_parameter("slot_centre"), Vector2(tag.card.size.x / 2.0, NameTag.SLOT_MIDDLE), "cut at the slot")
	assert_false(tag.slot is Panel, "nothing is painted in the slot: it is a hole, so the back strap and the background show through")
	assert_true(NameTag.PAPER_SHADER.code.contains("COLOR = vec4(base.rgb, base.a * hole)"), "the hole is transparent")
	assert_almost_eq(card.get_center().x, 360.0, 3.0, "the card hangs in the middle of the screen")


func test_the_lanyard_is_two_black_straps_one_in_front_and_one_behind_meeting_at_the_slot() -> void:
	var screen: TitleScreen = _screen()
	await wait_frames(4)
	var tag: NameTag = screen.name_tag
	for piece: ColorRect in [tag.front_strap, tag.back_strap]:
		assert_lt(((piece.material as ShaderMaterial).get_shader_parameter("base_colour") as Color).get_luminance(), 0.08, "%s is black webbing" % piece.name)
		assert_lt((piece.get_global_transform() * Vector2(piece.size.x / 2.0, 0.0)).y, 0.0, "%s starts above the top of the screen" % piece.name)
		# its bottom end is the middle of the slot
		var foot: Vector2 = piece.get_global_transform() * Vector2(piece.size.x / 2.0, piece.size.y)
		var hole: Vector2 = tag.slot.get_global_rect().get_center()
		assert_almost_eq(foot.x, hole.x, 2.0, "%s meets the slot (across)" % piece.name)
		assert_almost_eq(foot.y, hole.y, 2.0, "%s meets the slot (down)" % piece.name)
	assert_lt(tag.back_strap.z_index, tag.card.z_index, "the back strap is behind the card")
	assert_gt(tag.front_cap.z_index, tag.card.z_index, "the front strap's end is over the card")
	assert_lt(tag.front_strap.rotation, 0.0, "the front strap leans one way")
	assert_gt(tag.back_strap.rotation, 0.0, "the back strap the other: a V that opens upward")
	assert_eq(tag.front_cap.rotation, tag.front_strap.rotation, "the cap is the same strap")
	var top_front: float = (tag.front_strap.get_global_transform() * Vector2(tag.front_strap.size.x / 2.0, 0.0)).x
	var top_back: float = (tag.back_strap.get_global_transform() * Vector2(tag.back_strap.size.x / 2.0, 0.0)).x
	assert_lt(top_front, top_back, "so the two spread apart going up")
	assert_gt(tag.front_strap.z_index, 0)
	assert_eq((screen.find_child("Logo", true, false) as Control).z_index, NameTag.LOGO_Z)


func test_the_card_is_centred_between_the_bottom_of_the_logo_and_the_top_of_the_chosen_model() -> void:
	for height: float in [1280.0, 1558.0]:
		var phone: Control = autofree(Control.new()) as Control
		phone.custom_minimum_size = Vector2(720.0, height)
		phone.size = Vector2(720.0, height)
		add_child(phone)
		var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
		phone.add_child(screen)
		screen.name_tag.swing = false
		await wait_frames(6)
		var logo: Rect2 = (screen.find_child("Logo", true, false) as Control).get_global_rect()
		var paper: Rect2 = (screen.name_tag.card.get_node("Paper") as Control).get_global_rect()
		var stage_top: float = (screen.carousel.get_parent() as Control).get_global_rect().position.y
		var model_top: float = stage_top + screen.carousel.front_top()
		var above: float = paper.position.y - logo.end.y
		var below: float = model_top - paper.end.y
		assert_almost_eq(above, below, 2.0, "%d px: the same room above and under the card (%.1f above, %.1f below)" % [int(height), above, below])
		assert_gt(above, 8.0, "%d px: room above" % int(height))
		var back_top: float = stage_top + screen.carousel.back_top()
		assert_lt(paper.end.y, back_top, "%d px: and the card stays clear of the models behind" % int(height))


func test_a_long_name_takes_smaller_letters_and_never_leaves_the_screen() -> void:
	var screen: TitleScreen = _screen()
	await wait_frames(4)
	var tag: NameTag = screen.name_tag
	tag.set_player_name("MMMMMMMMMMMM")
	await wait_frames(2)
	var card: Rect2 = tag.card.get_global_rect()
	assert_gte(card.position.x, 0.0)
	assert_lte(card.end.x, 720.0, "inside the screen")
	assert_lte(card.size.x, NameTag.CARD_MAX_WIDTH + 1.0)
	assert_lte(tag.name_label.get_global_rect().size.x, card.size.x, "the name fits its card")
	tag.set_player_name("Ana")
	await wait_frames(2)
	assert_almost_eq(tag.card.size.x, NameTag.CARD_MIN_WIDTH, 0.5, "a short name keeps the card at its least width")


func test_tapping_the_tag_opens_the_profile_sheet_and_saving_updates_the_tag() -> void:
	var screen: TitleScreen = _screen()
	await wait_frames(4)
	var tag: NameTag = screen.name_tag
	assert_almost_eq(tag.tap_button.get_global_rect().size.x, tag.card.get_global_rect().size.x, 1.0, "the whole card is the button")
	assert_almost_eq(tag.tap_button.get_global_rect().size.y, tag.card.get_global_rect().size.y, 1.0)
	assert_false(tag.tap_button.accessibility_name.is_empty())
	tag.tap_button.pressed.emit()
	assert_not_null(screen.dialog)
	assert_eq(screen.dialog.name, "ProfileSheet", "the modal for changing the player name")
	var field: LineEdit = screen.dialog.find_child("NameField", true, false) as LineEdit
	field.text = "Ligaya"
	(screen.dialog.find_child("Save", true, false) as Button).pressed.emit()
	assert_eq(Session.player_name, "Ligaya")
	assert_eq(tag.name_label.text, "LIGAYA", "the tag shows the new name")


func test_the_tag_swings_very_slowly_like_a_soft_wind_and_rests_with_reduce_motion_or_a_cover() -> void:
	var screen: TitleScreen = _screen()
	await wait_frames(4)
	var tag: NameTag = screen.name_tag
	var motion: bool = Settings.reduced_motion
	Settings.reduced_motion = false
	tag.swing = true
	var seen: Array[float] = []
	var steps: Array[float] = []
	for i: int in 20:
		await wait_seconds(0.2)
		seen.append(tag.rotation)
		if seen.size() > 1:
			steps.append(absf(seen[seen.size() - 1] - seen[seen.size() - 2]))
	assert_gt(seen.max() - seen.min(), 0.004, "it moves, left and right")
	assert_lte(absf(seen.max()), deg_to_rad(NameTag.SWAY_DEGREES) + 0.001, "only a little")
	assert_lte(absf(seen.min()), deg_to_rad(NameTag.SWAY_DEGREES) + 0.001)
	assert_gte(NameTag.SWAY_SECONDS, 8.0, "a cycle takes many seconds")
	assert_lt(steps.max(), 0.012, "and never turns fast (a step in 0.2 s)")
	assert_lte(absf(tag.card.rotation), deg_to_rad(NameTag.LAG_DEGREES) + 0.001, "the card only trails a little")
	Settings.reduced_motion = true
	await wait_frames(3)
	assert_eq(tag.rotation, 0.0, "it rests with Reduce motion")
	assert_eq(tag.card.rotation, 0.0)
	Settings.reduced_motion = false
	screen.open_quit_confirm()
	await wait_frames(3)
	assert_false(tag.swing, "and under a sheet")
	assert_eq(tag.rotation, 0.0)
	Settings.reduced_motion = motion


func test_the_logo_is_in_front_of_the_lanyard_and_the_card_in_front_of_the_lights() -> void:
	var screen: TitleScreen = _screen()
	await wait_frames(4)
	var tag: NameTag = screen.name_tag
	var logo: Control = screen.find_child("Logo", true, false) as Control
	for piece: ColorRect in [tag.back_strap, tag.front_strap, tag.front_cap]:
		assert_gt(logo.z_index, piece.z_index, "the logo is over %s (owner)" % piece.name)
	assert_gt(tag.card.z_index, UiBackdrop.LIGHT_Z, "the card is over the backdrop's lights")
	assert_lt(tag.card.z_index, screen.mode_models[screen.carousel.index].z_index, "and under the chosen model")


func test_the_tag_goes_with_the_main_page_under_a_page() -> void:
	var screen: TitleScreen = _screen()
	await wait_frames(4)
	assert_true(screen.name_tag.is_visible_in_tree())
	screen.open_page(&"settings")
	assert_false(screen.name_tag.is_visible_in_tree(), "hidden under Settings")
	screen.close_page()
	assert_true(screen.name_tag.is_visible_in_tree())


func test_the_tag_fits_between_the_logo_and_the_models_on_tall_and_short_phones() -> void:
	for height: float in [1100.0, 1280.0, 1558.0]:
		var phone: Control = autofree(Control.new()) as Control
		phone.custom_minimum_size = Vector2(720.0, height)
		phone.size = Vector2(720.0, height)
		add_child(phone)
		var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
		phone.add_child(screen)
		screen.name_tag.swing = false
		await wait_frames(6)
		var tag: NameTag = screen.name_tag
		var logo: Rect2 = (screen.find_child("Logo", true, false) as Control).get_global_rect()
		var card: Rect2 = tag.card.get_global_rect()
		var back_top: float = (screen.carousel.get_parent() as Control).get_global_rect().position.y + screen.carousel.back_top()
		assert_gt(card.position.y, logo.end.y - 1.0, "%d px: below the logo row" % int(height))
		assert_lt(card.end.y, back_top + 1.0, "%d px: above the models behind" % int(height))
		assert_gte(tag.card.scale.x, TitleScreen.TAG_MIN_SCALE - 0.001, "%d px: not shrunk past its least" % int(height))
		assert_lt((tag.front_strap.get_global_transform() * Vector2(tag.front_strap.size.x / 2.0, 0.0)).y, 0.0, "%d px: the lanyard still leaves the screen" % int(height))


func test_a_name_is_at_most_ten_letters_and_the_widest_ten_fit_the_tag_at_full_size() -> void:
	assert_eq(Session.MAX_NAME_LENGTH, 10)
	var screen: TitleScreen = _screen()
	await wait_frames(4)
	var tag: NameTag = screen.name_tag
	tag.set_player_name("M".repeat(Session.MAX_NAME_LENGTH))
	await wait_frames(2)
	assert_eq(tag.name_label.get_theme_font_size("font_size"), UiMetrics.HEADLINE, "ten of the widest letters still take the largest size")
	assert_lte(tag.card.size.x, NameTag.CARD_MAX_WIDTH)


func test_a_name_keeps_only_letters_digits_and_a_few_marks_in_single_spaces() -> void:
	assert_eq(Session.clean_name("  Ma   ria  "), "Ma ria", "one space at a time, trimmed")
	assert_eq(Session.clean_name("Ñoño_99"), "Ñoño_99", "any language's letters, digits and an underscore")
	assert_eq(Session.clean_name("Ana-Lei O'Brien"), "Ana-Lei O'", "a hyphen and an apostrophe, cut at ten")
	assert_eq(Session.clean_name("<b>Ben</b>"), "bBenb", "no brackets or slashes")
	assert_eq(Session.clean_name("Ben😀\n\tX"), "BenX", "no emoji, new lines or tabs")
	assert_eq(Session.clean_name("!!!"), "", "nothing left is empty (the profile asks for a name)")
	assert_eq(Session.filter_name("a  b"), "a b")


func test_the_profile_field_filters_as_you_type_and_counts_the_letters() -> void:
	var screen: TitleScreen = _screen()
	await wait_frames(2)
	var card: MenuDialog = screen.open_name_editor()
	var field: LineEdit = card.find_child("NameField", true, false) as LineEdit
	assert_eq(field.max_length, 10, "the field takes ten letters at most")
	field.text = "Ab"
	field.text_changed.emit("Ab")
	var counted: bool = false
	for label: Node in card.find_children("*", "Label", true, false):
		if (label as Label).text == "2 / 10 letters":
			counted = true
	assert_true(counted, "a live count: 2 / 10 letters")
	field.text = "A<b>c"
	field.text_changed.emit("A<b>c")
	assert_eq(field.text, "Abc", "brackets are dropped as they are typed or pasted")
