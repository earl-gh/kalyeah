extends GutTest
## The base posts' colour pictures keep all three colour channels. A texture flagged as a normal map in its import settings drops
## blue, which turned the red post's concrete yellow and the blue post's flyer green.

const POST_TEXTURES: Array[String] = [
	"res://assets/models/props/post_red_2.png",
	"res://assets/models/props/post_blue_0.png",
	"res://assets/models/props/post_blue_2.png",
]


func test_post_pictures_are_not_imported_as_normal_maps() -> void:
	for path: String in POST_TEXTURES:
		var config: ConfigFile = ConfigFile.new()
		assert_eq(config.load(path + ".import"), OK, path)
		assert_ne(config.get_value("params", "compress/normal_map", 0), 1, "%s must not be a normal map" % path)


func test_the_red_posts_concrete_keeps_its_blue_channel() -> void:
	var image: Image = (load(POST_TEXTURES[0]) as Texture2D).get_image()
	if image.is_compressed():
		image.decompress()
	var blue: float = 0.0
	var count: int = 0
	for y: int in range(0, image.get_height(), 8):
		for x: int in range(0, image.get_width(), 8):
			blue += image.get_pixel(x, y).b
			count += 1
	assert_gt(blue / float(count), 0.4, "a cream concrete picture, not a yellow one")
