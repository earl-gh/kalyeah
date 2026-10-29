extends GutTest

func test_clock_caption_and_time_fit_with_padding_even_in_overtime() -> void:
	var chip: ClockChip = autofree(ClockChip.new()) as ClockChip
	chip.size = MatchHud.CLOCK_SIZE
	add_child(chip)
	var content: Rect2 = Rect2(ClockChip.PADDING, chip.size - ClockChip.PADDING * 2.0)
	for seconds: float in [0.0, 65.0, 5999.0]:
		for overtime: bool in [false, true]:
			chip.show_time(seconds, overtime)
			var lines: Array[Rect2] = chip.text_rects()
			for line: Rect2 in lines:
				assert_true(content.encloses(line), "%s / %s includes its outline inside the padding" % [chip.caption_text(), chip.text()])
			assert_gte(lines[1].position.y - lines[0].end.y, ClockChip.LINE_GAP)

func test_score_numbers_keep_padding_and_do_not_overlap_capture_progress() -> void:
	var board: Scoreboard = autofree(Scoreboard.new()) as Scoreboard
	board.size = Vector2(720.0, 1280.0)
	add_child(board)
	board.set_play_area(Rect2(28.8, 150.0, 662.4, 850.0))
	var font: Font = KalyeahTheme.ACTION_FONT
	for cell: Rect2 in board.cell_rects():
		assert_true(Rect2(Vector2.ZERO, board.size).encloses(cell))
		assert_gte(cell.get_center().x, board.play_area.end.x)
		for capturing: bool in [false, true]:
			var area: Rect2 = board.value_rect(cell, capturing)
			for text: String in ["0", "1", "2", "3"]:
				var ink: Vector2 = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, Scoreboard.BIG_FONT) + Vector2.ONE * Scoreboard.TEXT_OUTLINE * 2.0
				assert_lte(ink.x, area.size.x)
				assert_lte(ink.y, area.size.y)
			if capturing:
				assert_gte(board.capture_rect(cell).position.y - area.end.y, Scoreboard.CAPTURE_GAP)
