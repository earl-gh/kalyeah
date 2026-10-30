extends GutTest
## The trumpo's picture is the owner's own file, its pixels edited to a bare, unpainted pale wood with a gentle grain and a steel-grey pin
## (the owner's photo: no paint, no bands, no rope, no bottle cap). The texture Godot reads is the one next to the .glb, so the picture
## inside the .glb must be the same file; and the weapon look must not turn the wood lavender in the shade.

const TEXTURE: String = "res://assets/models/weapons/trumpo_prop_base_color.jpg"
const MODEL: String = "res://assets/models/weapons/trumpo.glb"


func _picture() -> Image:
	var texture: Texture2D = WeaponLook.picture_of(WeaponModel.look_of("trumpo"))
	var image: Image = texture.get_image()
	if image.is_compressed():
		image.decompress()
	return image


## The used texels (the padding between islands is the island's own colour), as colours.
func _texels() -> Array[Color]:
	var image: Image = _picture()
	var out: Array[Color] = []
	for y: int in range(0, image.get_height(), 4):
		for x: int in range(0, image.get_width(), 4):
			var c: Color = image.get_pixel(x, y)
			if c.get_luminance() >= 0.05:
				out.append(c)
	return out


func _is_steel(c: Color) -> bool:
	return c.s < 0.25


func test_no_paint_is_left_no_red_no_navy_no_orange_band_no_cream_rope() -> void:
	var texels: Array[Color] = _texels()
	assert_gt(texels.size(), 1000, "the picture has its islands")
	var paint: int = 0
	var cream: int = 0
	for c: Color in texels:
		if _is_steel(c):
			continue
		if (c.r > 0.55 and c.g < 0.3 and c.b < 0.3) or (c.b > c.r + 0.04) or c.s > 0.7:
			paint += 1
		if c.r > 0.9 and c.g > 0.84 and c.b > 0.7:
			cream += 1
	assert_eq(paint, 0, "no red, blue or saturated orange texel")
	assert_eq(cream, 0, "no cream rope")


func test_the_body_is_pale_natural_wood_and_the_pin_is_grey_steel() -> void:
	var texels: Array[Color] = _texels()
	var wood: Color = Color(0, 0, 0)
	var woods: int = 0
	var steel: int = 0
	var odd: int = 0
	var hues: Array[float] = []
	for c: Color in texels:
		if _is_steel(c):
			steel += 1
			if c.get_luminance() > 0.62 or c.get_luminance() < 0.2:
				odd += 1
		else:
			wood += c
			woods += 1
			hues.append(c.h)
	assert_lt(float(odd) / float(maxi(steel, 1)), 0.05, "steel is mid-grey, not white and not black (%d of %d texels are not: the island edges)" % [odd, steel])
	var share: float = float(woods) / float(texels.size())
	assert_gt(share, 0.88, "wood dominates the picture (%.2f)" % share)
	assert_gt(float(steel) / float(texels.size()), 0.01, "the pin has its own islands, in steel grey")
	var mean: Color = wood / float(woods)
	var beech: Color = Color("E0B27A")
	assert_almost_eq(mean.r, beech.r, 0.08, "the wood is the pale beech of the brief (#E0B27A): red %.2f" % mean.r)
	assert_almost_eq(mean.g, beech.g, 0.08, "green %.2f" % mean.g)
	assert_almost_eq(mean.b, beech.b, 0.09, "blue %.2f" % mean.b)
	hues.sort()
	assert_gt(hues[int(hues.size() * 0.02)], 0.06, "every hue is tan, none red (2%% of texels are redder than hue %.3f)" % hues[int(hues.size() * 0.02)])
	assert_lt(hues[int(hues.size() * 0.98)], 0.13, "and none yellow-green")


func test_the_grain_is_gentle_not_bands() -> void:
	var lights: Array[float] = []
	var sum: float = 0.0
	var sum_sq: float = 0.0
	var count: int = 0
	for c: Color in _texels():
		if _is_steel(c):
			continue
		var l: float = c.get_luminance()
		lights.append(l)
		sum += l
		sum_sq += l * l
		count += 1
	var mean: float = sum / float(count)
	var deviation: float = sqrt(maxf(sum_sq / float(count) - mean * mean, 0.0))
	assert_gt(deviation, 0.012, "a figure is there (deviation %.3f)" % deviation)
	assert_lt(deviation, 0.07, "but it is gentle")
	lights.sort()
	var spread: float = lights[int(lights.size() * 0.99)] - lights[int(lights.size() * 0.01)]
	assert_lt(spread, 0.36, "no band is far darker or lighter than the wood round it (1%% to 99%% spread %.2f)" % spread)


func test_the_weapon_look_keeps_the_wood_brown_in_the_shade_not_lavender() -> void:
	var shade: Vector3 = (WeaponModel.look_of("trumpo") as ShaderMaterial).get_shader_parameter(&"shadow_tint") as Vector3
	assert_lte(shade.z, shade.y * 1.1, "the shade keeps no more blue than green: %s" % shade)
	assert_gt(shade.x, shade.y, "and more red than green: warm")
	assert_gt(shade.x, 0.5, "clearly darker but never black")
	var other: ShaderMaterial = WeaponModel.look_of("slipper") as ShaderMaterial
	var default_tint: Variant = other.get_shader_parameter(&"shadow_tint")
	assert_null(default_tint, "the other weapons keep the shader's own violet shade (only the trumpo's is warm)")


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
