class_name RoomMapPicker
extends RefCounted
## Owns the room map overlay and its lifetime, separate from team selection.

var view: HotspotRoomView


func _init(p_view: HotspotRoomView) -> void:
	view = p_view


func open_map_carousel() -> void:
	if view._map_overlay != null and is_instance_valid(view._map_overlay):
		return
	if not view._is_host():
		return
	view._map_overlay = Control.new()
	view._map_overlay.name = "MapOverlay"
	view._map_overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	view._map_overlay.mouse_filter = Control.MOUSE_FILTER_STOP
	view._map_overlay.z_index = MenuDialog.MODAL_Z
	view.add_child(view._map_overlay)
	var tint: ColorRect = ColorRect.new()
	tint.name = "Tint"
	tint.color = Color(0.01, 0.025, 0.06, 0.82)
	tint.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	tint.mouse_filter = Control.MOUSE_FILTER_STOP
	tint.gui_input.connect(_on_tint_input)
	view._map_overlay.add_child(tint)
	var maps: Array[Dictionary] = []
	var current: int = 0
	var chosen: StringName = StringName(view._room.get("map", "street") as String)
	for id: StringName in RoomService.MAP_IDS:
		if id == chosen:
			current = maps.size()
		maps.append({"id": String(id), "title": view.MAP_NAMES.get(id, String(id).to_upper()), "color": view.MAP_COLORS.get(id, "blue"),
			"heading": "" if id in RoomService.PLAYABLE_MAP_IDS else "Coming soon"})
	view.map_carousel = MenuCarousel.new()
	view.map_carousel.name = "MapCarousel"
	view.map_carousel.card_size = view.MAP_PICK_CARD
	view.map_carousel.custom_minimum_size = Vector2(0.0, view.MAP_PICK_CARD.y + 24.0)
	# the full width, centred up and down
	view.map_carousel.anchor_left = 0.0
	view.map_carousel.anchor_right = 1.0
	view.map_carousel.anchor_top = 0.5
	view.map_carousel.anchor_bottom = 0.5
	view.map_carousel.offset_left = 0.0
	view.map_carousel.offset_right = 0.0
	view.map_carousel.offset_top = -(view.MAP_PICK_CARD.y + 24.0) / 2.0
	view.map_carousel.offset_bottom = (view.MAP_PICK_CARD.y + 24.0) / 2.0
	view._map_overlay.add_child(view.map_carousel)
	view._map_hint = Label.new()
	view._map_hint.name = "MapHint"
	view._map_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	view._map_hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	view._map_hint.add_theme_font_size_override("font_size", UiMetrics.CAPTION)
	view._map_hint.add_theme_color_override("font_color", KalyeahTheme.CREAM)
	# just under the carousel (which is centred up and down)
	view._map_hint.anchor_left = 0.0
	view._map_hint.anchor_right = 1.0
	view._map_hint.anchor_top = 0.5
	view._map_hint.anchor_bottom = 0.5
	view._map_hint.offset_left = view.MARGIN
	view._map_hint.offset_right = -view.MARGIN
	view._map_hint.offset_top = view.MAP_PICK_CARD.y / 2.0 + 40.0
	view._map_hint.offset_bottom = view.MAP_PICK_CARD.y / 2.0 + 110.0
	view._map_hint.mouse_filter = Control.MOUSE_FILTER_IGNORE
	view._map_overlay.add_child(view._map_hint)
	view.map_carousel.setup("", maps, current)
	view.map_carousel.selected.connect(_on_map_carousel_selected)


func close_map_carousel() -> void:
	if not is_instance_valid(view):
		return
	if view._map_overlay != null and is_instance_valid(view._map_overlay):
		view._map_overlay.queue_free()
	view._map_overlay = null
	view.map_carousel = null


func _on_tint_input(event: InputEvent) -> void:
	var click: InputEventMouseButton = event as InputEventMouseButton
	if click != null and click.pressed and click.button_index == MOUSE_BUTTON_LEFT:
		close_map_carousel()


func _on_map_carousel_selected(index: int) -> void:
	var id: StringName = RoomService.MAP_IDS[index]
	if not id in RoomService.PLAYABLE_MAP_IDS:
		# listed, but not playable yet: say so and keep the room on its map
		view._map_hint.text = "%s is coming soon! For now, Agawan Base is the only street open." % view.MAP_NAMES.get(id, String(id).to_upper())
		return
	view._map_hint.text = ""
	view.map_chosen.emit(id)
	# a moment to see the card come forward, then back to the room
	view.get_tree().create_timer(0.35).timeout.connect(close_map_carousel)


