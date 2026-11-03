class_name AvatarPhotos
extends RefCounted
## The six avatars as ID photos (owner, 2026-11-03). They are the kids' pictures (`assets/ui/avatars/<kid>.webp`, the owner's art, in the order
## of `Session.AVATARS`), shown as tiles 3 to 8 of the nametag's photo grid (`ProfileSheet`). The first is the default: the name tag wears it
## while there is no photo, and Remove photo goes back to it. A tap keeps the picture as the ID photo (`IdPhoto`, on this phone only). The art
## has a white mat round it with rounded corners, so the photo is cut from inside it (`INSET`).

const DIR: String = "res://assets/ui/avatars/"
## The mat and the picture's rounded corners are cut away: this many pixels off each side of the 512 px picture.
const INSET: int = 26
## The tile's ring when it is the photo in use.
const RING_WIDTH: int = 6


## The id of the default avatar.
static func default_id() -> String:
	return Session.AVATARS[0]


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


## What the name tag shows while there is no saved photo: the default avatar (null if its file is missing).
static func default_texture() -> Texture2D:
	var picture: Image = photo_of(default_id())
	return null if picture == null else ImageTexture.create_from_image(picture)


## The avatar the ID photo is in use: the default while there is no photo, the one whose picture was kept, or "" for a photo of the player's own.
static func current() -> String:
	var kept: Image = IdPhoto.saved_image()
	if kept == null:
		return default_id()
	kept.convert(Image.FORMAT_RGB8)
	for id: String in Session.AVATARS:
		var fitted: Image = IdPhoto.fit(photo_of(id))
		if fitted != null and fitted.get_data() == kept.get_data():
			return id
	return ""


## A square tile `side` px wide showing kid `id`'s picture, or null when the file is missing. Its `selected` ring is a child named "Ring".
static func tile(id: String, side: float) -> Button:
	var picture: Image = photo_of(id)
	if picture == null:
		return null
	var pick: Button = Button.new()
	pick.name = id.capitalize()
	pick.accessibility_name = "Avatar: %s" % id.capitalize()
	pick.custom_minimum_size = Vector2.ONE * side
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
	rounded.set_shader_parameter("rect_size", Vector2.ONE * side)
	rounded.set_shader_parameter("radius", float(UiSkin.RADIUS_M))
	art.material = rounded
	pick.add_child(art)
	var ring: Panel = Panel.new()
	ring.name = "Ring"
	ring.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	ring.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color.TRANSPARENT
	box.border_color = KalyeahTheme.BLUE
	box.set_border_width_all(RING_WIDTH)
	box.set_corner_radius_all(UiSkin.RADIUS_M)
	box.anti_aliasing = true
	ring.add_theme_stylebox_override("panel", box)
	ring.hide()
	pick.add_child(ring)
	return pick
