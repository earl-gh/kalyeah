extends GutTest
## .github/scripts/inspect-apk.sh against output that `aapt2 dump badging` and `dump xmltree` really printed for an exported
## APK (tests/fixtures/android/, captured from a local Godot 4.6.2 debug export). The first version of the script looked for
## lines aapt2 does not print and failed a good APK; these tests keep it reading what is really there. The
## CHANGE_WIFI_MULTICAST_STATE and CAMERA lines were added by hand, in the form of the captured INTERNET and VIBRATE lines, because the
## capture predates those permissions; replace them with a fresh capture (the workflow prints the raw aapt2 output).

const SCRIPT := "res://.github/scripts/inspect-apk.sh"
const BADGING := "res://tests/fixtures/android/badging.txt"
const MANIFEST := "res://tests/fixtures/android/manifest.txt"
const VERSION := "0.0.0"


func _read(path: String) -> String:
	return FileAccess.get_file_as_string(path)


func _save(name: String, text: String) -> String:
	var path: String = "user://%s" % name
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	file.store_string(text)
	file.close()
	return ProjectSettings.globalize_path(path)


func _inspect(badging: String, manifest: String) -> Dictionary:
	var output: Array = []
	var code: int = OS.execute("bash", [
		ProjectSettings.globalize_path(SCRIPT), "--dumps", badging, manifest, VERSION,
	], output, true)
	return {"code": code, "text": "\n".join(output)}


func _broken(from: String, to: String) -> Dictionary:
	var manifest: String = _read(MANIFEST)
	assert_true(manifest.contains(from), "the fixture holds %s" % from)
	var result: Dictionary = _inspect(
		ProjectSettings.globalize_path(BADGING), _save("manifest_broken.txt", manifest.replace(from, to)))
	DirAccess.remove_absolute("user://manifest_broken.txt")
	return result


func test_the_real_output_of_a_good_apk_passes_every_check() -> void:
	var result: Dictionary = _inspect(ProjectSettings.globalize_path(BADGING), ProjectSettings.globalize_path(MANIFEST))
	assert_eq(result["code"], 0, result["text"])
	assert_false(String(result["text"]).contains("FAIL"), result["text"])
	for check: String in ["min SDK", "target SDK", "launcher activity", "OpenGL ES 3.0", "portrait", "backups", "arm64-v8a", "permissions"]:
		assert_true(String(result["text"]).contains(check), "%s is checked" % check)


func test_a_min_sdk_above_24_fails() -> void:
	var result: Dictionary = _broken("minSdkVersion(0x0101020c)=24", "minSdkVersion(0x0101020c)=26")
	assert_eq(result["code"], 1)
	assert_true(String(result["text"]).contains("FAIL  min SDK is 24 or lower"))


func test_a_target_sdk_below_35_fails() -> void:
	var result: Dictionary = _broken("targetSdkVersion(0x01010270)=35", "targetSdkVersion(0x01010270)=34")
	assert_true(String(result["text"]).contains("FAIL  target SDK is 35 or higher"))


func test_a_missing_launcher_fails() -> void:
	var result: Dictionary = _broken("android.intent.category.LAUNCHER", "android.intent.category.INFO")
	assert_true(String(result["text"]).contains("FAIL  it has a launcher activity"))


func test_opengl_es_2_fails() -> void:
	var result: Dictionary = _broken("glEsVersion(0x01010281)=0x00030000", "glEsVersion(0x01010281)=0x00020000")
	assert_true(String(result["text"]).contains("FAIL  it needs OpenGL ES 3.0"))


func test_landscape_and_backups_on_fail() -> void:
	assert_true(String(_broken("screenOrientation(0x0101001e)=1", "screenOrientation(0x0101001e)=0")["text"]).contains(
		"FAIL  the activity is locked to portrait"))
	assert_true(String(_broken("allowBackup(0x01010280)=false", "allowBackup(0x01010280)=true")["text"]).contains(
		"FAIL  backups are off"))


func test_a_failure_prints_the_raw_aapt2_output_to_read() -> void:
	var result: Dictionary = _broken("minSdkVersion(0x0101020c)=24", "minSdkVersion(0x0101020c)=26")
	var text: String = result["text"]
	assert_true(text.contains("aapt2 dump badging"), "the badging header")
	assert_true(text.contains("package: name='com.kalyeah.game'"), "the raw badging lines")
	assert_true(text.contains("aapt2 dump xmltree"), "the xmltree header")
	assert_true(text.contains("E: uses-sdk"), "the raw manifest lines")


func test_a_missing_multicast_permission_fails() -> void:
	var badging: String = _read(BADGING).replace("uses-permission: name='android.permission.CHANGE_WIFI_MULTICAST_STATE'\n", "")
	var result: Dictionary = _inspect(_save("badging_missing.txt", badging), ProjectSettings.globalize_path(MANIFEST))
	DirAccess.remove_absolute("user://badging_missing.txt")
	assert_true(String(result["text"]).contains("FAIL  only the permissions the game uses"), "the hotspot's broadcast needs it")


func test_an_extra_permission_fails() -> void:
	var badging: String = _read(BADGING) + "uses-permission: name='android.permission.RECORD_AUDIO'\n"
	var result: Dictionary = _inspect(_save("badging_broken.txt", badging), ProjectSettings.globalize_path(MANIFEST))
	DirAccess.remove_absolute("user://badging_broken.txt")
	assert_true(String(result["text"]).contains("FAIL  only the permissions the game uses"))
