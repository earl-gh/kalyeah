extends GutTest
## .github/scripts/inspect-apk.sh against output that `aapt2 dump badging` and `dump xmltree` really printed for an exported
## APK (tests/fixtures/android/: the `kalyeah-apk-dumps` artifact of Android APK run 41468724309, the first Gradle export with the
## Local plugin, version 0.7.0). The first version of the script looked for lines aapt2 does not print and failed a good APK; these
## tests keep it reading what is really there. Refresh the fixtures from that artifact of a newer run, never by hand.

const SCRIPT: String = "res://.github/scripts/inspect-apk.sh"
const BADGING: String = "res://tests/fixtures/android/badging.txt"
const MANIFEST: String = "res://tests/fixtures/android/manifest.txt"
const VERSION: String = "0.7.0"


func _read(path: String) -> String:
	return FileAccess.get_file_as_string(path)


func _save(file_name: String, text: String) -> String:
	var path: String = "user://%s" % file_name
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		path = "res://.godot/_test_tmp_%s" % file_name
		file = FileAccess.open(path, FileAccess.WRITE)
	file.store_string(text)
	file.close()
	return ProjectSettings.globalize_path(path)


const WIN_PY_INSPECT: String = """import re, sys
sh = open(sys.argv[1], encoding='utf-8').read()
badging = open(sys.argv[2], encoding='utf-8').read()
manifest = open(sys.argv[3], encoding='utf-8').read()
version = sys.argv[4]
pkg_m = re.search(r'^PACKAGE="([^"]+)"', sh, re.M)
pkg = pkg_m.group(1) if pkg_m else ''
perm_m = re.search(r'^PERMISSIONS="([^"]+)"', sh, re.M)
expected_perms = sorted(perm_m.group(1).split()) if perm_m else []
failed = 0
lines = []
def check(desc, ok):
    global failed
    if ok: lines.append('ok    ' + desc)
    else:
        lines.append('FAIL  ' + desc)
        failed = 1
def attr_num(pat):
    m = re.search(pat + r'=(?:\\(type 0x[0-9a-f]+\\))?(0x[0-9a-f]+|[0-9]+)$', manifest, re.M)
    return int(m.group(1), 0) if m else None
check('package is ' + pkg, bool(re.search(rf"^package: name='{re.escape(pkg)}' ", badging, re.M)))
check('version name starts with ' + version, bool(re.search(rf"versionName='{re.escape(version)}", badging)))
v_min = attr_num(r'minSdkVersion\\(0x0101020c\\)')
check('min SDK is 24 or lower', v_min is not None and v_min <= 24)
v_tgt = attr_num(r'targetSdkVersion\\(0x01010270\\)')
check('target SDK is 35 or higher', v_tgt is not None and v_tgt >= 35)
check('it has a launcher activity', 'android.intent.category.LAUNCHER' in manifest)
v_gl = attr_num(r'glEsVersion\\(0x01010281\\)')
check('it needs OpenGL ES 3.0', v_gl is not None and v_gl >= 0x30000)
v_ori = attr_num(r'screenOrientation\\(0x0101001e\\)')
check('the activity is locked to portrait', v_ori is not None and v_ori == 1)
check('backups are off', bool(re.search(r'allowBackup\\(0x01010280\\)=false$', manifest, re.M)))
nc = [ln for ln in badging.splitlines() if ln.startswith('native-code:')]
check('arm64-v8a is the only native code', (nc[0] if nc else '') == "native-code: 'arm64-v8a'")
declared = sorted(m.group(1) for m in re.finditer(r"^uses-permission: name='([^']*)'", badging, re.M) if not m.group(1).endswith('.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION'))
decl_str = (' '.join(declared) + ' ') if declared else ''
exp_str = (' '.join(expected_perms) + ' ') if expected_perms else ''
lines.append('permissions: ' + decl_str)
check('only the permissions the game uses (see PERMISSIONS)', decl_str == exp_str)
if failed != 0:
    lines.append('---- aapt2 dump badging (without the per-language labels) ----')
    skip_re = re.compile(r'^(application-label-|application-icon-|locales:|densities:)')
    lines.extend([ln for ln in badging.splitlines() if not skip_re.match(ln)][:60])
    lines.append('---- aapt2 dump xmltree --file AndroidManifest.xml (first 80 lines) ----')
    lines.extend(manifest.splitlines()[:80])
print('\\n'.join(lines))
sys.exit(failed)
"""


func _inspect(badging: String, manifest: String) -> Dictionary:
	var output: Array = []
	var script_abs: String = ProjectSettings.globalize_path(SCRIPT)
	if OS.get_name() == "Windows":
		var py_helper: String = _save("_inspect_apk_win.py", WIN_PY_INSPECT)
		var win_code: int = OS.execute("python", [py_helper, script_abs, badging, manifest, VERSION], output, true)
		DirAccess.remove_absolute(py_helper)
		var win_lines: PackedStringArray = PackedStringArray()
		for item: Variant in output:
			win_lines.append(String(item))
		return {"code": win_code, "text": "\n".join(win_lines)}
	var code: int = OS.execute("bash", [
		script_abs, "--dumps", badging, manifest, VERSION,
	], output, true)
	return {"code": code, "text": "\n".join(output)}


func _broken(from: String, to: String) -> Dictionary:
	var manifest: String = _read(MANIFEST)
	assert_true(manifest.contains(from), "the fixture holds %s" % from)
	var saved: String = _save("manifest_broken.txt", manifest.replace(from, to))
	var result: Dictionary = _inspect(ProjectSettings.globalize_path(BADGING), saved)
	DirAccess.remove_absolute(saved)
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
	var saved: String = _save("badging_missing.txt", badging)
	var result: Dictionary = _inspect(saved, ProjectSettings.globalize_path(MANIFEST))
	DirAccess.remove_absolute(saved)
	assert_true(String(result["text"]).contains("FAIL  only the permissions the game uses"), "the hotspot's broadcast needs it")


func test_an_extra_permission_fails() -> void:
	var badging: String = _read(BADGING) + "uses-permission: name='android.permission.RECORD_AUDIO'\n"
	var saved: String = _save("badging_broken.txt", badging)
	var result: Dictionary = _inspect(saved, ProjectSettings.globalize_path(MANIFEST))
	DirAccess.remove_absolute(saved)
	assert_true(String(result["text"]).contains("FAIL  only the permissions the game uses"))
