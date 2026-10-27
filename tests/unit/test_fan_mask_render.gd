extends GutTest
## What the pocket mask really draws, in pixels: a hub card is rendered with and without its fan and the two images are compared, so the
## card's own panel, border and shadow cancel out and only the fan is left. Below the card's bottom edge nothing of the fan shows; above
## its top edge the fan is whole; the cut is anti-aliased (a card a half pixel off the pixel grid has blended rows).
## It needs a real renderer: the suite's headless run has none, so there it is reported as pending; run it once under a display
## (`xvfb-run godot --rendering-driver opengl3 -s addons/gut/gut_cmdln.gd -gselect=test_fan_mask_render`) to check the pixels.

const VIEW: Vector2i = Vector2i(720, 460)
const CARD_AT: Vector2 = Vector2(28.0, 160.5)


func _render(with_fan: bool) -> Image:
	var viewport: SubViewport = autofree(SubViewport.new()) as SubViewport
	viewport.size = VIEW
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(viewport)
	var kids: Array[Texture2D] = []
	for id: StringName in CardsHub.KIDS_FAN:
		kids.append(CharactersPage.card_picture(id))
	var card: CardsHubCard = CardsHubCard.create(&"characters", "Kids", "Meet the six kids", kids)
	viewport.add_child(card)
	card.position = CARD_AT
	card.size = Vector2(664.0, CardsHubCard.HEIGHT)
	card.fan.visible = with_fan
	await wait_process_frames(4)
	var image: Image = viewport.get_texture().get_image()
	_last_card = card
	return image


var _last_card: CardsHubCard


func _difference(a: Image, b: Image, x: int, y: int) -> float:
	var one: Color = a.get_pixel(x, y)
	var two: Color = b.get_pixel(x, y)
	return maxf(maxf(absf(one.r - two.r), absf(one.g - two.g)), maxf(absf(one.b - two.b), absf(one.a - two.a)))


func test_the_fan_is_cut_by_the_card_below_its_edge_whole_above_it_and_soft_at_the_cut() -> void:
	if DisplayServer.get_name() == "headless":
		pending("needs a real renderer (the headless run has none); run under a display")
		return
	var without: Image = await _render(false)
	var with: Image = await _render(true)
	var card: CardsHubCard = _last_card
	var panel: Rect2 = Rect2(CARD_AT, card.size)
	var fan: Rect2 = Rect2(card.fan.get_global_rect())
	var left: int = int(fan.position.x) + 4
	var right: int = int(minf(fan.end.x, panel.end.x)) - 4
	# 1. below the card's bottom edge (and past the shadow's own pixels, which are in both images): nothing of the fan
	var below_hits: int = 0
	var below_rows: int = 0
	for y: int in range(int(panel.end.y) + 1, mini(int(fan.end.y), VIEW.y - 1)):
		below_rows += 1
		for x: int in range(left, right):
			if _difference(with, without, x, y) > 0.004:
				below_hits += 1
	assert_gt(below_rows, 10, "the fan really reaches below the card (so there is something to hide)")
	assert_eq(below_hits, 0, "no pixel of the fan shows below the card's bottom edge")
	# 2. above the card's top edge: the fan is there, opaque where its pictures are
	var above_opaque: int = 0
	for y: int in range(int(fan.position.y) + 3, int(panel.position.y) - 2):
		for x: int in range(left, right):
			if with.get_pixel(x, y).a > 0.97 and _difference(with, without, x, y) > 0.3:
				above_opaque += 1
	assert_gt(above_opaque, 2000, "the part above the top edge is drawn whole")
	# 3. across the top edge nothing is cut: the fan is as present just under it as just over it
	var over: int = 0
	var under: int = 0
	for x: int in range(left, right):
		if _difference(with, without, x, int(panel.position.y) - 3) > 0.3:
			over += 1
		if _difference(with, without, x, int(panel.position.y) + 3) > 0.3:
			under += 1
	assert_gt(over, 40)
	assert_almost_eq(float(under), float(over), float(over) * 0.35, "no cut at the top edge: the fan carries on into the card")
	# 4. the cut is soft: on the half-pixel grid the row at the cut is a blend, neither the full fan nor none of it
	var cut_row: int = int(floorf(panel.end.y - float(card.panel_box.get_border_width(SIDE_LEFT))))
	var blended: int = 0
	var full: int = 0
	for x: int in range(left, right):
		var inside: float = _difference(with, without, x, cut_row - 4)
		if inside < 0.3:
			continue
		full += 1
		var at_cut: float = _difference(with, without, x, cut_row)
		if at_cut > inside * 0.12 and at_cut < inside * 0.88:
			blended += 1
	assert_gt(full, 40, "the fan is at the cut in many columns")
	assert_gt(blended, full / 2, "and the cut row is a blend, not a hard step")
