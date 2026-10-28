extends GutTest
## The APK stays under its size budget (.github/scripts/inspect-apk.sh, 190 MB) and the phone's texture memory stays small because the
## owner's 3D model textures (props, weapons, kids) are imported VRAM compressed (ETC2 on Android): lossless they were 2 to 10 MB each
## on disk and a full uncompressed 2048 px texture each in the phone's memory.

const MODEL_DIRS: Array[String] = ["res://assets/models/props", "res://assets/models/weapons", "res://assets/models/characters"]
const COMPRESS_LOSSLESS: int = 0
const COMPRESS_VRAM: int = 2


func _texture_imports() -> PackedStringArray:
	var found: PackedStringArray = []
	for dir_path: String in MODEL_DIRS:
		for file_name: String in DirAccess.get_files_at(dir_path):
			if file_name.ends_with(".png.import") or file_name.ends_with(".jpg.import"):
				found.append("%s/%s" % [dir_path, file_name])
	return found


func _param(import_path: String, key: String) -> Variant:
	var cfg: ConfigFile = ConfigFile.new()
	assert_eq(cfg.load(import_path), OK, import_path)
	return cfg.get_value("params", key, null)


func test_no_model_texture_is_lossless() -> void:
	var imports: PackedStringArray = _texture_imports()
	assert_gt(imports.size(), 40, "found the model textures")
	for import_path: String in imports:
		assert_ne(int(_param(import_path, "compress/mode")), COMPRESS_LOSSLESS,
			"%s is imported lossless; use VRAM compressed (compress/mode=2)" % import_path)


func test_kid_and_prop_textures_are_vram_compressed() -> void:
	for import_path: String in _texture_imports():
		assert_eq(int(_param(import_path, "compress/mode")), COMPRESS_VRAM, import_path)


func test_authored_kid_normal_maps_are_flagged_as_normal_maps() -> void:
	for kid: String in ["junjun", "ligaya", "migo", "toni", "popoy", "inday"]:
		var path: String = "res://assets/models/characters/%s_2.png.import" % kid
		assert_eq(int(_param(path, "compress/normal_map")), 1, "%s's normal map is compressed as a normal map" % kid)
