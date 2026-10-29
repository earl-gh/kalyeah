extends GutTest

var _history_path: String
var _player_name: String


func before_each() -> void:
	_history_path = MatchHistory.path
	_player_name = Session.player_name
	MatchHistory.path = "user://history_polish_test.cfg"
	MatchHistory.clear()


func after_each() -> void:
	MatchHistory.clear()
	MatchHistory.path = _history_path
	Session.player_name = _player_name


func test_history_limits_legacy_files_to_twenty_and_puts_filters_above_the_kpis() -> void:
	var entries: Array[Dictionary] = []
	for index: int in 30:
		entries.append({"mode": "practice", "mine": 30 - index, "theirs": 0, "won": true, "at": 30 - index})
	var file: ConfigFile = ConfigFile.new()
	file.set_value("history", "entries", entries)
	file.save(MatchHistory.path)
	var recent: Array[Dictionary] = MatchHistory.entries()
	assert_eq(recent.size(), 20)
	assert_eq(recent[0]["mine"], 30)
	assert_eq(recent[-1]["mine"], 11)
	var page: HistoryPage = autofree(HistoryPage.new()) as HistoryPage
	add_child(page)
	assert_lt(page.filter_bar.get_index(), page.summary_card.get_index())
	assert_eq(page.played_label.text, "20")
	assert_eq(page.find_child("List", true, false).get_child_count(), 20)


func test_every_month_fits_without_scrolling_at_both_portrait_heights() -> void:
	var viewport: SubViewport = autofree(SubViewport.new()) as SubViewport
	add_child(viewport)
	for height: int in [1280, 1558]:
		viewport.size = Vector2i(720, height)
		for month: int in range(1, 13):
			var card: MenuDialog = GameOfMonthSheet.build(month)
			card.open(viewport)
			await wait_process_frames(3)
			var scroll: ScrollContainer = card.find_child("DialogScroll", true, false) as ScrollContainer
			assert_lte(card.panel.size.y, float(height) * MenuDialog.MAX_HEIGHT_SHARE + 1.0, "month %d stays within the modal height" % month)
			assert_lte(card.body.size.y, scroll.size.y + 1.0, "month %d needs no scroll" % month)
			assert_eq(card.body.find_children("*", "Button", true, false).size(), 0, "the header X is the only close button")
			card.close(false)


func test_the_name_editor_reuses_the_tag_and_places_the_field_on_its_name_row() -> void:
	var card: MenuDialog = ProfileSheet.build(Callable())
	card.open(self)
	await wait_process_frames(3)
	var preview: NameTag = card.find_child("Preview", true, false) as NameTag
	var field: LineEdit = card.find_child("NameField", true, false) as LineEdit
	assert_eq(field.get_parent(), preview.card)
	assert_true(preview.strap.visible)
	assert_true(preview.swing)
	assert_lt(preview.sway_scale, 0.2)
	assert_true(preview.front_strap.z_as_relative)
	assert_lt(preview.back_strap.z_index, preview.card.z_index)
	assert_gt(preview.front_cap.z_index, preview.card.z_index)
	assert_eq(preview.greeting_label.text, NameTag.GREETING)
	assert_eq(preview.photo_frame.position, Vector2(NameTag.PHOTO_LEFT, NameTag.PHOTO_TOP))
	assert_almost_eq(field.position.x + 8.0, preview.name_label.position.x, 0.1)
	field.text = "LIGAYA"
	field.text_changed.emit(field.text)
	assert_eq(preview.player_name(), "LIGAYA")
	assert_eq(Session.player_name, _player_name, "preview typing does not save")
	assert_null(card.find_child("Cancel", true, false))
	assert_false(card.close_button.is_visible_in_tree())
	assert_false((card.find_child("Header", true, false) as Control).visible)
	assert_true(card.panel.get_theme_stylebox("panel") is StyleBoxEmpty)
	assert_true(card.inner.get_theme_stylebox("panel") is StyleBoxEmpty)
	card.close(false)


func test_the_header_x_closes_without_running_the_footer_action() -> void:
	var card: MenuDialog = MenuDialog.create("Question")
	var applied: Array[bool] = [false]
	var save: Button = MenuDialog.make_button("Save", "gold")
	save.pressed.connect(func() -> void: applied[0] = true)
	card.add_buttons([save])
	card.open(self)
	await wait_process_frames(2)
	assert_true(save.is_visible_in_tree())
	assert_true(card.close_button.is_visible_in_tree())
	card.close_button.pressed.emit()
	assert_false(card.is_open)
	assert_false(applied[0])


func test_populated_history_and_all_filter_controls_fit_the_phone() -> void:
	MatchHistory.record("online", true, 3, 1)
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: TitleScreen = preload("res://scenes/ui/title_screen.tscn").instantiate() as TitleScreen
	phone.add_child(screen)
	screen.open_page(&"history")
	await wait_seconds(0.35)
	await wait_process_frames(4)
	assert_lte(screen.history_page.get_global_rect().end.x, 720.0)
	assert_almost_eq((screen.history_page.find_child("Title", true, false) as Label).get_global_rect().get_center().x, 360.0, 1.0)
	assert_lte(screen.history_page.filter_bar.get_global_rect().end.x, 720.0)
	assert_lte(screen.history_page.streak_label.get_global_rect().end.x, 720.0)


func test_editing_lifts_the_tag_out_of_the_menu_and_scrim_restores_it() -> void:
	var screen: TitleScreen = autofree(preload("res://scenes/ui/title_screen.tscn").instantiate()) as TitleScreen
	add_child(screen)
	var original: String = Session.player_name
	var card: MenuDialog = screen.open_name_editor()
	await wait_process_frames(3)
	assert_false(screen.name_tag.visible)
	assert_true(card.is_visible_in_tree())
	assert_eq(card.z_index, MenuDialog.MODAL_Z)
	(card.find_child("NameField", true, false) as LineEdit).text = "LIGAYA"
	card._on_dim_input(TouchHelper.click(Vector2(5.0, 5.0), true))
	assert_true(screen.name_tag.visible)
	assert_eq(Session.player_name, original)


func test_home_modals_keep_one_header_x_and_no_footer_dismissal() -> void:
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: TitleScreen = preload("res://scenes/ui/title_screen.tscn").instantiate() as TitleScreen
	phone.add_child(screen)
	for method: StringName in [&"open_practice_confirm", &"open_quit_confirm", &"open_game_of_the_month", &"open_donate"]:
		var card: MenuDialog = screen.call(method) as MenuDialog
		await wait_process_frames(3)
		assert_true(card.close_button.is_visible_in_tree(), String(method))
		assert_eq(card.close_button.text, "X")
		assert_true(card.panel.get_global_rect().encloses(card.close_button.get_global_rect()))
		assert_gt(card.close_button.get_global_rect().get_center().x, card.panel.get_global_rect().get_center().x)
		for node: Node in card.body.find_children("*", "Button", true, false):
			assert_false((node as Button).text in ["Cancel", "Close", "Done", "Okay", "Resume"], String(method))
		card.close(false)


func test_nametag_without_a_close_button_dismisses_by_outside_touch_and_back() -> void:
	var viewport: SubViewport = autofree(SubViewport.new()) as SubViewport
	viewport.size = Vector2i(720, 1280)
	add_child(viewport)
	var screen: TitleScreen = preload("res://scenes/ui/title_screen.tscn").instantiate() as TitleScreen
	viewport.add_child(screen)
	var original: String = Session.player_name
	var card: MenuDialog = screen.open_name_editor()
	(card.find_child("NameField", true, false) as LineEdit).text = "LIGAYA"
	await wait_process_frames(4)
	assert_false(card.close_button.is_visible_in_tree())
	var field: LineEdit = card.find_child("NameField", true, false) as LineEdit
	field.release_focus()
	var inside: Vector2 = field.get_global_rect().get_center()
	viewport.push_input(TouchHelper.click(inside, true))
	viewport.push_input(TouchHelper.click(inside, false))
	assert_true(field.has_focus(), "the editor still receives touches")
	assert_true(card.is_open, "touching the tag does not dismiss it")
	viewport.push_input(TouchHelper.click(Vector2(8.0, 500.0), true))
	viewport.push_input(TouchHelper.click(Vector2(8.0, 500.0), false))
	await wait_process_frames(3)
	assert_false(is_instance_valid(card) and card.is_open, "outside touch dismisses the editor")
	assert_eq(Session.player_name, original)
	assert_true(screen.name_tag.visible)
	var again: MenuDialog = screen.open_name_editor()
	await wait_process_frames(3)
	viewport.propagate_notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await wait_process_frames(3)
	assert_false(is_instance_valid(again) and again.is_open, "Android Back dismisses the editor")
	assert_true(screen.name_tag.visible)
