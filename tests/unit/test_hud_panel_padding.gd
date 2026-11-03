extends GutTest

func test_clock_caption_and_time_fit_with_padding_in_every_stage() -> void:
	var chip: ClockChip = autofree(ClockChip.new()) as ClockChip
	chip.size = Scoreboard.middle_rect(MatchHud.SCORE_SIZE, MatchHud.CLOCK_GAP).size
	add_child(chip)
	var content: Rect2 = Rect2(ClockChip.INNER_LEFT, ClockChip.INNER_TOP, chip.size.x - ClockChip.INNER_LEFT - ClockChip.INNER_RIGHT, chip.size.y - ClockChip.INNER_TOP - ClockChip.INNER_BOTTOM)
	for seconds: float in [0.0, 65.0, 599.0]:
		for stage: MatchSim.ClockStage in [MatchSim.ClockStage.REGULAR, MatchSim.ClockStage.EXTENDED, MatchSim.ClockStage.OVERTIME]:
			chip.show_time(seconds, stage)
			var lines: Array[Rect2] = chip.text_rects()
			for line: Rect2 in lines:
				if line.size != Vector2.ZERO:
					assert_true(content.encloses(line), "%s / %s includes its outline inside the padding" % [chip.caption_text(), chip.text()])
			assert_eq(chip.has_caption(), stage != MatchSim.ClockStage.REGULAR, "only the stages after the regular one name themselves")
			assert_gte(lines[1].position.y - lines[0].end.y, ClockChip.LINE_GAP)


func test_the_clock_stands_between_the_two_team_cells_and_fits_the_gap() -> void:
	var middle: Rect2 = Scoreboard.middle_rect(MatchHud.SCORE_SIZE, MatchHud.CLOCK_GAP)
	var board: Scoreboard = autofree(Scoreboard.new()) as Scoreboard
	board.size = MatchHud.SCORE_SIZE
	var cells: Array[Rect2] = board.cell_rects()
	assert_almost_eq(middle.position.x - cells[0].end.x, MatchHud.CLOCK_GAP, 0.01, "clear of the blue cell")
	assert_almost_eq(cells[1].position.x - middle.end.x, MatchHud.CLOCK_GAP, 0.01, "clear of the red cell")
	assert_almost_eq(middle.size.y, cells[0].size.y, 0.01, "as tall as the cells")
	assert_gt(middle.size.x, 100.0, "room for the caption and the time")
