@tool
extends EditorPlugin
## Puts Kalyeah's Android plugin (android/kalyeah_local, built into bin/ by the Android APK workflow) into the Android export.
## Editor only: the game itself reaches the plugin through `LocalLink`.

var _export: KalyeahLocalExport


func _enter_tree() -> void:
	_export = KalyeahLocalExport.new()
	add_export_plugin(_export)


func _exit_tree() -> void:
	remove_export_plugin(_export)
	_export = null


class KalyeahLocalExport extends EditorExportPlugin:
	func _get_name() -> String:
		return "KalyeahLocal"

	func _supports_platform(platform: EditorExportPlatform) -> bool:
		return platform is EditorExportPlatformAndroid

	## Relative to res://addons/.
	func _get_android_libraries(_platform: EditorExportPlatform, debug: bool) -> PackedStringArray:
		return PackedStringArray(["kalyeah_local/bin/kalyeah_local-%s.aar" % ("debug" if debug else "release")])
