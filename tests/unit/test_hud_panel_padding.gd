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
