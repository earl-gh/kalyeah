extends GutTest
## The trumpo's picture is the owner's own, palette-swapped (not repainted) to the Filipino top's colours: pale orange-tan wood and cream
## rope, instead of the red, navy and orange bands. The texture Godot reads is the one next to the .glb, so the picture inside the .glb must
## be the same file.

const TEXTURE: String = "res://assets/models/weapons/trumpo_prop_base_color.jpg"
const MODEL: String = "res://assets/models/weapons/trumpo.glb"


func _picture() -> Image:
	var texture: Texture2D = WeaponLook.picture_of(WeaponModel.look_of("trumpo"))
	var image: Image = texture.get_image()
	if image.is_compressed():
		image.decompress()
	return image


func test_no_red_or_navy_band_is_left() -> void:
	var image: Image = _picture()
	var strong: int = 0
	var count: int = 0
	for y: int in range(0, image.get_height(), 4):
		for x: int in range(0, image.get_width(), 4):
			var c: Color = image.get_pixel(x, y)
			if c.get_luminance() < 0.05:
				continue
			count += 1
			if (c.r > 0.55 and c.g < 0.3 and c.b < 0.3) or (c.b > c.r + 0.08 and c.b > 0.15):
				strong += 1
	assert_gt(count, 1000, "the picture has its islands")
	assert_lt(float(strong) / float(count), 0.01, "no red or blue is left: %d of %d" % [strong, count])


func test_the_picture_is_warm_wood_and_cream_rope() -> void:
	var image: Image = _picture()
	var wood: int = 0
	var cream: int = 0
	var count: int = 0
	for y: int in range(0, image.get_height(), 4):
		for x: int in range(0, image.get_width(), 4):
			var c: Color = image.get_pixel(x, y)
			if c.get_luminance() < 0.05:
				continue
			count += 1
			if c.r > c.g + 0.12 and c.g > c.b + 0.12:
				wood += 1
			elif c.r > 0.55 and c.r - c.b < 0.25 and c.r >= c.g:
				cream += 1
	assert_gt(float(wood) / float(count), 0.2, "the dome is orange-tan wood")
	assert_gt(float(cream) / float(count), 0.2, "the rope cone is cream")


func test_the_picture_inside_the_glb_is_the_texture_next_to_it() -> void:
	var glb: PackedByteArray = FileAccess.get_file_as_bytes(MODEL)
	var jpg: PackedByteArray = FileAccess.get_file_as_bytes(TEXTURE)
	assert_gt(glb.size(), jpg.size())
	assert_eq(_picture().get_size(), Vector2i(512, 512), "still 512 px")
	var found: bool = false
	var first: PackedByteArray = jpg.slice(0, 64)
	var at: int = 0
	while at >= 0 and not found:
		at = glb.find(0xFF, at)
		if at < 0 or at + jpg.size() > glb.size():
			break
		found = glb.slice(at, at + 64) == first and glb.slice(at, at + jpg.size()) == jpg
		at += 1
	assert_true(found, "trumpo.glb holds the same JPEG as trumpo_prop_base_color.jpg")
