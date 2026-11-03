class_name AvatarPickerSheet
extends RefCounted
## "Avatar picker" for the ID photo (owner, 2026-11-03): the six kids' pictures (`assets/ui/avatars/<kid>.webp`, the owner's art, in the
## order of `Session.AVATARS`) in a grid of three by two; a tap keeps that picture as the ID photo (`IdPhoto`, on this phone only) and closes
## the sheet. The art has a white mat round it with rounded corners, so the photo is cut from inside it (`INSET`).

const DIR: String = "res://assets/ui/avatars/"
const COLUMNS: int = 3
## The mat and the picture's rounded corners are cut away: this many pixels off each side of the 512 px picture.
const INSET: int = 26
const GAP: int = 12
## Each picture's side on the sheet.
const SIDE: float = 176.0


## The picture of kid `id` ready to be the ID photo (the mat cut off), or null when the file is missing.
static func photo_of(id: String) -> Image:
	var path: String = "%s%s.webp" % [DIR, id]
	if not ResourceLoader.exists(path):
		return null
	var image: Image = (load(path) as Texture2D).get_image()
	if image == null or image.is_empty():
		return null
	var side: int = mini(image.get_width(), image.get_height()) - INSET * 2
	return image.get_region(Rect2i(INSET, INSET, side, side))


## The sheet. `on_saved` runs after a pick, once the photo is kept.
static func build(on_saved: Callable) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Pick an avatar", MenuDialog.WIDTH_SHEET)
	card.name = "AvatarPicker"
	var grid: GridContainer = GridContainer.new()
	grid.name = "Avatars"
	grid.columns = COLUMNS
	grid.add_theme_constant_override("h_separation", GAP)
	grid.add_theme_constant_override("v_separation", GAP)
	grid.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	card.body.add_child(grid)
	for id: String in Session.AVATARS:
		var picture: Image = photo_of(id)
		if picture == null:
			continue
		var pick: Button = Button.new()
		pick.name = id.capitalize()
		pick.accessibility_name = "Avatar: %s" % id.capitalize()
		pick.custom_minimum_size = Vector2.ONE * SIDE
		pick.focus_mode = Control.FOCUS_ALL
		for state: String in ["normal", "hover", "pressed", "hover_pressed", "disabled"]:
			pick.add_theme_stylebox_override(state, StyleBoxEmpty.new())
		pick.add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.RADIUS_M))
		var art: TextureRect = TextureRect.new()
		art.name = "Picture"
		art.texture = ImageTexture.create_from_image(picture)
		art.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		art.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		art.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		art.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var rounded: ShaderMaterial = ShaderMaterial.new()
		rounded.shader = UiKit.CROP
		rounded.set_shader_parameter("rect_size", Vector2.ONE * SIDE)
		rounded.set_shader_parameter("radius", float(UiSkin.RADIUS_M))
		art.material = rounded
		pick.add_child(art)
		pick.pressed.connect(func() -> void:
			if IdPhoto.save(picture) == OK:
				card.close()
				on_saved.call())
		grid.add_child(pick)
	return card
