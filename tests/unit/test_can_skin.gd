extends GutTest
## The can model wears the Urdaneta corned-beef skin (tools/art/bake_can_skin.py): a yellow, red and blue picture, not the black
## it had. The texture Godot reads is the one next to the .glb, so the picture inside the .glb must be the same file.

const TEXTURE: String = "res://assets/models/weapons/can_prop_base_color.jpg"
const MODEL: String = "res://assets/models/weapons/can.glb"


func _average(image: Image) -> Color:
	var sum: Color = Color(0.0, 0.0, 0.0, 0.0)
	var count: int = 0
	for y: int in range(0, image.get_height(), 8):
		for x: int in range(0, image.get_width(), 8):
			sum += image.get_pixel(x, y)
			count += 1
	return sum / float(count)


func test_the_can_texture_is_the_bright_label_not_the_old_black_one() -> void:
	var texture: Texture2D = WeaponLook.picture_of(WeaponModel.look_of("can"))
	assert_not_null(texture, "the can's look carries its picture")
	var image: Image = texture.get_image()
	if image.is_compressed():
		image.decompress()
	var average: Color = _average(image)
	assert_gt(average.get_luminance(), 0.3, "the old skin averaged about 0.1")
	assert_gt(average.r, average.b + 0.1, "yellow and red, not grey")


func test_the_picture_inside_the_glb_is_the_texture_next_to_it() -> void:
	var glb: PackedByteArray = FileAccess.get_file_as_bytes(MODEL)
	var jpg: PackedByteArray = FileAccess.get_file_as_bytes(TEXTURE)
	assert_gt(glb.size(), jpg.size())
	var found: bool = false
	var first: PackedByteArray = jpg.slice(0, 64)
	var at: int = 0
	while at >= 0 and not found:
		at = glb.find(0xFF, at)
		if at < 0 or at + jpg.size() > glb.size():
			break
		found = glb.slice(at, at + 64) == first and glb.slice(at, at + jpg.size()) == jpg
		at += 1
	assert_true(found, "can.glb holds the same JPEG as can_prop_base_color.jpg")
